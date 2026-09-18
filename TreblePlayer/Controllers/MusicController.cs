using Microsoft.AspNetCore.Mvc;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;
using TreblePlayer.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Xml.Serialization;

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
    private readonly IArtistNormalizationService? _normalizationService;
    private readonly MusicPlayerDbContext? _dbContext;

    public MusicController(
        MusicPlayer musicPlayer,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        ILoggingService logger,
        PlaybackWebSocketHandler webSocketHandler,
        IArtistNormalizationService? normalizationService = null,
        MusicPlayerDbContext? dbContext = null
        )
    {
        _player = musicPlayer;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
        _webSocketHandler = webSocketHandler;
        _normalizationService = normalizationService;
        _dbContext = dbContext;
    }

    [HttpPost("play/{trackId}")]
    public async Task<IActionResult> PlayAsync(int trackId)
    {
        await _player.PlayAsync(trackId);
        _webSocketHandler.BroadcastNotification("QueuesUpdated");
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
        _webSocketHandler.BroadcastNotification("QueuesUpdated");
        return Ok(new { message = $"Playing collection {collectionId} of type {type} at index {startIndex}" });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var isPlaying = _player.IsPlaying();
        return Ok(new { message = $"Music playing: {isPlaying}", volume = _player.Volume });
    }

    [HttpPost("volume/{volume:int}")]
    public IActionResult SetVolume(int volume)
    {
        _player.SetVolume(volume);
        return Ok(new { volume = _player.Volume });
    }

    [HttpGet("queues")]
    public async Task<IActionResult> GetAllQueues()
    {
        var activeQueue = await _player.GetActiveQueueAsync();

        if (_dbContext != null)
        {
            var queueRows = await _dbContext.TrackQueues
                .AsNoTracking()
                .Select(queue => new
                {
                    queue.Id,
                    queue.Title,
                    queue.LastPlayedTrackId,
                    TrackCount = queue.Tracks.Count(),
                    TotalDuration = queue.Tracks.Select(track => (int?)track.Duration).Sum() ?? 0
                })
                .ToListAsync();

            return Ok(queueRows.Select(queue => new QueueMetadataDto
            {
                Id = queue.Id,
                Title = queue.Title,
                TrackCount = queue.TrackCount,
                TotalDuration = queue.TotalDuration,
                IsActive = activeQueue?.Id == queue.Id,
                LastPlayedTrackId = queue.LastPlayedTrackId
            }).ToList());
        }

        var queues = await _collectionRepository.GetAllQueuesAsync();

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
            Tracks = GetOrderedTracks(queue).Select(track => ToTrackDto(track)).ToList()
        };
        return Ok(queueDto);
    }

    [HttpPost("queue/switch/{queueId}")]
    public async Task<IActionResult> SwitchToQueue(int queueId)
    {
        await _player.LoadQueueAndPlayAsync(queueId);
        return Ok(new { message = $"Switched to queue {queueId}" });
    }

    [HttpGet("queue/active")]
    public async Task<IActionResult> GetActiveQueue()
    {
        var queue = await _player.GetActiveQueueAsync();
        if (queue == null) return NotFound(new { message = "No active queue found." });

        var orderedTracks = GetOrderedTracks(queue);

        var queueDto = new QueueDto
        {
            Id = queue.Id,
            Title = queue.Title,
            CurrentTrackIndex = queue.CurrentTrackIndex ?? 0,
            LastPlaybackPositionSeconds = queue.LastPlaybackPositionSeconds,
            Tracks = orderedTracks.Select(track => ToTrackDto(track)).ToList()
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
    public async Task<IActionResult> EnableShuffle(bool enable)
    {
        await _player.EnableShuffleAsync(enable);
        return Ok(new { message = $"Shuffle {(enable ? "enabled" : "disabled")}" });
    }

    [HttpPost("loop/{enable}")]
    public async Task<IActionResult> EnableLoop(bool enable)
    {
        await _player.EnableLoopAsync(enable);
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
        return Ok(playlists.Select(ToPlaylistDto).ToList());
    }

    [HttpGet("playlist/{playlistId}")]
    public async Task<IActionResult> GetPlaylistById(int playlistId)
    {
        var playlist = await _collectionRepository.GetPlaylistByIdAsync(playlistId);
        if (playlist == null)
        {
            return NotFound(new { message = $"Playlist with ID {playlistId} not found." });
        }
        return Ok(ToPlaylistDto(playlist));
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

        var createdPlaylist = await _collectionRepository.GetPlaylistByIdAsync(newPlaylist.Id);
        var responsePayload = new
        {
            playlist = createdPlaylist == null ? null : ToPlaylistDto(createdPlaylist),
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

        var albumsForFrontend = albumsFromRepo
            .Where(album => album.Tracks != null && album.Tracks.Any())
            .OrderBy(album => NormalizeArtistForSort(album.AlbumArtist))
            .ThenBy(album => album.Title)
            .Select(album => ToAlbumDto(album))
            .ToList();

        return Ok(albumsForFrontend);
    }

    [HttpGet("album-summaries")]
    public async Task<IActionResult> GetAlbumSummaries()
    {
        if (_dbContext == null)
        {
            var albums = await _collectionRepository.GetAllAlbumSummariesAsync();
            return Ok(albums
                .Where(album => album.Tracks != null && album.Tracks.Any())
                .OrderBy(album => NormalizeArtistForSort(album.AlbumArtist))
                .ThenBy(album => album.Title)
                .Select(album => ToAlbumDto(album, includeTracks: false))
                .ToList());
        }

        var albumRows = await _dbContext.Albums
            .AsNoTracking()
            .Where(album => album.Tracks.Any())
            .Select(album => new
            {
                album.Id,
                album.Title,
                album.AlbumArtist,
                album.Year,
                album.LastModified,
                OriginalArtist = album.OriginalArtist ?? album.Tracks
                    .OrderBy(track => track.DiscNumber)
                    .ThenBy(track => track.TrackNumber)
                    .Select(track => track.Artist)
                    .FirstOrDefault(),
                TrackCount = album.Tracks.Count()
            })
            .ToListAsync();

        var summaries = albumRows
            .OrderBy(album => NormalizeArtistForSort(album.AlbumArtist))
            .ThenBy(album => album.Title)
            .Select(album => new AlbumDto
            {
                Id = album.Id,
                Title = album.Title,
                Artist = album.AlbumArtist ?? "Unknown Artist",
                OriginalArtist = album.OriginalArtist,
                ArtistSortKey = NormalizeArtistForSort(album.AlbumArtist),
                ArtworkUrl = GetAlbumArtworkUrl(album.Id, album.LastModified),
                Year = album.Year,
                LastModified = album.LastModified,
                TrackCount = album.TrackCount
            })
            .ToList();

        return Ok(summaries);
    }

    [HttpGet("album/{albumId}")]
    public async Task<IActionResult> GetAlbumById(int albumId)
    {
        var album = await _collectionRepository.GetAlbumByIdAsync(albumId);
        return album == null
            ? NotFound(new { message = $"Album with ID {albumId} not found." })
            : Ok(ToAlbumDto(album));
    }

    [HttpGet("artists")]
    public async Task<IActionResult> GetAllArtists()
    {
        if (_dbContext == null)
        {
            return Ok(new List<ArtistDto>());
        }

        // The artist page only needs album/track counts and album summaries.
        // Avoid loading every Track entity and serializing every track into
        // every artist response just to render the grid.
        var albumRows = await _dbContext.Albums
            .AsNoTracking()
            .Where(album => album.Tracks.Any())
            .Select(album => new
            {
                album.Id,
                album.Title,
                album.AlbumArtist,
                album.Year,
                album.LastModified,
                TrackCount = album.Tracks.Count()
            })
            .ToListAsync();

        var artists = albumRows
            .GroupBy(album => NormalizeArtistForSort(album.AlbumArtist))
            .Select(g => new ArtistDto
            {
                Name = g.Key,
                AlbumCount = g.Count(),
                TrackCount = g.Sum(album => album.TrackCount),
                Albums = g.Select(album => new AlbumDto
                {
                    Id = album.Id,
                    Title = album.Title,
                    Artist = album.AlbumArtist ?? "Unknown Artist",
                    ArtistSortKey = NormalizeArtistForSort(album.AlbumArtist),
                    ArtworkUrl = GetAlbumArtworkUrl(album.Id, album.LastModified),
                    Year = album.Year,
                    LastModified = album.LastModified,
                    TrackCount = album.TrackCount
                }).OrderBy(a => a.Title).ToList()
            })
            .OrderBy(a => a.Name)
            .ToList();

        return Ok(artists);
    }

    [HttpGet("artist-names")]
    public async Task<IActionResult> GetArtistNames()
    {
        var albums = await _collectionRepository.GetAllAlbumsAsync();
        if (albums == null) return Ok(Array.Empty<string>());

        var names = albums
            .SelectMany(album => new[] { album.AlbumArtist ?? string.Empty }
                .Concat(album.Tracks?.Select(track => track.Artist) ?? Enumerable.Empty<string>()))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(name => name)
            .ToList();

        return Ok(names);
    }

    [HttpPost("sorted/albums")]
    public async Task<IActionResult> GetSortedAlbumsWithSpecifications(
            [FromBody] List<SortSpecification> specs,
            [FromServices] IArtistNormalizationService normalizationService)
    {
        // fallback
        if (specs == null || specs.Count == 0)
        {
            specs = new List<SortSpecification> {
                new SortSpecification {Field = "ArtistAlias", Direction = SortDirection.Ascending}
            };
        }

        // fetch raw albums into memory
        var albums = await _collectionRepository.GetAllAlbumsAsync();
        if (albums == null) return Ok(new List<AlbumDto>());

        albums = albums
            .Where(album => album.Tracks != null && album.Tracks.Any())
            .ToList();

        // function selectors
        var mappings = new Dictionary<string, Func<Album, object>>(StringComparer.OrdinalIgnoreCase){
            {"ArtistAlias", a => normalizationService.NormalizeArtistName(a.AlbumArtist ?? string.Empty, out _, out _, out _)},
            {"ArtistRaw", a => a.AlbumArtist ?? string.Empty},
            {"ReleaseYear", a => a.Year ?? 0},
            {"Year", a => a.Year ?? 0},
            {"DateUpdated", a => a.LastModified},
            {"Random", a => Random.Shared.Next()},
            {"Title", a => a.Title ?? string.Empty}
        };

        IOrderedEnumerable<Album>? orderedResult = null;

        foreach (var spec in specs)
        {
            if (!mappings.TryGetValue(spec.Field, out var selector))
            {
                selector = a => a.Title ?? string.Empty;
            }

            if (orderedResult == null)
            {
                orderedResult = spec.Direction == SortDirection.Ascending
                    ? albums.OrderBy(selector)
                    : albums.OrderByDescending(selector);
            }
            else
            {
                orderedResult = spec.Direction == SortDirection.Ascending
                    ? orderedResult.ThenBy(selector)
                    : orderedResult.ThenByDescending(selector);
            }
        }

        var finalResult = orderedResult?.ToList() ?? albums.OrderBy(a => a.Title).ToList();
        return Ok(finalResult.Select(album => ToAlbumDto(album)).ToList());
    }

    [HttpGet("tracks")]
    public async Task<IActionResult> GetAllTracks()
    {
        var tracks = await _trackRepository.GetAllTracksAsync();
        return Ok((tracks ?? Enumerable.Empty<Track>()).Select(track => ToTrackDto(track)).ToList());
    }

    [HttpGet("tracks/page")]
    public async Task<IActionResult> GetTracksPage([FromQuery] int offset = 0, [FromQuery] int limit = 200)
    {
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 25, 500);

        if (_dbContext == null)
        {
            var allTracks = (await _trackRepository.GetAllTracksAsync())
                .OrderBy(track => track.TrackId)
                .ToList();

            return Ok(new TrackPageDto
            {
                Offset = offset,
                Limit = limit,
                TotalCount = allTracks.Count,
                Tracks = allTracks
                    .Skip(offset)
                    .Take(limit)
                    .Select(track => ToTrackDto(track))
                    .ToList()
            });
        }

        var totalCount = await _dbContext.Tracks.CountAsync();
        var pageRows = await _dbContext.Tracks
            .AsNoTracking()
            .OrderBy(track => track.TrackId)
            .Skip(offset)
            .Take(limit)
            .Select(track => new
            {
                track.TrackId,
                track.TrackNumber,
                track.DiscNumber,
                track.Title,
                track.Artist,
                track.AlbumTitle,
                track.Duration
            })
            .ToListAsync();

        return Ok(new TrackPageDto
        {
            Offset = offset,
            Limit = limit,
            TotalCount = totalCount,
            Tracks = pageRows.Select(track => new TrackDto
            {
                Id = track.TrackId,
                Number = track.TrackNumber,
                Disc = track.DiscNumber,
                Title = track.Title,
                Artist = track.Artist,
                AlbumTitle = track.AlbumTitle ?? string.Empty,
                Duration = track.Duration,
                ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/track/{track.TrackId}"
            }).ToList()
        });
    }

    private List<Track> GetOrderedTracks(TrackQueue queue)
    {
        var trackMap = queue.Tracks.ToDictionary(t => t.TrackId);
        return queue.GetPlaybackOrder()
            .Where(trackMap.ContainsKey)
            .Select(id => trackMap[id])
            .ToList();
    }

    private TrackDto ToTrackDto(Track track, string? artworkUrl = null)
    {
        return new TrackDto
        {
            Id = track.TrackId,
            Number = track.TrackNumber,
            Disc = track.DiscNumber,
            Title = track.Title,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle ?? string.Empty,
            Duration = track.Duration,
            ArtworkUrl = artworkUrl ?? $"{Request.Scheme}://{Request.Host}/api/Artwork/track/{track.TrackId}"
        };
    }

    private AlbumDto ToAlbumDto(Album album, bool includeTracks = true)
    {
        var artworkUrl = GetAlbumArtworkUrl(album.Id, album.LastModified);
        var originalArtist = album.OriginalArtist ?? album.Tracks?
            .OrderBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .Select(track => track.Artist)
            .FirstOrDefault(artist => !string.IsNullOrWhiteSpace(artist));

        return new AlbumDto
        {
            Id = album.Id,
            Title = album.Title,
            Artist = album.AlbumArtist ?? "Unknown Artist",
            OriginalArtist = originalArtist,
            ArtistSortKey = NormalizeArtistForSort(album.AlbumArtist),
            ArtworkUrl = artworkUrl,
            Year = album.Year,
            LastModified = album.LastModified,
            TrackCount = album.Tracks?.Count ?? 0,
            Tracks = includeTracks
                ? album.Tracks?
                    .OrderBy(t => t.DiscNumber)
                    .ThenBy(t => t.TrackNumber)
                    .Select(t => ToTrackDto(t, artworkUrl))
                    .ToList() ?? new List<TrackDto>()
                : new List<TrackDto>()
        };
    }

    private string GetAlbumArtworkUrl(int albumId, DateTime lastModified)
    {
        return $"{Request.Scheme}://{Request.Host}/api/Artwork/album/{albumId}?v={lastModified.Ticks}";
    }

    private string NormalizeArtistForSort(string? artist)
    {
        if (_normalizationService == null)
            return artist ?? "Unknown Artist";

        return _normalizationService.NormalizeArtistName(
            artist ?? string.Empty,
            out _,
            out _,
            out _);
    }

    private PlaylistDto ToPlaylistDto(Playlist playlist)
    {
        return new PlaylistDto
        {
            Id = playlist.Id,
            Title = playlist.Title,
            TrackCount = playlist.Tracks?.Count ?? 0,
            TotalDuration = playlist.Tracks?.Sum(t => t.Duration) ?? 0,
            ArtworkUrl = $"{Request.Scheme}://{Request.Host}/api/Artwork/playlist/{playlist.Id}",
            Tracks = playlist.Tracks?.Select(track => ToTrackDto(track)).ToList() ?? new List<TrackDto>()
        };
    }
}
