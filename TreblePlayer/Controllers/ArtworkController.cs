using Microsoft.AspNetCore.Mvc;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Using Microsoft.Extensions.Logging for consistency
using Microsoft.AspNetCore.Http; // Needed for IFormFile

namespace TreblePlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtworkController : ControllerBase
{
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly IArtworkService _artworkService;
    private readonly ILoggingService _logger; // Assuming ILoggingService maps to Microsoft's ILogger or similar
    private readonly string _tempUploadPath = Path.Combine(Path.GetTempPath(), "trebleplayer_uploads"); // Temp location for uploads

    public ArtworkController(
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        IArtworkService artworkService,
        ILoggingService logger)
    {
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _artworkService = artworkService;
        _logger = logger;

         // Ensure temp directory exists
        if (!Directory.Exists(_tempUploadPath))
        {
            Directory.CreateDirectory(_tempUploadPath);
        }
    }

    [HttpGet("track/{trackId}")]
    public async Task<IActionResult> GetTrackArtwork(int trackId)
    {
        try
        {
            var track = await _trackRepository.GetTrackByIdAsync(trackId);
            if (track == null)
            {
                 _logger.LogWarning($"Track artwork requested but track not found: ID {trackId}");
                return await GetDefaultArtwork();
            }

            string artworkPath = await _artworkService.GetArtworkPathAsync(track);

            return await GetArtworkFile(artworkPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting track artwork for ID {trackId}", ex);
            return await GetDefaultArtwork();
        }
    }

    [HttpGet("album/{albumId}")]
    public async Task<IActionResult> GetAlbumArtwork(int albumId)
    {
        try
        {
             var album = await _collectionRepository.GetAlbumByIdAsync(albumId);
            string artworkPath; 
            if (album != null && !string.IsNullOrEmpty(album.ArtworkPath) && System.IO.File.Exists(album.ArtworkPath))
            {
                artworkPath = album.ArtworkPath;
            }
            else
            {
                 _logger.LogWarning($"Album artwork requested but album or its artwork not found: ID {albumId}");
                artworkPath = _artworkService.GetDefaultArtworkPath();
            }
            
            return await GetArtworkFile(artworkPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting album artwork for ID {albumId}", ex);
            return await GetDefaultArtwork();
        }
    }

    [HttpGet("playlist/{playlistId}")]
    public async Task<IActionResult> GetPlaylistArtwork(int playlistId)
    {
        try
        {
            var playlist = await _collectionRepository.GetPlaylistByIdAsync(playlistId);
            string artworkPath; 
            string specificDefault = _artworkService.GetDefaultPlaylistArtworkPath();

            if (playlist != null && !string.IsNullOrEmpty(playlist.ArtworkPath) && System.IO.File.Exists(playlist.ArtworkPath))
            {
                 artworkPath = playlist.ArtworkPath; 
            }
            else
            {
                artworkPath = specificDefault;
            }

            return await GetArtworkFile(artworkPath, specificDefault);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting playlist artwork for ID {playlistId}", ex);
             return await GetArtworkFile(_artworkService.GetDefaultPlaylistArtworkPath(), _artworkService.GetDefaultPlaylistArtworkPath());
        }
    }

    [HttpPost("playlist/{playlistId}/upload")]
    [Consumes("multipart/form-data")] // Specify expected content type
    public async Task<IActionResult> UploadPlaylistArtwork(int playlistId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded." });
        }

        // --- Basic Validation (Optional but recommended) ---
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var fileExt = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExt))
        {
            return BadRequest(new { message = "Invalid file type. Only JPG and PNG are allowed." });
        }
        // Add size validation if needed
        // if (file.Length > MAX_FILE_SIZE) return BadRequest(...);
        // --- End Validation ---


        try
        {
            var playlist = await _collectionRepository.GetPlaylistByIdAsync(playlistId);
            if (playlist == null)
            {
                return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
            }

            // Save temporarily
            var tempFilePath = Path.Combine(_tempUploadPath, Guid.NewGuid().ToString() + fileExt); // Unique temp name
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
             _logger.LogDebug($"Temporarily saved uploaded file for playlist {playlistId} to {tempFilePath}");


            // Use ArtworkService to copy to final location and get the path
            var finalArtworkPath = _artworkService.SaveArtworkToPlaylist(playlist, tempFilePath, fileExt);

            // Update playlist entity
            playlist.ArtworkPath = finalArtworkPath;
            playlist.LastModified = DateTime.UtcNow; // Update modification time
            await _collectionRepository.UpdateCollectionAsync(playlist); // Use UpdateCollectionAsync which calls SaveChangesAsync

            // Clean up temporary file
            if (System.IO.File.Exists(tempFilePath))
            {
                 System.IO.File.Delete(tempFilePath);
                 _logger.LogDebug($"Deleted temporary file {tempFilePath}");
            }

            return Ok(new { message = "Playlist artwork updated successfully.", artworkPath = finalArtworkPath });

        }
        catch (Exception ex)
        {
             _logger.LogError($"Error uploading artwork for playlist {playlistId}", ex);
            return StatusCode(500, new { message = "An error occurred while uploading the artwork." });
        }
    }

    private async Task<IActionResult> GetArtworkFile(string? filePath, string defaultPath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            _logger.LogWarning($"Artwork file not found or path invalid: {filePath}. Falling back to default: {defaultPath}");
            filePath = defaultPath;

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                 _logger.LogError($"Default artwork placeholder not found at: {filePath}");
                 return NotFound("Artwork not found, and default placeholder is missing.");
            }
        }

        try
        {
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            string contentType = GetContentType(filePath);
            return File(fileBytes, contentType);
        }
        catch (FileNotFoundException)
        {
             _logger.LogError($"Artwork file specifically not found during stream creation: {filePath}");
             return await GetArtworkFile(defaultPath, defaultPath);
        }
         catch (Exception ex)
        {
            _logger.LogError($"Error reading artwork file: {filePath}", ex);
            return await GetArtworkFile(defaultPath, defaultPath);
        }
    }

    private async Task<IActionResult> GetArtworkFile(string? filePath)
    {
        return await GetArtworkFile(filePath, _artworkService.GetDefaultArtworkPath());
    }

    private async Task<IActionResult> GetDefaultArtwork()
    {
        return await GetArtworkFile(_artworkService.GetDefaultArtworkPath());
    }

    private string GetContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            // Add other types if needed
            _ => "application/octet-stream", // Default MIME type
        };
    }
} 