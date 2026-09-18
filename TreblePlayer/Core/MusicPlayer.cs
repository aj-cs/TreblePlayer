using LibVLCSharp.Shared;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;

namespace TreblePlayer.Core;

public class MusicPlayer : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggingService _logger;

    private readonly LibVLC _libVlc;
    private MediaPlayer? _player;
    private Media? _currentMedia;
    private TrackIterator? _iterator;

    private readonly object _locker = new();
    private readonly SemaphoreSlim _stateSaveLock = new(1, 1);
    private bool _isPlaying;
    private int _volume = 70;
    public bool ShuffleEnabled { get; set; }
    public bool AutoAdvanceEnabled { get; set; } = true;

    // Events for WebSocket broadcasting
    public event Action<int>? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action? PlaybackResumed;
    public event Action<float>? PlaybackSeeked;
    public event Action<float>? PositionChanged;

    public MusicPlayer(IServiceScopeFactory scopeFactory, ILoggingService logger)
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC();
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private async Task ExecuteInScopeAsync(Func<ITrackCollectionRepository, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        await action(repo);
    }

    private async Task<T> ExecuteInScopeAsync<T>(Func<ITrackCollectionRepository, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        return await action(repo);
    }

    public async Task InternalPlayAsync(Track? track, float? seekSeconds = null)
    {
        if (track == null) return;

        lock (_locker)
        {
            Stop();
            _logger.LogInformation($"MusicPlayer: Preparing to play track (ID: {track.TrackId})");
            _currentMedia = new Media(_libVlc, new Uri(track.FilePath));
            _player = new MediaPlayer(_currentMedia);
            _player.Volume = _volume;

            if (AutoAdvanceEnabled)
            {
                _player.EndReached += (_, _) => HandleTrackEnd();
            }

            _player.TimeChanged += (s, e) => 
            {
                PositionChanged?.Invoke(e.Time / 1000f);
            };

            _player.Play();
            if (seekSeconds.HasValue) _player.Time = (long)(seekSeconds.Value * 1000);
            _isPlaying = true;
        }

        _logger.LogInformation($"Broadcasting PlaybackStarted for track {track.TrackId}");
        PlaybackStarted?.Invoke(track.TrackId);
        _ = SaveActiveQueueStateAsync();
        _logger.LogInformation($"Playing: {track.Title}, ID: {track.TrackId}");
    }

    private void HandleTrackEnd()
    {
        var currentTrack = _iterator?.Current;
        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteInScopeAsync(async repo =>
                {
                    var activeQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
                    if (activeQueue == null || currentTrack == null)
                    {
                        await NextAsync();
                        return;
                    }

                    switch (activeQueue.LoopTrack)
                    {
                        case LoopTrack.Forever:
                            await InternalPlayAsync(currentTrack);
                            break;
                        case LoopTrack.Once:
                            activeQueue.LoopTrack = LoopTrack.None;
                            await repo.SaveAsync(activeQueue);
                            await InternalPlayAsync(currentTrack);
                            break;
                        default:
                            await NextAsync();
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error during track end: {ex.Message}", ex);
            }
        });
    }

    public async Task PlayAsync(int trackId)
    {
        Track? track;
        using (var scope = _scopeFactory.CreateScope())
        {
            track = await scope.ServiceProvider.GetRequiredService<ITrackRepository>().GetTrackByIdAsync(trackId);
        }

        if (track == null) throw new Exception("Track not found");
        var queueId = await CreateNowPlayingQueueAsync(new List<Track> { track }, $"Now playing: {track.Title}");
        await LoadQueueAndPlayAsync(queueId);
    }

    public async Task PlayCollectionAsync(int collectionId, TrackCollectionType type, int startIndex = 0)
    {
        var collection = await ExecuteInScopeAsync(repo => repo.GetTrackCollectionByIdAsync(collectionId, type));
        if (collection == null || !collection.Tracks.Any()) return;
        await StartPlaybackFromCollection(collection, startIndex);
    }

    public bool Pause()
    {
        bool paused;
        lock (_locker)
        {
            if (_player == null || !_player.IsPlaying) return false;
            _player.Pause();
            _isPlaying = false;
            paused = true;
        }

        _ = SaveActiveQueueStateAsync();
        if (paused) PlaybackPaused?.Invoke();
        return paused;
    }

    public bool Stop()
    {
        bool stopped;
        lock (_locker)
        {
            if (_player == null) return false;
            try
            {
                _player.Stop();
                _player.Dispose();
                _currentMedia?.Dispose();
            }
            catch (Exception ex) { _logger.LogWarning($"Stop failed: {ex.Message}"); }
            _isPlaying = false;
            _player = null;
            _currentMedia = null;
            stopped = true;
        }

        _ = SaveActiveQueueStateAsync();
        if (stopped) PlaybackStopped?.Invoke();
        return stopped;
    }

    public bool Resume()
    {
        lock (_locker)
        {
            if (_player == null || _player.IsPlaying) return false;
            _player.Play();
            _isPlaying = true;
            PlaybackResumed?.Invoke();
            return true;
        }
    }

    public void Seek(float seconds)
    {
        lock (_locker)
        {
            if (_player != null)
            {
                _player.Time = (long)(seconds * 1000);
                PlaybackSeeked?.Invoke(seconds);
            }
        }
    }

    public bool IsPlaying() => _isPlaying && _player?.IsPlaying == true;

    public int Volume => _volume;

    public void SetVolume(int volume)
    {
        var normalizedVolume = Math.Clamp(volume, 0, 100);
        lock (_locker)
        {
            _volume = normalizedVolume;
            if (_player != null) _player.Volume = normalizedVolume;
        }
    }

    public float? CurrentPositionSeconds => _player?.Time / 1000f;

    public virtual async Task<TrackQueue?> GetActiveQueueAsync()
    {
        return await ExecuteInScopeAsync(async repo =>
        {
            return (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
        });
    }

    public async Task NextAsync()
    {
        if (_iterator?.HasNext == true)
        {
            await InternalPlayAsync(_iterator.Next);
        }
        else
        {
            await ExecuteInScopeAsync(async repo =>
            {
                var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
                if (queue?.IsLoopEnabled == true)
                {
                    _iterator?.Reset();
                    await InternalPlayAsync(_iterator?.Current);
                }
            });
        }
    }

    public async Task PreviousAsync()
    {
        if (_iterator?.HasPrevious == true) await InternalPlayAsync(_iterator.Previous);
    }

    public async Task CreateQueueAsync(string title) => await ExecuteInScopeAsync(repo => repo.AddQueueAsync(new TrackQueue { Title = title }));

    public async Task DeleteQueueAsync(int queueId)
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var queue = await repo.GetQueueByIdAsync(queueId);
            if (queue == null) throw new Exception("Queue not found");

            if (queue.IsSessionQueue)
            {
                Stop();
                _iterator = null;
            }

            await repo.RemoveCollectionFromDb(queue);
        });
    }

    public async Task ReorderQueueAsync(int queueId, List<int> trackIds)
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var queue = await repo.GetQueueByIdAsync(queueId);
            if (queue == null) throw new Exception("Queue not found");

            var trackMap = queue.Tracks.ToDictionary(t => t.TrackId);

            var requestedIds = trackIds.Distinct().ToList();
            if (requestedIds.Count != trackMap.Count || requestedIds.Any(id => !trackMap.ContainsKey(id)))
            {
                throw new ArgumentException("The reordered queue must contain each existing track exactly once.");
            }

            queue.SetPlaybackOrder(requestedIds);
            queue.IsManuallyModified = true;

            if (queue.IsSessionQueue && _iterator != null)
            {
                var currentTrackId = _iterator.Current?.TrackId;
                var orderedTracks = requestedIds.Select(id => trackMap[id]).ToList();
                var newIndex = currentTrackId.HasValue
                    ? Math.Max(0, orderedTracks.FindIndex(t => t.TrackId == currentTrackId.Value))
                    : 0;

                _iterator = new TrackIterator(orderedTracks, newIndex, _logger);
                queue.CurrentTrackIndex = newIndex;
            }

            await repo.SaveAsync(queue);
        });
    }

    public async Task AddTrackToQueueAsync(int queueId, int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var track = await scope.ServiceProvider.GetRequiredService<ITrackRepository>().GetTrackByIdAsync(trackId);
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = await repo.GetQueueByIdAsync(queueId);

        if (track == null || queue == null) throw new ArgumentException("Track or Queue not found");
        queue.AddTrack(track);
        queue.SetPlaybackOrder(queue.GetPlaybackOrder().Append(trackId));
        queue.IsManuallyModified = true;
        await repo.SaveAsync(queue);
    }

    public async Task LoadQueueAndPlayAsync(int queueId, int? startIndex = null)
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var queues = await repo.GetAllQueuesAsync();
            foreach (var q in queues) q.IsSessionQueue = false;

            var queue = await repo.GetQueueByIdAsync(queueId);
            if (queue == null || !queue.Tracks.Any()) return;

            var trackMap = queue.Tracks.ToDictionary(t => t.TrackId);
            var orderedTracks = queue.GetPlaybackOrder()
                .Where(trackMap.ContainsKey)
                .Select(id => trackMap[id])
                .ToList();

            if (!orderedTracks.Any()) orderedTracks = queue.Tracks.OrderBy(t => t.TrackNumber).ToList();

            var previousIndex = queue.CurrentTrackIndex;
            var previousTrackId = queue.LastPlayedTrackId;
            var previousPosition = queue.LastPlaybackPositionSeconds;
            int effectiveIndex = Math.Clamp(startIndex ?? queue.CurrentTrackIndex ?? 0, 0, orderedTracks.Count - 1);
            _iterator = new TrackIterator(orderedTracks, effectiveIndex, _logger);
            queue.IsSessionQueue = true;
            queue.CurrentTrackIndex = effectiveIndex;
            await repo.SaveAsync(queue);

            var isSameSavedTrack = previousIndex == effectiveIndex && previousTrackId == _iterator.Current?.TrackId;
            float? resumePosition = isSameSavedTrack ? previousPosition : null;
            await InternalPlayAsync(_iterator.Current, resumePosition);
        });
    }

    public async Task SaveActiveQueueStateAsync()
    {
        await _stateSaveLock.WaitAsync();
        try
        {
            await ExecuteInScopeAsync(async repo =>
            {
                var sessionQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
                if (sessionQueue != null && _iterator != null)
                {
                    sessionQueue.CurrentTrackIndex = _iterator.CurrentIndex;
                    sessionQueue.LastPlaybackPositionSeconds = CurrentPositionSeconds;
                    sessionQueue.LastPlayedTrackId = _iterator.Current?.TrackId;
                    await repo.SaveAsync(sessionQueue);
                }
            });
        }
        finally
        {
            _stateSaveLock.Release();
        }
    }

    public async Task<int> CreateNowPlayingQueueAsync(List<Track> tracks, string title, int? collectionId = null, TrackCollectionType? collectionType = null)
    {
        return await ExecuteInScopeAsync(async repo =>
        {
            foreach (var q in await repo.GetAllQueuesAsync()) q.IsSessionQueue = false;

            var queue = new TrackQueue
            {
                Title = title,
                IsSessionQueue = true,
                IsShuffleEnabled = ShuffleEnabled,
                Tracks = tracks,
                CurrentTrackIndex = 0,
                DateCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                CollectionId = collectionId,
                OriginCollectionType = collectionType,
                IsManuallyModified = false
            };
            queue.SetShuffledOrder(tracks.Select(t => t.TrackId).ToList());

            await repo.AddQueueAsync(queue);
            await repo.SaveAsync(queue);
            return queue.Id;
        });
    }

    public async Task EnableShuffleAsync(bool enable = true)
    {
        ShuffleEnabled = enable;
        await ExecuteInScopeAsync(async repo =>
        {
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
            if (queue == null) return;

            queue.IsShuffleEnabled = enable;
            var ids = enable
                ? queue.Tracks.OrderBy(_ => Guid.NewGuid()).Select(t => t.TrackId).ToList()
                : queue.Tracks.OrderBy(t => t.DiscNumber).ThenBy(t => t.TrackNumber).Select(t => t.TrackId).ToList();
            queue.SetPlaybackOrder(ids);
            await repo.SaveAsync(queue);

            if (_iterator != null)
            {
                var currentTrackId = _iterator.Current?.TrackId;
                var tracks = ids.Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id)).Where(t => t != null).Cast<Track>().ToList();
                var currentIndex = currentTrackId.HasValue ? Math.Max(0, tracks.FindIndex(t => t.TrackId == currentTrackId.Value)) : 0;
                _iterator = new TrackIterator(tracks, currentIndex, _logger);
                queue.CurrentTrackIndex = currentIndex;
                await repo.SaveAsync(queue);
            }
        });
    }

    public async Task EnableLoopAsync(bool enable = true)
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
            if (queue != null)
            {
                queue.IsLoopEnabled = enable;
                await repo.SaveAsync(queue);
            }
        });
    }

    public async Task SetLoopModeAsync(LoopTrack mode) => await ExecuteInScopeAsync(async repo =>
    {
        var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
        if (queue != null) { queue.LoopTrack = mode; await repo.SaveAsync(queue); }
    });

    public async Task<LoopTrack> ToggleLoopModeAsync() => await ExecuteInScopeAsync(async repo =>
    {
        var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
        if (queue == null) return LoopTrack.None;
        queue.LoopTrack = queue.LoopTrack switch { LoopTrack.None => LoopTrack.Once, LoopTrack.Once => LoopTrack.Forever, _ => LoopTrack.None };
        await repo.SaveAsync(queue);
        return queue.LoopTrack;
    });

    private async Task StartPlaybackFromCollection(ITrackCollection collection, int startIndex = 0)
    {
        var tracks = collection.Tracks
            .OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ToList();
        
        if (ShuffleEnabled) tracks = tracks.OrderBy(_ => Guid.NewGuid()).ToList();

        // Check for existing queue for this collection
        var existingQueueId = await ExecuteInScopeAsync(async repo =>
        {
            var queues = await repo.GetAllQueuesAsync();
            return queues.FirstOrDefault(q => q.CollectionId == collection.Id && q.OriginCollectionType == collection.CollectionType)?.Id;
        });

        int queueId;
        if (existingQueueId.HasValue)
        {
            queueId = existingQueueId.Value;
            // Only update queue tracks if it hasn't been manually modified
            await ExecuteInScopeAsync(async repo =>
            {
                var queue = await repo.GetQueueByIdAsync(queueId);
                if (!queue.IsManuallyModified)
                {
                    // For now, avoid re-assigning tracks to prevent tracking conflicts
                    // The existing tracks should be sufficient for re-playback
                    queue.IsShuffleEnabled = ShuffleEnabled;
                    queue.SetPlaybackOrder(tracks.Select(t => t.TrackId).ToList());
                    await repo.SaveAsync(queue);
                }
            });
        }
        else
        {
            queueId = await CreateNowPlayingQueueAsync(tracks, $"Now playing: {collection.Title}", collection.Id, collection.CollectionType);
        }

        await LoadQueueAndPlayAsync(queueId, Math.Clamp(startIndex, 0, tracks.Count - 1));
    }

    public void Dispose()
    {
        Stop();
        _libVlc.Dispose();
    }
}
