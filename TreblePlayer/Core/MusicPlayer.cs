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

namespace TreblePlayer.Core;

public class MusicPlayer : IDisposable
{
    private ITrackCollection? _currentCollection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlaybackHub> _hubContext;

    private CancellationTokenSource? _cts;

    private readonly MiniAudioEngine _engine;
    private SoundPlayer? _player;

    private TrackIterator? _iterator;

    private readonly object _lock = new();
    private bool _isPlaying;
    private bool _shuffleEnabled = false;

    //tracking current collection
    private int _currentTrackIndex;

    public MusicPlayer(IServiceScopeFactory scopeFactory, IHubContext<PlaybackHub> hubContext)
    {
        _engine = new MiniAudioEngine(44100, Capability.Playback);
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;

        _currentCollection = null;
        _currentTrackIndex = -1;
    }

    public async Task InternalPlayAsync(Track? track, float? seekSeconds = null)
    {
        if (track == null)
        {
            return;
        }

        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var stream = File.OpenRead(track.FilePath); //TODO: switch to FileService later
        _player = new SoundPlayer(new StreamDataProvider(stream));

        Mixer.Master.AddComponent(_player);
        _player.Play();
        _isPlaying = true;

        if (seekSeconds.HasValue)
        {
            _player.Seek(seekSeconds.Value);
            Console.WriteLine($"Resumed at {seekSeconds.Value} seconds");
        }

        await _hubContext.Clients.All.SendAsync("PlaybackStarted", track.TrackId);
        Console.WriteLine($"Playing: {track.Title}, ID: {track.TrackId}");

        _ = Task.Run(async () =>
                {
                    await Task.Delay(100); // gives time to update player state from stopped to playing
                    while (!(_player.State == PlaybackState.Stopped) && !token.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                    }

                    lock (_lock)
                    {
                        _isPlaying = false;
                        _player.Stop();
                        Mixer.Master.RemoveComponent(_player);
                        _player = null;
                    }
                    await _hubContext.Clients.All.SendAsync("PlaybackEnded", track.TrackId);


                    await SaveActiveQueueStateAsync(); // saves queue progress

                    if (_iterator == null)
                    {
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
                    var sessionQueue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

                    if (sessionQueue == null)
                    {
                        return;
                    }

                    switch (sessionQueue.LoopTrack)
                    {
                        case LoopTrack.Once:
                            sessionQueue.LoopTrack = LoopTrack.None;
                            await repo.SaveAsync(sessionQueue);
                            await InternalPlayAsync(_iterator.Current);
                            break;
                        case LoopTrack.Forever:
                            await InternalPlayAsync(_iterator.Current);
                            break;
                        default:
                            if (_iterator.HasNext)
                            {
                                await NextAsync();
                            }
                            break;
                    }
                    // // advance if a collection is playing
                    // if (_iterator?.HasNext == true)
                    // {
                    //     await NextAsync(); // triggers internalplayasync again
                    // }
                }, token);

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
            Console.WriteLine("No tracks in the collection to play.");
            return;
        }

        if (startIndex < 0 || startIndex >= collection.Tracks.Count)
        {
            Console.WriteLine("Invalid start index.");
            return;
        }
        //skip to the start index
        var tracks = collection.Tracks.Skip(startIndex).ToList();

        if (_shuffleEnabled)
        {
            tracks = tracks.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        var queueId = await CreateNowPlayingQueueAsync(tracks, $"Now playing: {collection.Title}"); // , type: {type}, typeID: {collectionId}
        await LoadQueueAndPlayAsync(queueId);
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_player?.State == PlaybackState.Playing)
            {
                _player?.Pause();
                _isPlaying = false;
                _ = SaveActiveQueueStateAsync(); // persist s the paused pos
                _hubContext.Clients.All.SendAsync("PlaybackPaused");
                Console.WriteLine("Paused");

            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();

            _ = SaveActiveQueueStateAsync(); // persist the queue state before stopping

            _player?.Stop();
            _isPlaying = false;


            if (_player != null)
            {
                Mixer.Master.RemoveComponent(_player);
                _player = null;
            }

            _hubContext.Clients.All.SendAsync("PlaybackStopped");
            Console.WriteLine("Stopped");
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_player?.State == PlaybackState.Paused)
            {
                _player.Play();
                _isPlaying = true;
                _hubContext.Clients.All.SendAsync("PlaybackResume");
                Console.WriteLine("Resumed");
            }
        }
    }

    public void Seek(float seconds)
    {
        lock (_lock)
        {
            _player?.Seek(seconds);
            _hubContext.Clients.All.SendAsync("PlaybackSeeked", seconds);
            Console.WriteLine($"Seeked to {seconds} seconds");
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
            Console.WriteLine($"Failed to remove track from queue: {ex.Message}");
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
        Console.WriteLine($"Shuffled queue {queueId}");

    }

    public async Task LoadQueueAndPlayAsync(int queueId)
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
            Console.WriteLine("Queue not found or is empty");
            return;
        }

        var orderedTracks = queue.GetShuffledOrder()
            .Select(id => queue.Tracks.FirstOrDefault(t => t.TrackId == id))
            .Where(t => t != null)!
            .ToList();

        _iterator = new TrackIterator(orderedTracks, queue.CurrentTrackIndex ?? 0);
        queue.IsSessionQueue = true;
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
        _shuffleEnabled = enable;
        Console.WriteLine($"Shuffle mode: {(_shuffleEnabled ? "Enabled" : "Disabled")}");
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
                            .OrderBy(_ => _shuffleEnabled ? Guid.NewGuid() : Guid.Empty)
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
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
            var queue = (await repo.GetAllQueuesAsync()).FirstOrDefault(q => q.IsSessionQueue);

            if (queue != null)
            {
                queue.IsLoopEnabled = enable;
                await repo.SaveAsync(queue);
                Console.WriteLine($"Loop mode: {(enable ? "Enabled" : "Disabled")}");
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
            Console.WriteLine($"Loop mode set to: {queue.LoopTrack}");
            await repo.SaveAsync(queue);
            return queue.LoopTrack;
        }
        return LoopTrack.None;
    }
    public void Dispose()
    {
        Stop();
        _engine.Dispose();
        if (_engine.IsDisposed)
        {
            Console.WriteLine("Disposed engine");
        }
    }
}
// public async Task CreateQueueFromAlbumOrPlaylistAsync(int collectionId, TrackCollectionType type)
// {
//     //change later, 
//     // queues should be created from track(s) aka IEnumerable/ICollection<Track> or an ITrackCollection
//     //temporary queue logic, final musicplayer.cs
//     ITrackCollection collection = type switch
//     {
//         TrackCollectionType.Album =>
//             collection = await _collectionRepository.GetTrackCollectionByIdAsync(collectionId, TrackCollectionType.Album) as Album,
//
//         TrackCollectionType.Playlist =>
//             collection = await _collectionRepository.GetTrackCollectionByIdAsync(collectionId, TrackCollectionType.Album) as Playlist,
//
//         _ => throw new ArgumentException("Cannot create new queue out of existing queue.")
//
//     };
//
//     if (collection == null)
//     {
//         throw new Exception("Invalid collection type or collection not found.");
//     }
//     //if (string.IsNullOrWhiteSpace(title))
//     //{
//     //    throw new Exception("Queue title cannot be null or empty.");
//     //}
//
//     TrackQueue newQueue = TrackQueue.CreateFromCollection(collection);
//     await _collectionRepository.AddQueueAsync(newQueue);
// }
//
// public async Task CreateQueueFromTracksAsync(int[] trackIds)
// {
//     List<Track> tracks = new List<Track>();
//     if (trackIds.Count() == 1)
//     {
//         await _trackRepository.GetTrackByIdAsync(trackIds[0]);
//     }
//     else
//     {
//         tracks.AddRange(await _trackRepository.GetTracksByIdAsync(trackIds));
//     }
// }
