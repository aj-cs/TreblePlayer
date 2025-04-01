using TreblePlayer.Models;
namespace TreblePlayer.Data;

public interface ITrackCollectionRepository
{
    Task SaveAsync(ITrackCollection collection);

    Task RemoveAlbumAndTracksAsync(Album album);
    Task RemoveCollectionFromDb(ITrackCollection collection);


    Task<ITrackCollection> GetTrackCollectionByIdAsync(int collectionId, TrackCollectionType collectionType); // returns collection with tracks list loaded in memory

    Task<Album> GetAlbumByIdAsync(int albumId);

    Task<TrackQueue> GetQueueByIdAsync(int queueId);
    Task AddQueueAsync(TrackQueue newQueue);
    Task<List<TrackQueue>> GetAllQueuesAsync();
    Task RemoveTrackFromQueueAsync(int queueId, int trackId);
    Task ClearQueueAsync(int queueId);

    Task<Playlist> GetPlaylistByIdAsync(int playlistId);

    Task<List<ITrackCollection>> GetCollectionsByTitleAsync(string title);
    Task<List<ITrackCollection>> GetCollectionsByTrackAsync(int trackId);
    //Task<ICollection<ITrackCollection>> GetUserCollectionsAsync(int userId);

    Task UpdateCollectionAsync(ITrackCollection collection);

    // Task SaveChangesAsync();
}

