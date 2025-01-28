using TreblePlayer.Models;
namespace TreblePlayer.Data;

public interface ITrackCollectionRepository
{
    Task SaveAsync(ITrackCollection collection);
    Task AddQueueAsync(TrackQueue newQueue);
    Task<string> GetCollectionTitleByIdAsync(int collectionId, TrackCollectionType type);

    Task<ITrackCollection> GetTrackCollectionByIdAsync(int collectionId, TrackCollectionType collectionType); // returns collection with tracks list loaded in memory

    Task RemoveAlbumAndTracksAsync(Album album);
    Task RemoveCollectionFromDb(ITrackCollection collection);
    Task SaveChangesAsync();
}

