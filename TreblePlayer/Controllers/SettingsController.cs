using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Core; // <<< Add this using statement
using TreblePlayer.Services; // <<< Add using for Services
using System.Collections.Generic; // For List<>
using System.Threading.Tasks; // For Task<>
using System.IO; // Added for Path.GetFullPath
using System.ComponentModel.DataAnnotations; // Added for [Required]
using Microsoft.Extensions.Logging; // Added for ILogger

namespace TreblePlayer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly MusicPlayerDbContext _context;
        private readonly PlaybackWebSocketHandler _webSocketHandler;
        private readonly ILogger<SettingsController> _logger; // Use standard ILogger
        private readonly IMetadataService _metadataService; // <<< Add field
        private readonly OrphanedDataCleanupService _cleanupService; // Add this field

        public SettingsController(
            MusicPlayerDbContext context, 
            PlaybackWebSocketHandler webSocketHandler, 
            ILogger<SettingsController> logger,
            IMetadataService metadataService,
            OrphanedDataCleanupService cleanupService) // Add parameter
        {
            _context = context;
            _webSocketHandler = webSocketHandler;
            _logger = logger;
            _metadataService = metadataService;
            _cleanupService = cleanupService; // Assign field
        }

        // GET: api/settings/monitoredfolders
        [HttpGet("monitoredfolders")]
        public async Task<ActionResult<IEnumerable<MonitoredFolder>>> GetMonitoredFolders()
        {
            _logger.LogInformation("Getting monitored folders.");
            return await _context.MonitoredFolders.OrderBy(f => f.Path).ToListAsync();
        }

        // POST: api/settings/monitoredfolders
        [HttpPost("monitoredfolders")]
        public async Task<ActionResult<MonitoredFolder>> AddMonitoredFolder([FromBody] MonitoredFolderCreateModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Path))
            {
                return BadRequest("Folder path cannot be empty.");
            }

            // Basic validation/normalization (consider more robust path validation)
            string normalizedPath = Path.GetFullPath(model.Path); // Ensure consistent format
            _logger.LogInformation($"Attempting to add monitored folder: {normalizedPath}");

            // Check if it already exists
            bool exists = await _context.MonitoredFolders.AnyAsync(f => f.Path.ToLower() == normalizedPath.ToLower());
            if (exists)
            {
                 _logger.LogWarning($"Folder already monitored: {normalizedPath}");
                return Conflict("This folder path is already being monitored.");
            }

            var newFolder = new MonitoredFolder
            {
                Path = normalizedPath,
                DateAdded = DateTime.UtcNow
            };

            try
            {
                _context.MonitoredFolders.Add(newFolder);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully added monitored folder: {normalizedPath} (ID: {newFolder.Id})");

                // Notify frontend via WebSocket
                _webSocketHandler.BroadcastNotification("MonitoredFoldersUpdated");

                // Automatically scan the new folder
                try
                {
                    _logger.LogInformation($"Automatically scanning newly added folder: {normalizedPath}");
                    await _metadataService.ScanMusicFolderAsync(normalizedPath);
                    _logger.LogInformation($"Automatic scan of new folder completed: {normalizedPath}");
                    
                    // Notify frontend that library has been updated
                    _webSocketHandler.BroadcastNotification("LibraryUpdated");
                }
                catch (Exception scanEx)
                {
                    _logger.LogError(scanEx, $"Error during automatic scan of new folder: {normalizedPath}");
                    // Continue - we still want to return success for adding the folder
                }

                // Return the created object (consistent with REST patterns)
                return CreatedAtAction(nameof(GetMonitoredFolders), new { id = newFolder.Id }, newFolder);
            }
            catch (DbUpdateException ex) // Catch potential unique constraint violation
            {
                 _logger.LogError(ex, $"Database error adding monitored folder: {normalizedPath}");
                 // Check inner exception if needed
                 return StatusCode(500, "An error occurred while saving the folder path.");
            }
             catch (Exception ex)
            {
                 _logger.LogError(ex, $"Unexpected error adding monitored folder: {normalizedPath}");
                 return StatusCode(500, "An unexpected error occurred.");
            }
        }

        // DELETE: api/settings/monitoredfolders/{id}
        [HttpDelete("monitoredfolders/{id}")]
        public async Task<IActionResult> RemoveMonitoredFolder(int id)
        {
             _logger.LogInformation($"Attempting to remove monitored folder with ID: {id}");
            var folderToRemove = await _context.MonitoredFolders.FindAsync(id);

            if (folderToRemove == null)
            {
                _logger.LogWarning($"Monitored folder not found for removal: ID {id}");
                return NotFound();
            }

            string folderPath = folderToRemove.Path; // Store the path before removing the folder

            try
            {
                _context.MonitoredFolders.Remove(folderToRemove);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully removed monitored folder: {folderPath} (ID: {id})");

                // Notify frontend via WebSocket about the folder removal
                _webSocketHandler.BroadcastNotification("MonitoredFoldersUpdated");
                
                // Immediately trigger cleanup for the removed folder
                try
                {
                    _logger.LogInformation($"Triggering immediate cleanup for removed folder: {folderPath}");
                    await _cleanupService.CleanupRemovedFolderAsync(folderPath);
                    _logger.LogInformation($"Cleanup for removed folder completed successfully: {folderPath}");
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, $"Error during cleanup of removed folder: {folderPath}");
                    // We still return success for the folder removal even if cleanup fails
                }

                return NoContent(); // Standard success response for DELETE
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing monitored folder with ID: {id}");
                return StatusCode(500, "An error occurred while removing the folder path.");
            }
        }

        // --- Scan Endpoints --- 

        // POST: api/settings/scan/all
        [HttpPost("scan/all")]
        public async Task<IActionResult> ScanAllMonitoredFolders()
        {
            _logger.LogInformation("API request received to scan all monitored folders.");
            try
            {
                // Get all monitored folder paths from the database
                var monitoredFolders = await _context.MonitoredFolders.Select(f => f.Path).ToListAsync();

                if (!monitoredFolders.Any())
                {
                    _logger.LogWarning("Scan all requested, but no folders are monitored in the database.");
                    return BadRequest("No folders are currently monitored. Add folders via POST /api/settings/monitoredfolders.");
                }

                _logger.LogInformation($"Initiating scan for {monitoredFolders.Count} monitored folder(s).");

                // Trigger the scan service (this might take time)
                // Consider running this in the background for long scans
                await _metadataService.ScanMusicFromDirectoryAsync(monitoredFolders);
                
                _logger.LogInformation("Scan of all monitored folders completed successfully (via API trigger).");
                // MetadataService will send the WebSocket notification upon its completion
                return Ok("Scan initiated for all monitored folders."); // Or Accepted() if run in background
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during scan of all monitored folders.");
                return StatusCode(500, "An unexpected error occurred during the library scan.");
            }
        }

        // POST: api/settings/scan/folder
        [HttpPost("scan/folder")]
        public async Task<IActionResult> ScanSpecificFolder([FromBody] ScanFolderRequestModel model)
        {
             if (model == null || string.IsNullOrWhiteSpace(model.Path))
            {
                return BadRequest("Folder path cannot be empty in request body.");
            }

            string requestedPath = model.Path;
            _logger.LogInformation($"API request received to scan specific folder: {requestedPath}");

            try
            {
                // Optional: Validate if path seems plausible (e.g., is it rooted?)
                // More robust validation might be needed depending on OS/security.
                string absolutePath = Path.GetFullPath(requestedPath); // Normalize

                if (!Directory.Exists(absolutePath))
                {
                    _logger.LogWarning($"Scan requested for non-existent folder: {absolutePath}");
                    return NotFound($"Directory not found: {absolutePath}");
                }

                 _logger.LogInformation($"Initiating scan for specific folder: {absolutePath}");

                // Trigger scan for the single folder
                // Assuming ScanMusicFolderAsync handles scanning a single directory path
                await _metadataService.ScanMusicFolderAsync(absolutePath);

                _logger.LogInformation($"Scan of specific folder completed successfully (via API trigger): {absolutePath}");
                // MetadataService will send the WebSocket notification upon its completion
                return Ok($"Scan initiated for folder: {absolutePath}"); // Or Accepted()
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, $"Error occurred during scan of specific folder: {requestedPath}");
                 return StatusCode(500, "An unexpected error occurred during the folder scan.");
            }
        }

        // POST: api/settings/normalize-artists
        [HttpPost("normalize-artists")]
        public async Task<IActionResult> NormalizeArtistNames()
        {
            _logger.LogInformation("API request received to normalize artist names in albums.");
            try
            {
                // Call the metadata service to normalize artist names
                await _metadataService.NormalizeExistingArtistNames();
                
                _logger.LogInformation("Artist name normalization completed successfully.");
                return Ok("Artist names normalized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during artist name normalization.");
                return StatusCode(500, "An unexpected error occurred during artist name normalization.");
            }
        }
    }

    // Simple DTO for adding a folder
    public class MonitoredFolderCreateModel
    {
        [Required]
        public string Path { get; set; } = string.Empty;
    }

    // Simple DTO for scanning a specific folder
    public class ScanFolderRequestModel
    {
        [Required]
        public string Path { get; set; } = string.Empty;
    }
} 