using TreblePlayer.Models;
using System.Threading.Tasks;
namespace TreblePlayer.Services;

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

    public async Task<Track> LoadTrackFromFilePathAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"No file found at {filePath}");
        }

        var trackMetadata = await _metadataService.GetTrackMetadataFromFileAsync(filePath);

        var track = new Track
        {
            Title = trackMetadata.Title ?? "Unknown Title",
            Artist = trackMetadata.Artist ?? "Unknown Artist",
            Genre = trackMetadata.Genre ?? "Unknown Genre",
            // Bitrate = trackMetadata.Bitrate,
            DateCreated = System.DateTime.UtcNow,
            LastModified = System.DateTime.UtcNow,
            FilePath = filePath ?? "Unknown File",
            Year = trackMetadata.Year ?? 2000,
            Duration = trackMetadata.Duration,
            AlbumTitle = trackMetadata.Album ?? "Unknown Album"
        };
        return track;
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
            Title = albumMetadata.Title ?? "Unknown Title",
            AlbumArtist = albumMetadata.AlbumArtist ?? "Unknown Artist",
            Genre = albumMetadata.Genre ?? "Unknown Genre",
            Year = albumMetadata.Year ?? 2000,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            FolderPath = folderPath ?? "Unknown Folder",
            Tracks = new List<Track>()
        };

        foreach (var filePath in trackFiles)
        {
            var trackMetadata = await _metadataService.GetTrackMetadataFromFileAsync(filePath);

            var track = new Track
            {
                Title = trackMetadata.Title ?? "Unknown Title",
                Artist = trackMetadata.Artist ?? "Unknown Artist",
                Genre = trackMetadata.Genre ?? "Unknown Genre",
                Year = trackMetadata.Year ?? 200,
                Duration = trackMetadata.Duration,
                DateCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
                FilePath = trackMetadata.FilePath ?? "Unknown File"
            };
            album.AddTrack(track);
        }
        return album;
    }
}

