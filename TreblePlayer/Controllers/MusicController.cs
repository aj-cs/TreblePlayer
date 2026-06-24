using Microsoft.AspNetCore.Mvc;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;
using TreblePlayer.DTOs;

namespace TreblePlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MusicController : ControllerBase
{
    private readonly MusicPlayer _player;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly ILoggingService _logger;
    private readonly PlaybackWebSocketHandler _webSocketHandler;

    public MusicController(
        MusicPlayer musicPlayer,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        ILoggingService logger,
        PlaybackWebSocketHandler webSocketHandler)
    {
        _player = musicPlayer;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
        _webSocketHandler = webSocketHandler;
    }

    [HttpPost("play/{trackId}")]
    public async Task<IActionResult> PlayAsync(int trackId)
    {
        await _player.PlayAsync(trackId);
        return Ok(new { message = $"Playing track (ID: {trackId})" });
    }

    [HttpPost("resume")]
    public IActionResult Resume()
    {
        bool resumed = _player.Resume();
        if (resumed)
        {
            return Ok(new { message = "Playback resumed" });
        }
        return BadRequest(new { message = "No paused track to resume" });
    }

    [HttpPost("pause")]
    public IActionResult Pause()
    {
        bool paused = _player.Pause();
        if (paused)
        {
            return Ok(new { message = "Paused music." });
        }
        return BadRequest(new { message = "No playing track to pause." });
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        bool stopped = _player.Stop();
        if (stopped)
        {
            return Ok(new { message = "Stopped music." });
        }
        return BadRequest(new { message = "No playing track to stop." });
    }

    [HttpPost("next")]
    public async Task<IActionResult> PlayNext()
    {
        await _player.NextAsync();
        return Ok(new { message = "Next track playing." });
    }

    [HttpPost("previous")]
    public async Task<IActionResult> PlayPrevious()
    {
        await _player.PreviousAsync();
        return Ok(new { message = "Previous track playing." });
    }

    [HttpPost("seek/{seconds}")]
    public IActionResult Seek(float seconds)
    {
        _player.Seek(seconds);
        return Ok(new { message = $"Seeked to {seconds} seconds." });
    }

    [HttpPost("playCollection/{collectionId}/{type}/{startIndex}")]
    public async Task<IActionResult> PlayCollection(int collectionId, TrackCollectionType type, int startIndex = 0)
    {
        await _player.PlayCollectionAsync(collectionId, type, startIndex);
        return Ok(new { message = $"Playing collection {collectionId} of type {type} at index {startIndex}" });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var isPlaying = _player.IsPlaying();
        return Ok(new { message = $"Music playing: {isPlaying}" });
    }

    [HttpGet("queues")]
    public async Task<IActionResult> GetAllQueues()
    {
        var queues = await _collectionRepository.GetAllQueuesAsync();
        Console.WriteLine($"GetAllQueues returned {queues.Count} queues.");
        foreach (var q in queues) Console.WriteLine($"Queue: {q.Title}, TrackCount: {q.Tracks?.Count ?? 0}");

        var activeQueue = await _player.GetActiveQueueAsync();

        var metadata = queues.Select(q => new QueueMetadataDto
        {
            Id = q.Id,
            Title = q.Title,
            TrackCount = q.Tracks?.Count ?? 0,
            TotalDuration = q.Tracks?.Sum(t => t.Duration) ?? 0,
            IsActive = activeQueue?.Id == q.Id,
            LastPlayedTrackId = q.LastPlayedTrackId
        }).ToList();

        return Ok(metadata);
    }

    [HttpGet("queue/{queueId}")]
    public async Task<IActionResult> GetQueueById(int queueId)
    {
        var queue = await _collectionRepository.GetQueueByIdAsync(queueId);
        if (queue == null) return NotFound();

        var queueDto = new QueueDto
        {
            Id = queue.Id,
            Title = queue.Title,
            CurrentTrackIndex = queue.CurrentTrackIndex ?? 0,
            LastPlaybackPositionSeconds = queue.LastPlaybackPositionSeconds,
            LastPlayedTrackId = queue.LastPlayedTrackId,
            Tracks = queue.Tracks.Select(t => new TrackDto
            {
                Id = t.TrackId,
                Number = t.TrackNumber,
                Disc = t.DiscNumber,
                Title = t.Title,
                Artist = t.Artist,
                AlbumTitle = t.AlbumTitle ?? string.Empty,
                Duration = t.Duration,
                ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/track/{t.TrackId}"
            }).ToList()
        };
        return Ok(queueDto);
    }

