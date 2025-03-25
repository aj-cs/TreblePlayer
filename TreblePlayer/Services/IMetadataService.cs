using TreblePlayer.Models.Metadata;
namespace TreblePlayer.Services;
public interface IMetadataService
{
    //#Task<bool> IsSupportedFile(string filePath);

    Task<TrackMetadata> GetTrackMetadataFromFileAsync(string filePath);
    Task<List<TrackMetadata>> GetTrackMetadataFromFolderAsync(string folderPath);

    Task<AlbumMetadata> GetAlbumMetadataAsync(IEnumerable<string> trackFilePaths);
    Task<AlbumMetadata> GetAlbumMetadataFromFolderAsync(string folderPath);

    Task<List<TrackMetadata>> GetTracksByAlbumAsync(string folderPath, string albumTitle);

    Task ScanMusicFolderAsync(string folderPath);

    //Task<List<TrackMetadata>> GetTracksByArtistAsync(string folderPath, string artistName);
    //Task<List<string>> GetDistinctArtistsAsync(string folderPath);
    //Task<List<string>> GetDistinctAlbumsAsync(string folderPath);

}
