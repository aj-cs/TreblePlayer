using TreblePlayer.Data;
using TreblePlayer.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using SoundFlow.Abstracts;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Providers;
using SoundFlow.Enums;
using TreblePlayer.Services;

namespace TreblePlayer.Core;

public class MusicPlayer : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlaybackHub> _hubContext;
    private readonly ILoggingService _logger;
    private CancellationTokenSource? _cts;

    private readonly MiniAudioEngine _engine;
    private SoundPlayer? _player;

    private TrackIterator? _iterator;

    private readonly object _lock = new();
    private bool _isPlaying;
    public bool ShuffleEnabled = false;
    public bool AutoAdvanceEnabled { get; set; } = true;

    public MusicPlayer(IServiceScopeFactory scopeFactory, IHubContext<PlaybackHub> hubContext, ILoggingService logger)
    {
        _engine = new MiniAudioEngine(44100, Capability.Playback);
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Plays the specified track. If a track is already playing or paused, then:
    /// - If the same track is requested: resume if paused or ignore if playing.
    /// - If a different track is requested: stop the current track and override with the new one.
    /// </summary>
    public async Task InternalPlayAsync(Track? track, float? seekSeconds = null)
    {
        if (track == null)
        {
            return;
        }

        // check if a player already exists for the current track.
        // if playing, ignore duplicate play command; if paused then resume.
        lock (_lock)
        {
            if (_player != null && _iterator != null && _iterator.Current != null)
            {
                if (_iterator.Current.TrackId == track.TrackId)
                {
                    if (_player.State == PlaybackState.Playing)
                    {
                        _logger.LogInformation("MusicPlayer: Already playing the current track.");
                        return;
                    }
                    else if (_player.State == PlaybackState.Paused)
                    {
                        _logger.LogInformation("MusicPlayer: Resuming paused track.");
                        _player.Play();
                        _isPlaying = true;
                        _hubContext.Clients.All.SendAsync("PlaybackResume");
                        return;
                    }
                }
                else
                {
                    _logger.LogInformation("MusicPlayer: Overriding current track with new track.");
                }
            }
            // Stop any existing playback before starting new playback.
            Stop();
            _cts = new CancellationTokenSource();
        }

        var stream = File.OpenRead(track.FilePath); // TODO: switch to FileService later
        var provider = new StreamDataProvider(stream);
        if (AutoAdvanceEnabled)
        {
            provider.EndOfStreamReached += async (sender, args) =>
            {
                _logger.LogInformation("End of track reached. Auto advancing.");
                await NextAsync();
            };
        }
        var newPlayer = new SoundPlayer(provider);

        lock (_lock)
        {
            _player = newPlayer;
            Mixer.Master.AddComponent(_player);
            _player.Play();
            _isPlaying = true;
        }

        if (seekSeconds.HasValue)
        {
            _player.Seek(seekSeconds.Value);
            _logger.LogInformation($"Resumed at {seekSeconds.Value} seconds");
        }

        await _hubContext.Clients.All.SendAsync("PlaybackStarted", track.TrackId);
        _logger.LogInformation($"Playing: {track.Title}, ID: {track.TrackId}");

        // NOTE: The background monitoring loop has been removed.
        // If you need to auto-advance to the next track when one finishes,
        // you'll need to implement that using an event or a more robust check.
    }


    public async Task PlayAsync(int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var trackRepo = scope.ServiceProvider.GetRequiredService<ITrackRepository>();
        var track = await trackRepo.GetTrackByIdAsync(trackId);

        if (track == null)
        {
            throw new Exception("Track not found");
        }
        var queueId = await CreateNowPlayingQueueAsync(new List<Track> { track }, $"Now playing track: {track.Title}");

        await LoadQueueAndPlayAsync(queueId);

    }
    public async Task PlayCollectionAsync(int collectionId, TrackCollectionType type, int startIndex = 0)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var collection = await collectionRepo.GetTrackCollectionByIdAsync(collectionId, type);
        if (collection == null || collection.Tracks.Count == 0)
        {
            _logger.LogWarning("No tracks in the collection to play.");
            return;
        }

        if (startIndex < 0 || startIndex >= collection.Tracks.Count)
        {
            _logger.LogWarning("Invalid start index.");
            return;
        }
        //skip to the start index
        var tracks = collection.Tracks.ToList();

        if (ShuffleEnabled)
        {
            tracks = tracks.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        var queueId = await CreateNowPlayingQueueAsync(tracks, $"Now playing: {collection.Title}"); // , type: {type}, typeID: {collectionId}
        await LoadQueueAndPlayAsync(queueId, startIndex);
    }
    /// <summary>
    /// pauses playback if a track is playing
    /// retunrs true if pause succeeded, false if nothing was playing
    /// </summary>

    public bool Pause()
    {
        lock (_lock)
        {
            if (_player == null || _player.State != PlaybackState.Playing)
            {
                _logger.LogWarning("MusicPlayer: Cannot pause, no track is playing");
                return false;
            }
            _player?.Pause();
            _isPlaying = false;
            _ = SaveActiveQueueStateAsync(); // persist s the paused pos
            _hubContext.Clients.All.SendAsync("PlaybackPaused");
            _logger.LogInformation("Paused");
            return true;
        }
    }
    /// <summary>
    /// stops playback if a track is playing
    /// retunrs true if stop succeeded, false if nothing was playing
    /// </summary>
    public bool Stop()
    {
        lock (_lock)
        {
            if (_player == null)
            {
                _logger.LogWarning("MusicPlayer: No active track to stop");
                return false;
            }
            _cts?.Cancel();
            _ = SaveActiveQueueStateAsync(); // persist the queue state before stopping

            try
            {
                _player?.Stop();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Warning: _player.Stop() failed - {ex.Message}");
            }
            _isPlaying = false;


            if (_player != null)
            {
                Mixer.Master.RemoveComponent(_player);
                _player = null;
            }

            _hubContext.Clients.All.SendAsync("PlaybackStopped");
            _logger.LogInformation("Stopped");
            return true;
        }
    }
    /// <summary>
    /// resumes playback if a track is paused
    /// retunrs true if resume succeeded, false if no track is paused
    /// </summary>
    public bool Resume()
    {
        lock (_lock)
        {
            if (_player == null || _player.State != PlaybackState.Paused)
            {
                _logger.LogWarning("MusicPlayer: Cannot resume, no track is paused.");
                return false;
            }

            _player.Play();
            _isPlaying = true;
            _hubContext.Clients.All.SendAsync("PlaybackResume");
            _logger.LogInformation("Resumed");
            return true;
        }
    }

    public void Seek(float seconds)
    {
        lock (_lock)
        {
            _player?.Seek(seconds);
            _hubContext.Clients.All.SendAsync("PlaybackSeeked", seconds);
            _logger.LogInformation($"Seeked to {seconds} seconds");
        }
    }

    public bool IsPlaying()
    {
        return _isPlaying && _player?.State == PlaybackState.Playing;

    }

    public async Task NextAsync()
    {
        if (_iterator?.HasNext == true)
        { //NOTE: check if ? is fine here
            await InternalPlayAsync(_iterator.Next());
        }
        else
        {
            // if loop is enabled

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
            if (queue?.IsLoopEnabled == true)
            {
                _iterator?.Reset();
                await InternalPlayAsync(_iterator?.Current);
            }
        }
    }
    public async Task PreviousAsync()
    {
        if (_iterator?.HasPrevious == true)
        {
            await InternalPlayAsync(_iterator.Previous());
        }
    }

    private void CleanUpPlayer()
    {
        if (_player != null)
        {
            Mixer.Master.RemoveComponent(_player);
            _player = null;
        }
        _cts?.Dispose();
        _cts = null;
    }

    public async Task CreateQueueAsync(string title)
    {
        var newQueue = new TrackQueue { Title = title };

        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        await collectionRepo.AddQueueAsync(newQueue);
    }

    public async Task AddTrackToQueueAsync(int queueId, int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var trackRepo = scope.ServiceProvider.GetRequiredService<ITrackRepository>();


        var track = await trackRepo.GetTrackByIdAsync(trackId);
        var queue = await collectionRepo.GetQueueByIdAsync(queueId);


        if (track == null || queue == null)
        {
            throw new ArgumentException("Track or Queue not found");
        }

        queue.AddTrack(track);
        await collectionRepo.SaveAsync(queue);

    }

    public async Task PlayQueueAsync(int queueId)
    {
        await LoadQueueAndPlayAsync(queueId);
    }

    public async Task<List<TrackQueue>> GetAllQueuesAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        return await collectionRepo.GetAllQueuesAsync();
    }

    public async Task<List<Track>> GetTracksFromQueueAsync(int queueId)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = await collectionRepo.GetQueueByIdAsync(queueId);
        return queue?.Tracks.ToList() ?? new List<Track>();
    }

    public async Task RemoveTrackFromQueueAsync(int queueId, int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        try
        {
            await collectionRepo.RemoveTrackFromQueueAsync(queueId, trackId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to remove track from queue: {ex.Message}");
        }
    }

    public async Task ClearQueueAsync(int queueId)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        await collectionRepo.ClearQueueAsync(queueId);
    }

    public async Task ShuffleQueueAsync(int queueId)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = await collectionRepo.GetQueueByIdAsync(queueId);

        if (queue == null || queue.Tracks.Count == 0)
        {
            throw new ArgumentException($"Queue: {queueId} not found or is empty.");
        }
        _iterator = new TrackIterator(queue.Tracks.ToList());
        _iterator.Shuffle();
        _logger.LogInformation($"Shuffled queue {queueId}");

    }

    public async Task LoadQueueAndPlayAsync(int queueId, int startIndex = 0)
    {
        using var scope = _scopeFactory.CreateScope();
        var collectionRepo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queues = await collectionRepo.GetAllQueuesAsync();
        foreach (var q in queues)
        {
            q.IsSessionQueue = false;
        }

        var queue = await collectionRepo.GetQueueByIdAsync(queueId);
        if (queue == null || queue.Tracks.Count == 0)
        {
            _logger.LogWarning("Queue not found or is empty");
            return;
        }

        var orderedTracks = queue.GetShuffledOrder()
            .Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id))
            .Where(t => t != null)!
            .ToList();

        int effectiveIndex = startIndex != 0 ? startIndex : (queue.CurrentTrackIndex ?? 0);
        _iterator = new TrackIterator(orderedTracks, effectiveIndex);
        queue.IsSessionQueue = true;
        queue.CurrentTrackIndex = startIndex;
        await collectionRepo.SaveAsync(queue);

        if (queue.LastPlaybackPositionSeconds.HasValue)
        {
            await InternalPlayAsync(_iterator.Current, queue.LastPlaybackPositionSeconds);
        }
        else
        {
            await InternalPlayAsync(_iterator.Current);
        }
    }

    public async Task SaveActiveQueueStateAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var sessionQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

        if (sessionQueue != null && _iterator != null)
        {
            sessionQueue.CurrentTrackIndex = _iterator.CurrentIndex;
            sessionQueue.LastPlaybackPositionSeconds = _player?.Time;
            await repo.SaveAsync(sessionQueue);
        }
    }

    public async Task<int> CreateNowPlayingQueueAsync(List<Track> tracks, string title)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();

        var queues = await repo.GetAllQueuesAsync();
        foreach (var q in queues)
        {
            q.IsSessionQueue = false;
        }

        var queue = new TrackQueue
        {
            Title = title,
            IsSessionQueue = true,
            Tracks = tracks,
            CurrentTrackIndex = 0,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
        };

        queue.SetShuffledOrder(tracks.Select(t => t.TrackId).ToList());

        await repo.AddQueueAsync(queue);
        await repo.SaveAsync(queue);
        return queue.Id;
    }

    public async Task ResumeCurrentQueueAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var sessionQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

        if (sessionQueue != null)
        {
            await LoadQueueAndPlayAsync(sessionQueue.Id);
        }
    }


    public void EnableShuffle(bool enable = true)
    {
        ShuffleEnabled = enable;
        _logger.LogInformation($"Shuffle mode: {(ShuffleEnabled ? "Enabled" : "Disabled")}");
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
            if (queue != null)
            {
                queue.IsShuffleEnabled = enable;
                if (enable)
                {
                    queue.SetShuffledOrder(
                            queue.Tracks
                            .OrderBy(_ => ShuffleEnabled ? Guid.NewGuid() : Guid.Empty)
                            .Select(t => t.TrackId)
                            .ToList());

                }
                await repo.SaveAsync(queue);
            }
        }
        );
    }

    public void EnableLoop(bool enable = true)
    {
        _logger.LogInformation($"Loop mode: {(enable ? "Enabled" : "Disabled")}");
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

            if (queue != null)
            {
                queue.IsLoopEnabled = enable;
                await repo.SaveAsync(queue);
            }
        });
    }

    public async Task SetLoopModeAsync(LoopTrack mode)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
        if (queue != null)
        {
            queue.LoopTrack = mode;
            await repo.SaveAsync(queue);
        }
    }

    public async Task<LoopTrack> ToggleLoopModeAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

        if (queue != null)
        {
            queue.LoopTrack = queue.LoopTrack switch
            {
                LoopTrack.None => LoopTrack.Once,
                LoopTrack.Once => LoopTrack.Forever,
                LoopTrack.Forever => LoopTrack.None,
                _ => LoopTrack.None
            };
            _logger.LogInformation($"Loop mode set to: {queue.LoopTrack}");
            await repo.SaveAsync(queue);
            return queue.LoopTrack;
        }
        return LoopTrack.None;
    }
    public void Dispose()
    {
        Stop();
        _engine.Dispose();
        _logger.LogInformation("Disposed engine");
    }
}
