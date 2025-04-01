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
            if (await _dbContext.Tracks.AnyAsync(t => t.FilePath == filePath))
            {
                continue;
            }
            try
            {
                var metadata = new ATL.Track(filePath);
                var newTrack = new Models.Track
                {
                    Title = metadata.Title ?? Path.GetFileNameWithoutExtension(filePath),
                    Artist = metadata.Artist ?? "Unknown Artist",
                    AlbumTitle = metadata.Album ?? "Unknown Album",
                    Bitrate = metadata.Bitrate,
                    Year = metadata.Year ?? null,
                    Genre = metadata.Genre ?? "Unknown Genre",
                    DateCreated = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow,
                    FilePath = filePath,
                    Duration = metadata.Duration,
                };
                var album = await GetOrCreateAlbumAsync(newTrack, filePath);
                album.Tracks.Add(newTrack);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing '{filePath}': {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
        Console.WriteLine("Music folder scan complete.");
    }

    public async Task<Album> GetOrCreateAlbumAsync(Models.Track track, string filePath)
    {
        try
        {
            var folder = Path.GetDirectoryName(filePath);
            var album = await _dbContext.Albums
                .Include(a => a.Tracks)
                .FirstOrDefaultAsync(a =>
                        a.Title == track.AlbumTitle &&
                        a.AlbumArtist == track.Artist &&
                        a.FolderPath == folder);

            if (album != null)
            {
                return album;
            }
            album = new Album
            {
                Title = track.AlbumTitle ?? "Unknown Album",
                AlbumArtist = track.Artist ?? "Unknown Artist",
                Genre = track.Genre ?? "Unknown Genre",
                FolderPath = folder,
                Year = track.Year ?? null,
                DateCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
            };
            _dbContext.Albums.Add(album);
            return album;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"No file found at {filePath}");
            throw;
        }
    }
}
