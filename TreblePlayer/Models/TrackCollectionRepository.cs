using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace TreblePlayer.Models;


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

            TrackCollectionType.Album => await _dbContext.Albums.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.Id == collectionId),
            TrackCollectionType.Playlist => await _dbContext.Playlists.Include(p => p.Tracks).FirstOrDefaultAsync(p => p.Id == collectionId),
            TrackCollectionType.TrackQueue => await _dbContext.TrackQueues.Include(q => q.Tracks).FirstOrDefaultAsync(q => q.Id == collectionId),
            _ => throw new ArgumentException("Unsupported track collection type.")
        };
    }

    public async Task AddQueueAsync(TrackQueue queue)
    {
        await _dbContext.TrackQueues.AddAsync(queue);
        await _dbContext.SaveChangesAsync();
    }
    public async Task SaveAsync(ITrackCollection collection)
    {
        _dbContext.Update(collection);
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
        var albums = await _dbContext.Albums.Where(a => a.Title.Contains(title)).ToListAsync<ITrackCollection>();
        var playlists = await _dbContext.Playlists.Where(a => a.Title.Contains(title)).ToListAsync<ITrackCollection>();
        var queues = await _dbContext.TrackQueues.Where(a => a.Title.Contains(title)).ToListAsync<ITrackCollection>();
        return albums.Concat(playlists).Concat(queues).ToList();
    }
    public async Task<List<ITrackCollection>> GetCollectionsByTrackAsync(int trackId)
    {
        var albums = await _dbContext.Albums.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync<ITrackCollection>();
        var playlists = await _dbContext.Playlists.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync<ITrackCollection>();
        var queues = await _dbContext.TrackQueues.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync<ITrackCollection>();
        return albums.Concat(playlists).Concat(queues).ToList();
    }

    public async Task<Album> GetAlbumByIdAsync(int albumId)
    {
        return await _dbContext.Albums.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.Id == albumId);
    }

    public async Task<Playlist> GetPlaylistByIdAsync(int playlistId)
    {
        return await _dbContext.Playlists.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.Id == playlistId);
    }

    public async Task<TrackQueue> GetQueueByIdAsync(int queueId)
    {
        return await _dbContext.TrackQueues.Include(a => a.Tracks).FirstOrDefaultAsync(a => a.Id == queueId);
    }
    public async Task RemoveCollectionFromDb(ITrackCollection collection)
    {
        switch (collection.CollectionType)
        {
            case TrackCollectionType.Album:
                _dbContext.Albums.Remove((Album)collection);
                break;
            case TrackCollectionType.Playlist:
                _dbContext.Playlists.Remove((Playlist)collection);
                break;
            case TrackCollectionType.TrackQueue:
                _dbContext.TrackQueues.Remove((TrackQueue)collection);
                break;
            default:
                throw new ArgumentException("Unsupported Track Collection");
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveAlbumAndTracksAsync(Album album)
    {
        // remove the tracks
        _dbContext.Tracks.RemoveRange(album.Tracks);

        // remove the album 
        _dbContext.Albums.Remove(album);

        await SaveChangesAsync();
    }

    public async Task UpdateCollectionAsync(ITrackCollection collection)
    {
        _dbContext.Update(collection);
        await _dbContext.SaveChangesAsync();
    }
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}

