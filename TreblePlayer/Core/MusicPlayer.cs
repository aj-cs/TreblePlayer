namespace TreblePlayer.Core;
using TreblePlayer.Data;
using System;
using LibVLCSharp.Shared;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

public class MusicPlayer
{
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly IHubContext<PlaybackHub> _hubContext;
    private LibVLC _libVlc; // maybe readonly
    private MediaPlayer _mediaPlayer;
    private TaskCompletionSource<bool>? _tcs;

    //Tracking current collection
    private ITrackCollection? _currentCollection;
    private int _currentTrackIndex;

    public MusicPlayer(ITrackRepository trackRepository, ITrackCollectionRepository collectionRepository, IHubContext<PlaybackHub> hubContext)
    {
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _hubContext = hubContext;
        Core.Initialize();
        _libVlc = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVlc);

        // subscribe to events, for real time updates
        _mediaPlayer.TimeChanged += TimeChanged;
        _mediaPlayer.PositionChanged += PositionChanged;
        _mediaPlayer.Paused += Paused;
        _mediaPlayer.EndReached += EndReached;
        _mediaPlayer.Stopped += Stopped;
        _mediaPlayer.Playing += Playing;


        _currentCollection = null;
        _currentTrackIndex = -1;
    }


    public async Task PlayAsync(int trackId)
    {
        if (_tcs != null && !_tcs.Task.IsCompleted)
        {
            _tcs.TrySetResult(false); // or maybe _tcs.TrySetCanceled() idk
        }
        var track = await _trackRepository.GetTrackByIdAsync(trackId);
        if (track == null)
        {
            throw new Exception("Track not found");
        }

        var media = new Media(_libVlc, track.FilePath, FromType.FromPath);
        _mediaPlayer.Play(media);
        Console.WriteLine("Now playing");

        //wait for playback to finish or user pauses or user stops (stops might not be necessary)

        _tcs = new TaskCompletionSource<bool>();

        //end of media means mark task as complete
        //_mediaPlayer.EndReached += (sender, e) => tcs.TrySetResult(true);

        //user pausing or stopping marks task as complete
        //_mediaPlayer.Paused += (sender, e) => tcs.TrySetResult(true);
        //_mediaPlayer.Stopped += (sender, e) => tcs.TrySetResult(true);

        await _tcs.Task;

    }

    //public async Task PlayNextAsync(string filePathToNext) {}

    // check if changing Pause and Stop to void is right
    public async Task PauseAsync()
    {
        if (_mediaPlayer.CanPause)
        {
            _mediaPlayer.Pause();

            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                //_tcs.TrySetResult(false);
                await _tcs.Task;
            }
        }
    }

    public async Task StopAsync()
    {
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Stop();
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                await _tcs.Task;
            }
        }

    }

    public async Task ResumeAsync()
    {
        if (!_mediaPlayer.IsPlaying && !_mediaPlayer.CanPause)
        {
            _mediaPlayer.Play();
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                await _tcs.Task;
            }
        }
    }

    public bool IsPlaying()
    {
        return _mediaPlayer.IsPlaying;
    }

    public async Task CreateQueueFromAlbumOrPlaylistAsync(int collectionId, TrackCollectionType type)
    {
        //change later, 
        // queues should be created from track(s) aka IEnumerable/ICollection<Track> or an ITrackCollection
        //temporary queue logic, final musicplayer.cs
        ITrackCollection collection = type switch
        {
            TrackCollectionType.Album =>
                collection = await _collectionRepository.GetTrackCollectionByIdAsync(collectionId, TrackCollectionType.Album) as Album,

            TrackCollectionType.Playlist =>
                collection = await _collectionRepository.GetTrackCollectionByIdAsync(collectionId, TrackCollectionType.Album) as Playlist,

            _ => throw new ArgumentException("Cannot create new queue out of existing queue.")

        };

        if (collection == null)
        {
            throw new Exception("Invalid collection type or collection not found.");
        }
        //if (string.IsNullOrWhiteSpace(title))
        //{
        //    throw new Exception("Queue title cannot be null or empty.");
        //}

        TrackQueue newQueue = TrackQueue.CreateFromCollection(collection);
        await _collectionRepository.AddQueueAsync(newQueue);
    }

    public async Task CreateQueueFromTracksAsync(int[] trackIds)
    {
        List<Track> tracks = new List<Track>();
        if (trackIds.Count() == 1)
        {
            await _trackRepository.GetTrackByIdAsync(trackIds[0]);
        }
        else
        {
            tracks.AddRange(await _trackRepository.GetTracksByIdAsync(trackIds));
        }
    }

    public async Task AddTrackToQueueAsync(int queueId, int trackId)
    {
        var queue = await _collectionRepository.GetTrackCollectionByIdAsync(queueId, TrackCollectionType.TrackQueue) as TrackQueue;
        var track = await _trackRepository.GetTrackByIdAsync(trackId);

        if (queue == null || track == null)
        {
            throw new Exception("Queue or Track not found.");
        }
        queue.AddTrack(track);
        await _collectionRepository.SaveAsync(queue);
    }
    public async Task PlayCollectionAsync(ITrackCollection collection, int startIndex = 0)
    {
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
            if (_tcs != null && !_tcs.Task.IsCompleted)
            {
                _tcs.TrySetResult(false); // or maybe _tcs.TrySetCanceled() idk
            }

            var media = new Media(_libVlc, track.FilePath, FromType.FromPath);
            _mediaPlayer.Play(media);
            Console.WriteLine($"Now playing: {track.Title} (ID: {track.TrackId}) in {collection.CollectionType.ToString()}: {collection.Title} (ID: {collection.Id}");

            _tcs = new TaskCompletionSource<bool>(); // check if this is right

            await _tcs.Task;
        }
    }

    public async Task DeleteAlbumAsync(int albumId)
    {
        /*
         * albums are considered to be the base, deleting albums means
         * deleting tracks, maybe in the future deleting the album gives an option
         * to send to a "void" 'album', where there is no album metadata
         */
        var album = await _collectionRepository.GetTrackCollectionByIdAsync(albumId, TrackCollectionType.Album) as Album;

        if (album == null)
        {
            throw new Exception($"Album with id {albumId} not found.");
        }
        foreach (var track in album.Tracks)
        {
            if (File.Exists(track.FilePath))
            {
                File.Delete(track.FilePath);
            }
        }

        await _collectionRepository.RemoveAlbumAndTracksAsync(album);
        //issues if the file is in use or lacks permissions
    }

    public async Task DeleteCollectionAsync(ITrackCollection collection)
    {
        // might add a method for "soft" deleting albums (ie send them to the void album)
        switch (collection.CollectionType)
        {
            case TrackCollectionType.Album:
                await DeleteAlbumAsync(collection.Id);
                break;
            case TrackCollectionType.Playlist:
            case TrackCollectionType.TrackQueue:
                _collectionRepository.RemoveCollectionFromDb(collection);
                await _collectionRepository.SaveChangesAsync();
                break;
            default:
                throw new ArgumentException("Unsupported track collection type.");
        }
    }

    private void TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
    {
        Console.WriteLine($"Time Changed: {e.Time / 1000}s");
        _hubContext.Clients.All.SendAsync("TimeChanged", e.Time);
    }
    private void PositionChanged(object sender, MediaPlayerPositionChangedEventArgs e)
    {
        Console.WriteLine($"Position Changed: {e.Position * 100}%");
        _hubContext.Clients.All.SendAsync("PositionChanged", e.Position);
    }

    private void EndReached(object sender, EventArgs e)
    {
        Console.WriteLine("Track ended.");
        _hubContext.Clients.All.SendAsync("PlaybackEnded");
        _tcs.TrySetResult(true);
    }

    private void Stopped(object sender, EventArgs e)
    {
        Console.WriteLine("Track stopped.");
        _hubContext.Clients.All.SendAsync("PlaybackStopped");
        _tcs.TrySetResult(true);
    }

    private void Paused(object sender, EventArgs e)
    {
        Console.WriteLine("Track paused.");
        _hubContext.Clients.All.SendAsync("PlaybackPaused");
        //_tcs.TrySetResult(true);
    }

    private void Playing(object sender, EventArgs e)
    {
        Console.WriteLine("Track playing.");
        _hubContext.Clients.All.SendAsync("PlaybackPlaying");
    }
}
