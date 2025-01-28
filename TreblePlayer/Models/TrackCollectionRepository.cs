using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;
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
        switch (collectionType)
        {
            case TrackCollectionType.Album:
                return await _dbContext.Albums
                    .Include(x => x.Tracks)
                    .FirstOrDefaultAsync(x => x.Id == collectionId);
            case TrackCollectionType.Playlist:
                return await _dbContext.Playlists
                    .Include(x => x.Tracks)
                    .FirstOrDefaultAsync(x => x.Id == collectionId);
            case TrackCollectionType.TrackQueue:
                return await _dbContext.TrackQueues
                    .Include(x => x.Tracks)
                    .FirstOrDefaultAsync(x => x.Id == collectionId);
            default:
                throw new ArgumentException("Unsupported track collection type.");
        }
    }

    public async Task AddQueueAsync(TrackQueue queue)
    {
        await _dbContext.TrackQueues.AddAsync(queue);
        await _dbContext.SaveChangesAsync();
    }
    public async Task SaveAsync(ITrackCollection collection)
    {
        _dbContext.Update(collection);
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

    public async Task RemoveCollectionFromDb(ITrackCollection collection)
    {
        _dbContext.Remove(collection);
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
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}