    [HttpPost("queue/switch/{queueId}")]
    public async Task<IActionResult> SwitchToQueue(int queueId)
    {
        await _player.LoadQueueAndPlayAsync(queueId, 0);
        return Ok(new { message = $"Switched to queue {queueId}" });
    }

    [HttpGet("queue/active")]
    public async Task<IActionResult> GetActiveQueue()
    {
        var queue = await _player.GetActiveQueueAsync();
        if (queue == null) return NotFound(new { message = "No active queue found." });

        var orderedTracks = queue.IsShuffleEnabled
            ? queue.GetShuffledOrder().Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id)).Where(t => t != null).Cast<Track>().ToList()
            : queue.Tracks.OrderBy(t => t.TrackNumber).ToList();

        var queueDto = new QueueDto
        {
            Id = queue.Id,
            Title = queue.Title,
            CurrentTrackIndex = queue.CurrentTrackIndex ?? 0,
            LastPlaybackPositionSeconds = queue.LastPlaybackPositionSeconds,
            Tracks = orderedTracks.Select(t => new TrackDto
            {
                Id = t.TrackId,
                Number = t.TrackNumber,
                Disc = t.DiscNumber,
                Title = t.Title,
                Artist = t.Artist,
                AlbumTitle = t.AlbumTitle ?? string.Empty,
                Duration = t.Duration,
                ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/track/{t.TrackId}"
            }).ToList()
        };
        return Ok(queueDto);
    }

    [HttpPost("queue/create")]
    public async Task<IActionResult> CreateQueue(string title)
    {
        await _player.CreateQueueAsync(title);
        return Ok(new { message = $"Queue {title} created." });
    }

    [HttpDelete("queue/{queueId}")]
    public async Task<IActionResult> DeleteQueue(int queueId)
    {
        try
        {
            await _player.DeleteQueueAsync(queueId);
            _webSocketHandler.BroadcastNotification("QueuesUpdated");
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queue/{queueId}/reorder")]
    public async Task<IActionResult> ReorderQueue(int queueId, [FromBody] List<int> trackIds)
    {
        try
        {
            await _player.ReorderQueueAsync(queueId, trackIds);
            _webSocketHandler.BroadcastNotification("QueueUpdated", new { queueId });
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("queue/{queueId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToQueue(int queueId, int trackId)
    {
        await _player.AddTrackToQueueAsync(queueId, trackId);
        return Ok(new { message = $"Track (ID: {trackId} added to Queue (ID: {queueId})" });
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
        await _player.SetLoopModeAsync((LoopTrack)mode);
        return Ok(new { message = $"Loop mode set to {mode}" });
    }

    [HttpPost("loop/toggle")]
    public async Task<IActionResult> ToggleLoopMode()
    {
        var newMode = await _player.ToggleLoopModeAsync();
        return Ok(new { message = $"Loop mode toggled to {newMode}" });
    }

    [HttpGet("playlists")]
    public async Task<IActionResult> GetAllPlaylists()
    {
        var playlists = await _collectionRepository.GetAllPlaylistsAsync();
        return Ok(playlists);
    }

    [HttpGet("playlist/{playlistId}")]
    public async Task<IActionResult> GetPlaylistById(int playlistId)
    {
        var playlist = await _collectionRepository.GetPlaylistByIdAsync(playlistId);
        if (playlist == null)
        {
            return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
        }
        return Ok(playlist);
    }

    [HttpPost("playlist/create")]
    public async Task<IActionResult> CreatePlaylist([FromBody] PlaylistCreateWithItemsModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Title))
        {
            return BadRequest(new { message = "Playlist title cannot be empty." });
        }

        var newPlaylist = new Playlist { Title = model.Title };
        await _collectionRepository.AddPlaylistAsync(newPlaylist);

        List<string> trackAddErrors = new List<string>();
        if (model.TrackIds != null && model.TrackIds.Any())
        {
            foreach (var trackId in model.TrackIds)
            {
                try
                {
                    await _collectionRepository.AddTrackToPlaylistAsync(newPlaylist.Id, trackId);
                }
                catch (KeyNotFoundException ex)
                {
                    _logger.LogWarning($"Error adding track {trackId} to playlist {newPlaylist.Id}: {ex.Message}");
                    trackAddErrors.Add($"Track ID {trackId} not found or could not be added.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error adding track {trackId} to playlist {newPlaylist.Id}", ex);
                    trackAddErrors.Add($"Failed to add track ID {trackId}.");
                }
            }
        }

        var responsePayload = new
        {
            playlist = newPlaylist,
            trackAdditionErrors = trackAddErrors.Any() ? trackAddErrors : null
        };

        _webSocketHandler.BroadcastNotification("PlaylistsUpdated");
        return CreatedAtAction(nameof(GetPlaylistById), new { playlistId = newPlaylist.Id }, responsePayload);
    }

    [HttpDelete("playlist/{playlistId}")]
    public async Task<IActionResult> DeletePlaylist(int playlistId)
    {
        try
        {
            await _collectionRepository.RemovePlaylistAsync(playlistId);
            _webSocketHandler.BroadcastNotification("PlaylistsUpdated");
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
        }
    }

    [HttpPost("playlist/{playlistId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToPlaylist(int playlistId, int trackId)
    {
        try
        {
            await _collectionRepository.AddTrackToPlaylistAsync(playlistId, trackId);
            _webSocketHandler.BroadcastNotification("PlaylistsUpdated");
            return Ok(new { message = $"Track {trackId} added to playlist {playlistId}." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("playlist/{playlistId}/removeTrack/{trackId}")]
    public async Task<IActionResult> RemoveTrackFromPlaylist(int playlistId, int trackId)
    {
        try
        {
            await _collectionRepository.RemoveTrackFromPlaylistAsync(playlistId, trackId);
            _webSocketHandler.BroadcastNotification("PlaylistsUpdated");
            return Ok(new { message = $"Track {trackId} removed from playlist {playlistId}." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("albums")]
    public async Task<IActionResult> GetAllAlbums()
    {
        var albumsFromRepo = await _collectionRepository.GetAllAlbumsAsync();
        if (albumsFromRepo == null)
        {
            return Ok(new List<AlbumDto>());
        }

        var albumsForFrontend = albumsFromRepo.Select(album => new AlbumDto
        {
            Id = album.Id,
            Title = album.Title,
            Artist = album.AlbumArtist ?? "Unknown Artist",
            ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{album.Id}",
            TrackCount = album.Tracks?.Count ?? 0,
            Tracks = album.Tracks?.Select(t => new TrackDto
            {
                Id = t.TrackId,
                Number = t.TrackNumber,
                Disc = t.DiscNumber,
                Title = t.Title,
                Artist = t.Artist,
                AlbumTitle = album.Title,
                Duration = t.Duration,
                ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{album.Id}"
            }).OrderBy(t => t.Disc).ThenBy(t => t.Number).ToList() ?? new List<TrackDto>()
        }).OrderBy(a => a.Artist).ThenBy(a => a.Title).ToList();

        return Ok(albumsForFrontend);
    }

    [HttpGet("artists")]
    public async Task<IActionResult> GetAllArtists()
    {
        var albumsFromRepo = await _collectionRepository.GetAllAlbumsAsync();
        if (albumsFromRepo == null)
        {
            return Ok(new List<ArtistDto>());
        }

        var artists = albumsFromRepo
            .GroupBy(a => a.AlbumArtist ?? "Unknown Artist")
            .Select(g => new ArtistDto
            {
                Name = g.Key,
                AlbumCount = g.Count(),
                TrackCount = g.Sum(a => a.Tracks?.Count ?? 0),
                Albums = g.Select(album => new AlbumDto
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = album.AlbumArtist ?? "Unknown Artist",
                    ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{album.Id}",
                    TrackCount = album.Tracks?.Count ?? 0,
                    Tracks = album.Tracks?.Select(t => new TrackDto
                    {
                        Id = t.TrackId,
                        Number = t.TrackNumber,
                        Disc = t.DiscNumber,
                        Title = t.Title,
                        Artist = t.Artist,
                        AlbumTitle = album.Title,
                        Duration = t.Duration,
                        ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{album.Id}"
                    }).OrderBy(t => t.Disc).ThenBy(t => t.Number).ToList() ?? new List<TrackDto>()
                }).OrderBy(a => a.Title).ToList()
            })
            .OrderBy(a => a.Name)
            .ToList();

        return Ok(artists);
    }
    [HttpPost("tracks/sorted")]
    public async Task<IActionResult> GetSortedTracksWithSpecification([FromBody] List<SortSpecification> specs)
    {
        if (specs == null || specs.Count == 0)
        {
            specs = new List<SortSpecification> {
                new SortSpecification { Field = "artist", Direction = SortDirection.Ascending}
            };
        }

        var tracks = await _trackRepository.GetAllTracksAsync();
        var query = tracks.AsQueryable();

        var sortedQuery = QueryBuilder.ApplySort
    }




    [HttpGet("tracks")]
    public async Task<IActionResult> GetAllTracks()
    {
        var tracks = await _trackRepository.GetAllTracksAsync();
        return Ok(tracks ?? new List<Track>());
    }
}
