using TreblePlayer.Models;
namespace TreblePlayer.Data;

public interface ITrackRepository
{
    Task<IEnumerable<Track>> GetTracksByIdAsync(IEnumerable<int> trackIds);
    Task<Track> GetTrackByIdAsync(int trackId);
    Task RemoveTracksFromDb(ICollection<Track> tracks);
    Task<IEnumerable<Track>> GetTracksByAlbumIdAsync(int albumId);
    Task<IEnumerable<Track>> GetTracksByPlaylistIdAsync(int playlistId);
    Task<bool> AddOrUpdateTrackAsync(Track track);
    Task<IEnumerable<Track>> GetAllTracksAsync();
    Task<Track?> GetTrackByFilePathAsync(string filePath);
}
