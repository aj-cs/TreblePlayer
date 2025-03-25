using ATL;
using ATL.AudioData;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TreblePlayer.Models.Metadata;
using TreblePlayer.Models;
using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;

namespace TreblePlayer.Services;

public class MetadataService : IMetadataService
{
    private readonly MusicPlayerDbContext _dbContext;
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public MetadataService(MusicPlayerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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
    public async Task ScanMusicFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Error: Folder '{folderPath}' does not exist.");
            return;
        }

        var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
            .ToList();

        foreach (var filePath in filePaths)
        {
            try
            {

                var trackMetadata = new ATL.Track(filePath); // ATL.NET reads metadata
                var newTrack = new TreblePlayer.Models.Track
                {
                    Title = trackMetadata.Title ?? Path.GetFileNameWithoutExtension(filePath),
                    Artist = trackMetadata.Artist ?? "Unknown Artist",
                    AlbumTitle = trackMetadata.Album ?? "Unknown Album",
                    DateCreated = System.DateTime.UtcNow,
                    LastModified = System.DateTime.UtcNow,
                    FilePath = filePath ?? "Unknown File",
                    Duration = trackMetadata.Duration,
                };

                // Check if album exists, otherwise create a new one
                var album = await _dbContext.Albums.FirstOrDefaultAsync(a => a.Title == newTrack.AlbumTitle);
                if (album == null)
                {
                    album = new Album
                    {
                        Title = newTrack.AlbumTitle ?? "Unknown Album",
                        AlbumArtist = newTrack.Artist ?? "Unknown Artist",
                        Genre = "Unknown Genre",
                        FolderPath = Path.GetDirectoryName(filePath) ?? "Unknown Folder",
                        Year = newTrack.Year ?? 0,
                        DateCreated = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        Tracks = new List<TreblePlayer.Models.Track>()
                    };
                    _dbContext.Albums.Add(album);
                }

                if (string.IsNullOrWhiteSpace(album.FolderPath))
                {
                    album.FolderPath = Path.GetDirectoryName(filePath) ?? "Unknown Folder";
                }

                if (!album.Tracks.Any(t => t.TrackId == newTrack.TrackId))
                    album.Tracks.Add(newTrack);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file '{filePath}': {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Music folder scan complete.");
    }
}
