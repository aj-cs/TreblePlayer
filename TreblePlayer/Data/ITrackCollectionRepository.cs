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


    Task<List<ITrackCollection>> GetCollectionsByTitleAsync(string title);
    Task<List<ITrackCollection>> GetCollectionsByTrackAsync(int trackId);
    //Task<ICollection<ITrackCollection>> GetUserCollectionsAsync(int userId);

    Task UpdateCollectionAsync(ITrackCollection collection);

    // Task SaveChangesAsync();

    Task<string> GetCollectionTitleByIdAsync(int collectionId, TrackCollectionType type);

    Task<Playlist?> GetPlaylistByIdAsync(int playlistId);
    Task<List<Playlist>> GetAllPlaylistsAsync();
    Task AddPlaylistAsync(Playlist playlist);
    Task RemovePlaylistAsync(int playlistId);
    Task AddTrackToPlaylistAsync(int playlistId, int trackId);
    Task RemoveTrackFromPlaylistAsync(int playlistId, int trackId);
}

