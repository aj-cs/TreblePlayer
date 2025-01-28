using ATL;
using ATL.AudioData;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TreblePlayer.Models.Metadata;

namespace TreblePlayer.Core;


public class MetadataService : IMetadataService
{
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public async Task<List<TrackMetadata>> GetTrackMetadataFromFolderAsync(string folderPath)
    {
        var filePaths = Directory.GetFiles(folderPath, "*,*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()));
        var metadataTasks = filePaths.Select(filePath => TrackMetadata.CreateAsync(filePath));

        var trackMetadata = await Task.WhenAll(metadataTasks);

        return trackMetadata.ToList();
    }

    public async Task<AlbumMetadata> GetAlbumMetadataAsync(IEnumerable<string> trackFilePaths)
    {

        return await AlbumMetadata.CreateAsync(trackFilePaths);
    }

    public async Task<AlbumMetadata> GetAlbumMetadataFromFolderAsync(string folderPath)
    {
        var trackMetadata = await GetTrackMetadataFromFolderAsync(folderPath);
        var trackFilePaths = trackMetadata.Select(t => t.FilePath);

        return await GetAlbumMetadataAsync(trackFilePaths);
    }

    public async Task<List<TrackMetadata>> GetTracksByAlbumAsync(string folderPath, string artistName)
    {
        var allTracks = await GetTrackMetadataFromFolderAsync(folderPath);
        return allTracks.Where(t => t.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
    }

    public async Task<TrackMetadata> GetTrackMetadataFromFileAsync(string filePath)
    {
        return await TrackMetadata.CreateAsync(filePath);
    }
}
