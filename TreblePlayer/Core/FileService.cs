using TreblePlayer.Models;
using System.Threading.Tasks;
namespace TreblePlayer.Core;

public interface IFileService
{
    Task<Track> LoadTrackFromFilePathAsync(string filePath);
    Task<Album> LoadAlbumFromFolderAsync(string folderPath);
}

public class FileService : IFileService
{
    private readonly IMetadataService _metadataService;

    public FileService(IMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    public async Task<Album> LoadAlbumFromFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"No directory found at {folderPath}");
        }

        var trackFiles = Directory.GetFiles(folderPath)
            //.Where(f => IsSupportedFile(f))
            .ToList();

        if (!trackFiles.Any())
        {
            throw new Exception("No audio files found in the folder.");
        }
        var albumMetadata = await _metadataService.GetAlbumMetadataAsync(trackFiles);

        var album = new Album
        {
            Title = albumMetadata.Title,
            AlbumArtist = albumMetadata.AlbumArtist,
            Genre = albumMetadata.Genre,
            Year = albumMetadata.Year,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FolderPath = folderPath,
            Tracks = new List<Track>()
        };

        foreach (var filePath in trackFiles)
        {
            var trackMetadata = await _metadataService.GetTrackMetadataAsync(filePath);

            var track = new Track
            {
                Title = trackMetadata.Title,
                Artist = trackMetadata.Artist,
                Genre = trackMetadata.Genre,
                Year = trackMetadata.Year,
                Duration = trackMetadata.Duration,
                DateCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                FilePath = trackMetadata.FilePath
            };
            album.AddTrack(track);
        }

        return album;


    }
}

