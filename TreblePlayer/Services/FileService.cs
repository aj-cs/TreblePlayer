using TreblePlayer.Models;
using System.Threading.Tasks;
using TreblePlayer.Services;
using System.Linq;

namespace TreblePlayer.Services;

public interface IFileService
{
    Task<Track> LoadTrackFromFilePathAsync(string filePath);
    Task<Album> LoadAlbumFromFolderAsync(string folderPath);
}

public class FileService : IFileService
{
    private readonly IMetadataService _metadataService;
    private readonly ILoggingService _logger;

    public FileService(IMetadataService metadataService, ILoggingService logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    public async Task<Track> LoadTrackFromFilePathAsync(string filePath)
    {
        try
        {
            _logger.LogInformation($"Loading track from file: {filePath}");
            
            if (!File.Exists(filePath))
            {
                _logger.LogError($"No file found at {filePath}");
                throw new FileNotFoundException($"No file found at {filePath}");
            }

            var trackMetadata = await _metadataService.GetTrackMetadataFromFileAsync(filePath);

            var track = new Track
            {
                Title = trackMetadata.Title ?? "Unknown Title",
                Artist = trackMetadata.Artist ?? "Unknown Artist",
                Genre = trackMetadata.Genre ?? "Unknown Genre",
                DateCreated = System.DateTime.UtcNow,
                LastModified = System.DateTime.UtcNow,
                FilePath = filePath ?? "Unknown File",
                Year = trackMetadata.Year ?? 2000,
                Duration = trackMetadata.Duration,
                AlbumTitle = trackMetadata.Album ?? "Unknown Album"
            };
            
            _logger.LogInformation($"Successfully loaded track: {track.Title} by {track.Artist}");
            return track;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading track from {filePath}", ex);
            throw;
        }
    }

    public async Task<Album> LoadAlbumFromFolderAsync(string folderPath)
    {
        try
        {
            _logger.LogInformation($"Loading album from folder: {folderPath}");
            
            if (!Directory.Exists(folderPath))
            {
                _logger.LogError($"No directory found at {folderPath}");
                throw new DirectoryNotFoundException($"No directory found at {folderPath}");
            }

            var trackFiles = Directory.GetFiles(folderPath)
                .OrderBy(path => path)
                .ToList();

            if (!trackFiles.Any())
            {
                _logger.LogWarning($"No audio files found in folder: {folderPath}");
                throw new Exception("No audio files found in the folder.");
            }

            _logger.LogInformation($"Found {trackFiles.Count} files in folder");
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

            _logger.LogInformation($"Processing tracks for album: {album.Title}");
            foreach (var filePath in trackFiles)
            {
                try
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
                        TrackNumber = trackMetadata.TrackNumber,
                        FilePath = trackMetadata.FilePath ?? "Unknown File"
                    };
                    album.AddTrack(track);
                    _logger.LogDebug($"Added track to album: {track.Title}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing track file: {filePath}", ex);
                    // Continue processing other tracks even if one fails
                }
            }

            // Order tracks by TrackNumber before returning
            album.Tracks = album.Tracks.OrderBy(t => t.TrackNumber).ToList();

            _logger.LogInformation($"Successfully loaded album: {album.Title} with {album.Tracks.Count} tracks");
            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading album from {folderPath}", ex);
            throw;
        }
    }
}

