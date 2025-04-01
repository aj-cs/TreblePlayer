using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace TreblePlayer.Data;

public class TrackCollectionRepository : ITrackCollectionRepository
{
    private readonly MusicPlayerDbContext _dbContext;

    public TrackCollectionRepository(MusicPlayerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ITrackCollection> GetTrackCollectionByIdAsync(int collectionId, TrackCollectionType collectionType)
    {
        return collectionType switch
        {

            TrackCollectionType.Album => await _dbContext.Albums.Include(a => a.Tracks)
                .FirstOrDefaultAsync(a => a.Id == collectionId),
            TrackCollectionType.Playlist => await _dbContext.Playlists.Include(p => p.Tracks)
                .FirstOrDefaultAsync(p => p.Id == collectionId),
            TrackCollectionType.TrackQueue => await _dbContext.TrackQueues.Include(q => q.Tracks)
                .FirstOrDefaultAsync(q => q.Id == collectionId),
            _ => throw new ArgumentException("Unsupported track collection type.")
        };
    }


    public async Task SaveAsync(ITrackCollection collection)
    {
        var existingCollection = await GetTrackCollectionByIdAsync(collection.Id, collection.CollectionType);
        if (existingCollection == null)
        {
            throw new ArgumentException("Collection not found.");
        }

        _dbContext.Entry(existingCollection).CurrentValues.SetValues(collection);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<string> GetCollectionTitleByIdAsync(int collectionId, TrackCollectionType type)
    {
        var collection = await GetTrackCollectionByIdAsync(collectionId, type);
        if (collection == null)
        {
            throw new ArgumentException("Collection not found.");
        }
        return collection.Title;
    }

    public async Task<List<ITrackCollection>> GetCollectionsByTitleAsync(string title)
    {
        var albums = await _dbContext.Albums.Where(a => a.Title.Contains(title)).ToListAsync();
        var playlists = await _dbContext.Playlists.Where(a => a.Title.Contains(title)).ToListAsync();
        var queues = await _dbContext.TrackQueues.Where(a => a.Title.Contains(title)).ToListAsync();
        return albums.Cast<ITrackCollection>()
            .Concat(playlists.Cast<ITrackCollection>())
            .Concat(queues.Cast<ITrackCollection>())
            .ToList();
    }
    public async Task<List<ITrackCollection>> GetCollectionsByTrackAsync(int trackId)
    {
        var albums = await _dbContext.Albums.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
        var playlists = await _dbContext.Playlists.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
        var queues = await _dbContext.TrackQueues.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
        return albums.Cast<ITrackCollection>()
            .Concat(playlists.Cast<ITrackCollection>())
            .Concat(queues.Cast<ITrackCollection>())
            .ToList();
    }

    public async Task<Album> GetAlbumByIdAsync(int albumId)
    {
        return await _dbContext.Albums
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.Id == albumId);
    }

    public async Task<Playlist> GetPlaylistByIdAsync(int playlistId)
    {
        return await _dbContext.Playlists
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.Id == playlistId);
    }

    public async Task RemoveCollectionFromDb(ITrackCollection collection)
    {
        if (collection is Album album)
        {
            _dbContext.Albums.Remove(album);
        }
        else if (collection is Playlist playlist)
        {
            _dbContext.Playlists.Remove(playlist);
        }

        else if (collection is TrackQueue queue)
        {
            _dbContext.TrackQueues.Remove(queue);
        }
        else
        {
            throw new ArgumentException("Unsupported Track Collection");
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAlbumAndTracksAsync(Album album)
    {
        // remove the tracks
        if (album == null)
        {
            throw new ArgumentException("Album not found.");
        }

        if (!_dbContext.Albums.Contains(album))
        {
            Console.WriteLine("Album does not exist in DB");
            return;
        }

        _dbContext.Albums.Remove(album);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateCollectionAsync(ITrackCollection collection)
    {
        _dbContext.Update(collection);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<TrackQueue> GetQueueByIdAsync(int queueId)
    {
        return await _dbContext.TrackQueues
            .Include(q => q.Tracks)
            .FirstOrDefaultAsync(q => q.Id == queueId);
    }
    // could make async and have var queuse = await dbcontext... then reutrn queues but we arent doing anything after so
    public Task<List<TrackQueue>> GetAllQueuesAsync()
    {
        return _dbContext.TrackQueues
            .Include(q => q.Tracks)
            .ToListAsync();
    }
    public async Task AddQueueAsync(TrackQueue queue)
    {
        await _dbContext.TrackQueues.AddAsync(queue);
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveTrackFromQueueAsync(int queueId, int trackId)
    {
        var queue = await _dbContext.TrackQueues
            .Include(q => q.Tracks)
            .FirstOrDefaultAsync(q => q.Id == queueId);
        if (queue == null)
        {
            throw new Exception($"Queue {queueId} not found");
        }

        var track = queue.Tracks.FirstOrDefault(t => t.TrackId == trackId);
        if (track == null)
        {
            throw new Exception($"Track {track} not found");
        }

        queue.Tracks.Remove(track);
        await _dbContext.SaveChangesAsync();
    }

    public async Task ClearQueueAsync(int queueId)
    {
        var queue = await _dbContext.TrackQueues
            .Include(q => q.Tracks)
            .FirstOrDefaultAsync(q => q.Id == queueId);
        if (queue == null)
        {
            throw new Exception($"Queue: {queueId} not found.");
        }
        queue.Tracks.Clear();
        await _dbContext.SaveChangesAsync();
    }
}

