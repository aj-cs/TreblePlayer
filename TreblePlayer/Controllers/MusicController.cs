using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography.Xml;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using TreblePlayer.Services;
//using TreblePlayer.DTOs;
namespace TreblePlayer.Controllers;


[ApiController]
[Route("api/[controller]")]
public class MusicController : ControllerBase
{
    //private readonly AppDbContext _appDbContext;
    private readonly MusicPlayer _player;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly ILoggingService _logger;

    public MusicController(
        MusicPlayer musicPlayer,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        ILoggingService logger)
    {
        _player = musicPlayer;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
    }

    [HttpPost("play/{trackId}")]
    public async Task<IActionResult> PlayAsync(int trackId)
    {
        try
        {
            await _player.PlayAsync(trackId);
            return Ok(new { message = $"Playing track (ID: {trackId})" });
        }
        catch (Exception e)
        {
            _logger.LogError($"Error playing track {trackId}", e);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("resume")]
    public IActionResult Resume()
    {
        try
        {
            bool resumed = _player.Resume();
            if (resumed)
            {
                return Ok(new { message = "Playback resumed" });
            }
            else
            {
                return BadRequest(new { message = "No paused track to resume" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error resuming playback", ex);
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpPost("pause")]
    public IActionResult Pause()
    {
        try
        {
            bool paused = _player.Pause();
            if (paused)
            {
                return Ok(new { message = "Paused music." });
            }
            else
            {
                return BadRequest(new { message = "No playing track to pause." });
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Error pausing playback", e);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        try
        {
            bool stopped = _player.Stop();
            if (stopped)
            {
                return Ok(new { message = "Stopped music." });
            }
            else
            {
                return BadRequest(new { message = "No playing track to stop." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error stopping playback", ex);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("next")]
    public async Task<IActionResult> PlayNext()
    {
        try
        {
            await _player.NextAsync();
            return Ok(new { message = "Next track playing." });
        }
        catch (Exception e)
        {
            _logger.LogError("Error skipping to next track", e);
            return StatusCode(500, new { message = "An error occured." });
        }

    }

    [HttpPost("previous")]
    public async Task<IActionResult> PlayPrevious()
    {
        try
        {
            await _player.PreviousAsync();
            return Ok(new { message = "Previous track playing." });
        }
        catch (Exception e)
        {
            _logger.LogError("Error skipping to previous track", e);
            return StatusCode(500, new { message = "An error occured." });
        }
    }
    [HttpPost("seek/{seconds}")]
    public IActionResult Seek(float seconds)
    {
        try
        {
            _player.Seek(seconds);
            return Ok(new { message = $"Seeked to {seconds} seconds." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error seeking to {seconds} seconds", ex);
            return StatusCode(500, new { message = "Failed to seek." });
        }
    }
    [HttpPost("playCollection/{collectionId}/{type}")]
    public async Task<IActionResult> PlayCollection(int collectionId, TrackCollectionType type)
    {
        try
        {
            await _player.PlayCollectionAsync(collectionId, type);
            return Ok(new { message = $"Playing collection {collectionId} of type {type}" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error playing collection {collectionId}", ex);
            return StatusCode(500, new { message = $"Failed to play collection {collectionId} of type {type}" });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var isPlaying = _player.IsPlaying();
        return Ok(new { message = $"Music playing: {isPlaying}" });
    }

    [HttpPost("queue/create")]
    public async Task<IActionResult> CreateQueue(string title) //change later, 
    // queues should be created from track(s) aka IEnumerable/ICollection<Track> or an ITrackCollection
    {
        try
        {
            await _player.CreateQueueAsync(title);
            return Ok(new { message = $"Queue {title} created." });
        }

        catch (Exception e)
        {
            _logger.LogError("Error creating queue", e);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }

    }
    [HttpPost("queue/{queueId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToQueue(int queueId, int trackId)
    {
        try
        {
            await _player.AddTrackToQueueAsync(queueId, trackId);
            return Ok(new { message = $"Track (ID: {trackId} added to Queue (ID: {queueId})" });
        }
        catch (Exception e)
        {
            _logger.LogError("Error adding track to queue", e);
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("shuffle/{enable}")]
    public IActionResult EnableShuffle(bool enable)
    {
        _player.EnableShuffle(enable);
        return Ok(new { message = $"Shuffle {(enable ? "enabled" : "disabled")}" });
    }

    [HttpPost("loop/{enable}")]
    public IActionResult EnableLoop(bool enable)
    {
        _player.EnableLoop(enable);
        return Ok(new { message = $"Loop {(enable ? "enabled" : "disabled")}" });
    }

    [HttpPost("loop/set/{mode}")]
    public async Task<IActionResult> SetLoopMode(int mode)
    {
        try
        {
            await _player.SetLoopModeAsync((LoopTrack)mode);
            return Ok(new { message = $"Loop mode set to {mode}" });
        }
        catch (Exception e)
        {
            _logger.LogError("Error setting loop mode", e);
            return StatusCode(500, new { message = "Failed to set loop mode." });
        }
    }

    [HttpPost("loop/toggle")]
    public async Task<IActionResult> ToggleLoopMode()
    {
        try
        {
            var newMode = await _player.ToggleLoopModeAsync();
            return Ok(new { message = $"Loop mode toggled to {newMode}" });
        }
        catch (Exception e)
        {
            _logger.LogError("Error toggling loop mode", e);
            return StatusCode(500, new { message = "Failed to toggle loop mode." });
        }
    }

    [HttpGet("playlists")]
    public async Task<IActionResult> GetAllPlaylists()
    {
        try
        {
            var playlists = await _collectionRepository.GetAllPlaylistsAsync();
            // Consider mapping to DTOs to avoid exposing full Track objects if not needed
            return Ok(playlists);
        }
        catch (Exception ex)
        {
             _logger.LogError("Error getting all playlists", ex);
            return StatusCode(500, new { message = "Failed to retrieve playlists." });
        }
    }

    [HttpGet("playlist/{playlistId}")]
     public async Task<IActionResult> GetPlaylistById(int playlistId)
    {
        try
        {
            var playlist = await _collectionRepository.GetPlaylistByIdAsync(playlistId);
            if (playlist == null)
            {
                return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
            }
             // Consider mapping to DTOs
            return Ok(playlist);
        }
        catch (Exception ex)
        {
             _logger.LogError($"Error getting playlist {playlistId}", ex);
            return StatusCode(500, new { message = "Failed to retrieve playlist details." });
        }
    }


    [HttpPost("playlist/create")]
    public async Task<IActionResult> CreatePlaylist([FromBody] PlaylistCreateModel model) // Use a DTO for input
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Title))
        {
            return BadRequest(new { message = "Playlist title cannot be empty." });
        }
        try
        {
            var newPlaylist = new Playlist { Title = model.Title }; // Map from DTO
            await _collectionRepository.AddPlaylistAsync(newPlaylist);
            // Return the created playlist, potentially mapped to a DTO
            return CreatedAtAction(nameof(GetPlaylistById), new { playlistId = newPlaylist.Id }, newPlaylist);
        }
        catch (Exception ex)
        {
             _logger.LogError($"Error creating playlist with title '{model.Title}'", ex);
            return StatusCode(500, new { message = "Failed to create playlist." });
        }
    }

     [HttpDelete("playlist/{playlistId}")]
    public async Task<IActionResult> DeletePlaylist(int playlistId)
    {
        try
        {
            await _collectionRepository.RemovePlaylistAsync(playlistId);
            return NoContent(); // Standard response for successful deletion
        }
         catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting playlist {playlistId}", ex);
            return StatusCode(500, new { message = "Failed to delete playlist." });
        }
    }

     [HttpPost("playlist/{playlistId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToPlaylist(int playlistId, int trackId)
    {
        try
        {
            await _collectionRepository.AddTrackToPlaylistAsync(playlistId, trackId);
            return Ok(new { message = $"Track {trackId} added to playlist {playlistId}." });
        }
        catch (KeyNotFoundException ex)
        {
            // Check if playlist or track was not found
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
             _logger.LogError($"Error adding track {trackId} to playlist {playlistId}", ex);
            return StatusCode(500, new { message = "Failed to add track to playlist." });
        }
    }

     [HttpDelete("playlist/{playlistId}/removeTrack/{trackId}")]
    public async Task<IActionResult> RemoveTrackFromPlaylist(int playlistId, int trackId)
    {
        try
        {
            await _collectionRepository.RemoveTrackFromPlaylistAsync(playlistId, trackId);
            return Ok(new { message = $"Track {trackId} removed from playlist {playlistId}." });
        }
        catch (KeyNotFoundException ex)
        {
             // Handle case where playlist doesn't exist (repo throws)
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing track {trackId} from playlist {playlistId}", ex);
            return StatusCode(500, new { message = "Failed to remove track from playlist." });
        }
    }

    // --- Placeholder for DTO ---
    // You should create this in a DTOs folder or similar
    public class PlaylistCreateModel
    {
        public string Title { get; set; } = string.Empty;
        // Add other properties if needed for playlist creation
    }
}
