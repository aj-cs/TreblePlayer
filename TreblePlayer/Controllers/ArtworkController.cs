using Microsoft.AspNetCore.Mvc;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging; // Using Microsoft.Extensions.Logging for consistency

namespace TreblePlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtworkController : ControllerBase
{
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly IArtworkService _artworkService;
    private readonly ILoggingService _logger; // Assuming ILoggingService maps to Microsoft's ILogger or similar

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

            // Use the ArtworkService to determine the correct path (handles fallback logic)
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
            if (album == null)
            {
                 _logger.LogWarning($"Album artwork requested but album not found: ID {albumId}");
                return await GetDefaultArtwork();
            }

            // Directly use the album's path if available, otherwise default
            string artworkPath = (!string.IsNullOrEmpty(album.ArtworkPath) && System.IO.File.Exists(album.ArtworkPath))
                                 ? album.ArtworkPath
                                 : _artworkService.GetDefaultArtworkPath();

            return await GetArtworkFile(artworkPath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting album artwork for ID {albumId}", ex);
            return await GetDefaultArtwork();
        }
    }

    private async Task<IActionResult> GetArtworkFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            _logger.LogWarning($"Artwork file not found or path invalid: {filePath}. Falling back to default.");
            filePath = _artworkService.GetDefaultArtworkPath(); // Fallback path

            // Check if even the default placeholder exists
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                 _logger.LogError($"Default artwork placeholder not found at: {filePath}");
                 return NotFound("Artwork not found, and default placeholder is missing.");
            }
        }

        try
        {
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string contentType = GetContentType(filePath);
            return File(fileStream, contentType); // Returns FileStreamResult
        }
        catch (FileNotFoundException)
        {
             _logger.LogError($"Artwork file specifically not found during stream creation: {filePath}");
             // This case might be redundant due to the initial check, but safety first
             return await GetDefaultArtwork();
        }
         catch (Exception ex)
        {
            _logger.LogError($"Error reading artwork file: {filePath}", ex);
            // Fallback to default on any read error
             return await GetDefaultArtwork();
        }
    }

     private async Task<IActionResult> GetDefaultArtwork()
    {
        string defaultPath = _artworkService.GetDefaultArtworkPath();
        if (string.IsNullOrEmpty(defaultPath) || !System.IO.File.Exists(defaultPath))
        {
            _logger.LogError($"Default artwork placeholder not found at: {defaultPath}");
            return NotFound("Default artwork is missing.");
        }
        try
        {
            var fileStream = new FileStream(defaultPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            string contentType = GetContentType(defaultPath);
            return File(fileStream, contentType);
        }
        catch(Exception ex)
        {
             _logger.LogError($"Error reading default artwork file: {defaultPath}", ex);
             return StatusCode(500, "Error serving default artwork.");
        }
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