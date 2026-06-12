using LibVLCSharp.Shared;
using Microsoft.AspNetCore.SignalR;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;

namespace TreblePlayer.Core;

public class MusicPlayer : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlaybackHub> _hubContext;
    private readonly ILoggingService _logger;

    private readonly LibVLC _libVlc;
    private MediaPlayer? _player;
    private Media? _currentMedia;
    private TrackIterator? _iterator;

    private readonly object _locker = new();
    private bool _isPlaying;
    public bool ShuffleEnabled { get; set; }
    public bool AutoAdvanceEnabled { get; set; } = true;

    public MusicPlayer(IServiceScopeFactory scopeFactory, IHubContext<PlaybackHub> hubContext, ILoggingService logger)
    {
        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC();
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;
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

            if (AutoAdvanceEnabled)
            {
                _player.EndReached += (_, _) => HandleTrackEnd();
            }

            _player.Play();
            if (seekSeconds.HasValue) _player.Time = (long)(seekSeconds.Value * 1000);
            _isPlaying = true;
        }

        _logger.LogInformation($"Broadcasting PlaybackStarted for track {track.TrackId}");
        await _hubContext.Clients.All.SendAsync("PlaybackStarted", track.TrackId);
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
        lock (_locker)
        {
            if (_player == null || !_player.IsPlaying) return false;
            _player.Pause();
            _isPlaying = false;
            _ = SaveActiveQueueStateAsync();
            _hubContext.Clients.All.SendAsync("PlaybackPaused");
            return true;
        }
    }

    public bool Stop()
    {
        lock (_locker)
        {
            if (_player == null) return false;
            _ = SaveActiveQueueStateAsync();
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
            _hubContext.Clients.All.SendAsync("PlaybackStopped");
            return true;
        }
    }

    public bool Resume()
    {
        lock (_locker)
        {
            if (_player == null || _player.IsPlaying) return false;
            _player.Play();
            _isPlaying = true;
            _hubContext.Clients.All.SendAsync("PlaybackResume");
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
                _hubContext.Clients.All.SendAsync("PlaybackSeeked", seconds);
            }
        }
    }

    public bool IsPlaying() => _isPlaying && _player?.IsPlaying == true;

    public float? CurrentPositionSeconds => _player?.Time / 1000f;

    public async Task<TrackQueue?> GetActiveQueueAsync()
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

    public async Task AddTrackToQueueAsync(int queueId, int trackId)
    {
        using var scope = _scopeFactory.CreateScope();
        var track = await scope.ServiceProvider.GetRequiredService<ITrackRepository>().GetTrackByIdAsync(trackId);
        var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
        var queue = await repo.GetQueueByIdAsync(queueId);

        if (track == null || queue == null) throw new ArgumentException("Track or Queue not found");
        queue.AddTrack(track);
        await repo.SaveAsync(queue);
    }

    public async Task LoadQueueAndPlayAsync(int queueId, int startIndex = 0)
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var queues = await repo.GetAllQueuesAsync();
            foreach (var q in queues) q.IsSessionQueue = false;

            var queue = await repo.GetQueueByIdAsync(queueId);
            if (queue == null || !queue.Tracks.Any()) return;

            var orderedTracks = queue.IsShuffleEnabled
                ? queue.GetShuffledOrder().Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id)).Where(t => t != null).Cast<Track>().ToList()
                : queue.Tracks.OrderBy(t => t.TrackNumber).ToList();

            if (!orderedTracks.Any()) orderedTracks = queue.Tracks.OrderBy(t => t.TrackNumber).ToList();

            int effectiveIndex = startIndex != 0 ? startIndex : (queue.CurrentTrackIndex ?? 0);
            _iterator = new TrackIterator(orderedTracks, effectiveIndex, _logger);
            queue.IsSessionQueue = true;
            queue.CurrentTrackIndex = effectiveIndex;
            await repo.SaveAsync(queue);

            await InternalPlayAsync(_iterator.Current, queue.LastPlaybackPositionSeconds);
        });
    }

    public async Task SaveActiveQueueStateAsync()
    {
        await ExecuteInScopeAsync(async repo =>
        {
            var sessionQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
            if (sessionQueue != null && _iterator != null)
            {
                sessionQueue.CurrentTrackIndex = _iterator.CurrentIndex;
                sessionQueue.LastPlaybackPositionSeconds = CurrentPositionSeconds;
                await repo.SaveAsync(sessionQueue);
            }
        });
    }

    public async Task<int> CreateNowPlayingQueueAsync(List<Track> tracks, string title, int? collectionId = null)
    {
        return await ExecuteInScopeAsync(async repo =>
        {
            foreach (var q in await repo.GetAllQueuesAsync()) q.IsSessionQueue = false;

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
        });
    }

    public void EnableShuffle(bool enable = true)
    {
        ShuffleEnabled = enable;
        _ = Task.Run(async () =>
        {
            await ExecuteInScopeAsync(async repo =>
            {
                var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);
                if (queue == null) return;

                queue.IsShuffleEnabled = enable;
                var ids = enable ? queue.Tracks.OrderBy(_ => Guid.NewGuid()).Select(t => t.TrackId).ToList() : queue.Tracks.OrderBy(t => t.TrackNumber).Select(t => t.TrackId).ToList();
                queue.SetShuffledOrder(ids);
                await repo.SaveAsync(queue);

                if (_iterator != null && queue.IsSessionQueue)
                {
                    var tracks = ids.Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id)).Where(t => t != null).Cast<Track>().ToList();
                    _iterator = new TrackIterator(tracks, 0, _logger);
                }
            });
        });
    }

    public void EnableLoop(bool enable = true)
    {
        _ = Task.Run(async () =>
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
        var tracks = collection.Tracks.ToList();
        if (ShuffleEnabled) tracks = tracks.OrderBy(_ => Guid.NewGuid()).ToList();
        var queueId = await CreateNowPlayingQueueAsync(tracks, $"Now playing: {collection.Title}");
        await LoadQueueAndPlayAsync(queueId, Math.Clamp(startIndex, 0, tracks.Count - 1));
    }

    public void Dispose()
    {
        Stop();
        _libVlc.Dispose();
    }
}
