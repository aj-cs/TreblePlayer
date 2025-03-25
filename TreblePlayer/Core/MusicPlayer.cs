namespace TreblePlayer.Core;
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

public class MusicPlayer : IDisposable
{
    // private readonly ITrackRepository _trackRepository;
    // private readonly ITrackCollectionRepository _collectionRepository;
    private ITrackCollection? _currentCollection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<PlaybackHub> _hubContext;

    private CancellationTokenSource? _cts;

    private readonly MiniAudioEngine _engine;
    private SoundPlayer? _player;

    private readonly object _lock = new();
    private bool _isPlaying;

    //Tracking current collection
    private int _currentTrackIndex;

    public MusicPlayer(IServiceScopeFactory scopeFactory, IHubContext<PlaybackHub> hubContext)
    {
        _engine = new MiniAudioEngine(44100, Capability.Playback);
        _scopeFactory = scopeFactory;
        _hubContext = hubContext;

        _currentCollection = null;
        _currentTrackIndex = -1;
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

        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var stream = File.OpenRead(track.FilePath); //TODO: switch to FileService later
        _player = new SoundPlayer(new StreamDataProvider(stream));

        Mixer.Master.AddComponent(_player);
        _player.Play();
        _isPlaying = true;
        await _hubContext.Clients.All.SendAsync("PlaybackStarted", trackId);

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
            await _hubContext.Clients.All.SendAsync("PlaybackEnded", trackId);
        }, token);
    }

    //public async Task PlayNextAsync(string filePathToNext) {}

    // check if changing Pause and Stop to void is right
    public void Pause()
    {
        lock (_lock)
        {
            if (_player?.State == PlaybackState.Playing)
            {
                _player?.Pause();
                _isPlaying = false;
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
        var tracksToPlay = collection.Tracks.Skip(startIndex).ToList();
        foreach (var track in tracksToPlay)
        {
            await PlayAsync(track.TrackId); //start track
            // wait for current track to finish
            while (IsPlaying())
            {
                await Task.Delay(500);
            }
        }
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
    public void Dispose()
    {
        Stop();
        _engine.Dispose();
        if (_engine.IsDisposed)
        {
            Console.WriteLine("Disposed engine");
        }
    }

    // private void TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
    // {
    //     Console.WriteLine($"Time Changed: {e.Time / 1000}s");
    //     _hubContext.Clients.All.SendAsync("TimeChanged", e.Time);
    // }
    // private void PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
    // {
    //     Console.WriteLine($"Position Changed: {e.Position * 100}%");
    //     _hubContext.Clients.All.SendAsync("PositionChanged", e.Position);
    // }
    //
    // private void EndReached(object sender, EventArgs e)
    // {
    //     Console.WriteLine("Track ended.");
    //     _hubContext.Clients.All.SendAsync("PlaybackEnded");
    //     _tcs.TrySetResult(true);
    // }
    //
    // private void Stopped(object sender, EventArgs e)
    // {
    //     Console.WriteLine("Track stopped.");
    //     _hubContext.Clients.All.SendAsync("PlaybackStopped");
    //     _tcs.TrySetResult(true);
    // }
    //
    // private void Paused(object sender, EventArgs e)
    // {
    //     Console.WriteLine("Track paused.");
    //     _hubContext.Clients.All.SendAsync("PlaybackPaused");
    //     //_tcs.TrySetResult(true);
    // }
    //
    // private void Playing(object sender, EventArgs e)
    // {
    //     Console.WriteLine("Track playing.");
    //     _hubContext.Clients.All.SendAsync("PlaybackPlaying");
    // }
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
//
// public async Task AddTrackToQueueAsync(int queueId, int trackId)
// {
//     var queue = await _collectionRepository.GetTrackCollectionByIdAsync(queueId, TrackCollectionType.TrackQueue) as TrackQueue;
//     var track = await _trackRepository.GetTrackByIdAsync(trackId);
//
//     if (queue == null || track == null)
//     {
//         throw new Exception("Queue or Track not found.");
//     }
//     queue.AddTrack(track);
//     await _collectionRepository.SaveAsync(queue);
// }
