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

using System.Collections.Generic;
using System.Threading.Tasks;


[ApiController]
[Route("api/[controller]")]
public class MusicController : ControllerBase
{
    //private readonly AppDbContext _appDbContext;
    private readonly MusicPlayer _player;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly ILoggingService _logger;
    private readonly IHubContext<DataHub> _dataHubContext;

    public MusicController(
        MusicPlayer musicPlayer,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        ILoggingService logger,
        IHubContext<DataHub> dataHubContext)
    {
        _player = musicPlayer;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
        _dataHubContext = dataHubContext;
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
    public async Task<IActionResult> CreatePlaylist([FromBody] PlaylistCreateWithItemsModel model) // Use new DTO
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Title))
        {
            return BadRequest(new { message = "Playlist title cannot be empty." });
        }
        // Basic validation for TrackIds if needed
        // if (model.TrackIds == null || !model.TrackIds.Any())
        // {
        //     return BadRequest(new { message = "Playlist must contain at least one track." });
        // }

        try
        {
            // 1. Create the playlist entry
            var newPlaylist = new Playlist { Title = model.Title };
            await _collectionRepository.AddPlaylistAsync(newPlaylist);

            // 2. Add tracks to the newly created playlist
            // Need to handle potential errors during track addition
            List<string> trackAddErrors = new List<string>();
            if (model.TrackIds != null && model.TrackIds.Any())
            {
                foreach (var trackId in model.TrackIds)
                {
                    try
                    {
                        await _collectionRepository.AddTrackToPlaylistAsync(newPlaylist.Id, trackId);
                    }
                    catch (KeyNotFoundException ex) // Catch if specific track not found
                    {
                        _logger.LogWarning($"Error adding track {trackId} to playlist {newPlaylist.Id}: {ex.Message}");
                        trackAddErrors.Add($"Track ID {trackId} not found or could not be added.");
                    }
                    catch (Exception ex) // Catch other potential errors
                    {
                        _logger.LogError($"Error adding track {trackId} to playlist {newPlaylist.Id}", ex);
                        trackAddErrors.Add($"Failed to add track ID {trackId}.");
                    }
                }
            }

            // Return the created playlist (even if some tracks failed to add)
            // Include track addition errors in the response if any occurred
            var responsePayload = new
            {
                playlist = newPlaylist, // Consider mapping to a DTO
                trackAdditionErrors = trackAddErrors.Any() ? trackAddErrors : null
            };

            // <<< Send notification after successful creation >>>
            await _dataHubContext.Clients.All.SendAsync("PlaylistsUpdated");

            return CreatedAtAction(nameof(GetPlaylistById), new { playlistId = newPlaylist.Id }, responsePayload);
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
            
            // <<< Send notification after successful deletion >>>
            await _dataHubContext.Clients.All.SendAsync("PlaylistsUpdated");
            
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

            // <<< Send notification after successful add >>>
            await _dataHubContext.Clients.All.SendAsync("PlaylistsUpdated"); // Or maybe a more specific "PlaylistContentUpdated", playlistId ?

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

            // <<< Send notification after successful removal >>>
            await _dataHubContext.Clients.All.SendAsync("PlaylistsUpdated"); // Or maybe a more specific "PlaylistContentUpdated", playlistId ?

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
    // --- Add Methods for Getting Library Data ---

    [HttpGet("albums")]
    public async Task<IActionResult> GetAllAlbums()
    {
        try
        {
            var albumsFromRepo = await _collectionRepository.GetAllAlbumsAsync();
            if (albumsFromRepo == null)
            {
                return Ok(new List<object>());
            }

            var albumsForFrontend = new List<AlbumDto>(); // Initialize list to store results

            foreach (var album in albumsFromRepo)
            {
                // Construct artwork URL using the actual AlbumId
                string artworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{album.Id}";

                // Fetch tracks associated with this album asynchronously
                var tracksForAlbum = await _trackRepository.GetTracksByAlbumIdAsync(album.Id);

                // Calculate track count safely
                int trackCount = tracksForAlbum?.Count() ?? 0;

                // Correctly handle potential null tracksForAlbum before mapping
                var tracksDto = new List<TrackDto>(); // Default to empty list
                if (tracksForAlbum != null)
                {
                    tracksDto = tracksForAlbum
                       .Select(t => new TrackDto
                       {
                           Id = t.TrackId,
                           Number = t.TrackNumber,
                           Title = t.Title,
                           Duration = t.Duration
                       })
                       .OrderBy(t => t.Number)
                       .ToList();
                }

                // Add the mapped album to the results list
                albumsForFrontend.Add(new AlbumDto
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = album.AlbumArtist,
                    ArtworkUrl = artworkUrl,
                    TrackCount = trackCount,
                    Tracks = tracksDto // Assign the correctly typed list
                });
            }

            // Sort the final list
            var sortedAlbums = albumsForFrontend
                 .OrderBy(a => a.Artist) // Sort by artist
                 .ThenBy(a => a.Title) // Then by title
                 .ToList();

            return Ok(sortedAlbums);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting all albums", ex);
            return StatusCode(500, "An error occurred while retrieving albums.");
        }
    }


    [HttpGet("tracks")]
    public async Task<IActionResult> GetAllTracks()
    {
        try
        {
            var tracks = await _trackRepository.GetAllTracksAsync();
            return Ok(tracks ?? new List<Track>());
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting all tracks", ex);
            return StatusCode(500, "An error occurred while retrieving tracks.");
        }
    }

} // End of MusicController Class

// --- DTOs for Playlist Creation ---
// Define DTO used in CreatePlaylist above
// Consider moving this to a separate DTOs folder/namespace
public class PlaylistCreateWithItemsModel
{
    public string Title { get; set; } = string.Empty;
    public List<int> TrackIds { get; set; } = new List<int>();
}

// Keep original model if used elsewhere, or remove if only the new one is needed by API
// public class PlaylistCreateModel
// {
//    public string Title { get; set; } = string.Empty;
// }

// Ensure your Track model (in TreblePlayer.Models) has properties like:
// int TrackId { get; set; }
// string Title { get; set; }
// string Artist { get; set; }
// string AlbumTitle { get; set; }
// int? TrackNumber { get; set; } // Added assumption
// string DurationString { get; set; } // Added assumption for formatted duration

// Your Playlist model should have at least:
// int Id { get; set; }
// string Title { get; set; }
// ICollection<Track> Tracks { get; set; } // Or similar relationship

public class AlbumDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class TrackDto
{
    public int Id { get; set; }
    public int? Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Duration { get; set; }
}
