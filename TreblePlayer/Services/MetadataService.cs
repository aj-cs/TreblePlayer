using ATL;
using ATL.AudioData;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TreblePlayer.Models.Metadata;
using TreblePlayer.Models;
using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;

namespace TreblePlayer.Services;

public class MetadataService : IMetadataService
{
    private readonly MusicPlayerDbContext _dbContext;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly ILoggingService _logger;
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public MetadataService(
        MusicPlayerDbContext dbContext,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        ILoggingService logger)
    {
        _dbContext = dbContext;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _logger = logger;
    }

    public async Task<List<TrackMetadata>> GetTrackMetadataFromFolderAsync(string folderPath)
    {
        try
        {
            _logger.LogInformation($"Getting track metadata from folder: {folderPath}");

            if (!Directory.Exists(folderPath))
            {
                _logger.LogError($"Folder not found: {folderPath}");
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            var filePaths = Directory.GetFiles(folderPath, "*,*", SearchOption.AllDirectories)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            _logger.LogInformation($"Found {filePaths.Count} audio files in folder");

            var metadataTasks = filePaths.Select(filePath => TrackMetadata.CreateAsync(filePath));
            var trackMetadata = await Task.WhenAll(metadataTasks);

            _logger.LogInformation($"Successfully processed {trackMetadata.Length} tracks");
            return trackMetadata.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting track metadata from folder: {folderPath}", ex);
            throw;
        }
    }

    public async Task<AlbumMetadata> GetAlbumMetadataAsync(IEnumerable<string> trackFilePaths)
    {
        try
        {
            _logger.LogInformation($"Getting album metadata for {trackFilePaths.Count()} tracks");
            var metadata = await AlbumMetadata.CreateAsync(trackFilePaths);
            _logger.LogInformation($"Successfully retrieved album metadata: {metadata.Title}");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting album metadata", ex);
            throw;
        }
    }

    public async Task<AlbumMetadata> GetAlbumMetadataFromFolderAsync(string folderPath)
    {
        try
        {
            _logger.LogInformation($"Getting album metadata from folder: {folderPath}");
            var trackMetadata = await GetTrackMetadataFromFolderAsync(folderPath);
            var trackFilePaths = trackMetadata.Select(t => t.FilePath);
            var albumMetadata = await GetAlbumMetadataAsync(trackFilePaths);
            _logger.LogInformation($"Successfully retrieved album metadata from folder: {albumMetadata.Title}");
            return albumMetadata;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting album metadata from folder: {folderPath}", ex);
            throw;
        }
    }

    public async Task<List<TrackMetadata>> GetTracksByAlbumAsync(string folderPath, string artistName)
    {
        try
        {
            _logger.LogInformation($"Getting tracks by album for artist: {artistName} in folder: {folderPath}");
            var allTracks = await GetTrackMetadataFromFolderAsync(folderPath);
            var filteredTracks = allTracks.Where(t => t.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            _logger.LogInformation($"Found {filteredTracks.Count} tracks for artist: {artistName}");
            return filteredTracks;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting tracks by album for artist: {artistName}", ex);
            throw;
        }
    }

    public async Task<TrackMetadata> GetTrackMetadataFromFileAsync(string filePath)
    {
        try
        {
            _logger.LogInformation($"Getting track metadata for file: {filePath}");

            if (!File.Exists(filePath))
            {
                _logger.LogError($"No file found at {filePath}");
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var metadata = await TrackMetadata.CreateAsync(filePath);
            _logger.LogInformation($"Successfully retrieved track metadata: {metadata.Title}");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting track metadata for file: {filePath}", ex);
            throw;
        }
    }
    public async Task ScanMusicFromDirectoryAsync(List<string> directories)
    {
        try
        {
            _logger.LogInformation($"Starting music scan from {directories.Count} directories");

            // First, collect all folders concurrently
            var allFolders = new ConcurrentBag<string>();
            var folderTasks = directories.Select(async directory =>
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        _logger.LogWarning($"Directory not found: {directory}");
                        return;
                    }

                    var folders = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);
                    foreach (var folder in folders)
                    {
                        allFolders.Add(folder);
                    }
                    _logger.LogInformation($"Found {folders.Length} folders in {directory}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error scanning directory {directory}: {ex.Message}");
                }
            });
            await Task.WhenAll(folderTasks);

            _logger.LogInformation($"Found total of {allFolders.Count} folders to scan");

            // batches to avoid overwhelming the database
            var batchSize = 5;
            var folderList = allFolders.ToList();

            for (int i = 0; i < folderList.Count; i += batchSize)
            {
                var batch = folderList.Skip(i).Take(batchSize);
                // could one line it without debug
                // var scanTasks = batch.Select(folder => ScanMusicFolderAsync(folder));
                var scanTasks = batch.Select(async folder =>
                {
                    try
                    {
                        await ScanMusicFolderAsync(folder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error scanning folder {folder}: {ex.Message}");
                    }
                });
                await Task.WhenAll(scanTasks);

                // add a small delay between batches to prevent any possible
                // database stress
                if (i + batchSize < folderList.Count)
                {
                    await Task.Delay(100);
                }
            }

            _logger.LogInformation("Completed scanning all directories");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ScanMusicFromDirectoryAsync: {ex.Message}");
            throw;
        }
    }
    public async Task ScanMusicFolderAsync(string folderPath)
    {
        try
        {
            _logger.LogInformation($"Starting music folder scan: {folderPath}");

            if (!Directory.Exists(folderPath))
            {
                _logger.LogError($"Error: Folder '{folderPath}' does not exist.");
                return;
            }

            var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            _logger.LogInformation($"Found {filePaths.Count} audio files to process");

            var processedCount = 0;
            var skippedCount = 0;
            var errorCount = 0;

            foreach (var filePath in filePaths)
            {
                if (await _dbContext.Tracks.AnyAsync(t => t.FilePath == filePath))
                {
                    skippedCount++;
                    continue;
                }
                try
                {
                    _logger.LogDebug($"Processing file: {filePath}");
                    var metadata = new ATL.Track(filePath);
                    var newTrack = new Models.Track
                    {
                        Title = metadata.Title ?? Path.GetFileNameWithoutExtension(filePath),
                        Artist = metadata.Artist ?? "Unknown Artist",
                        AlbumTitle = metadata.Album ?? "Unknown Album",
                        Bitrate = metadata.Bitrate,
                        Year = metadata.Year ?? null,
                        Genre = metadata.Genre ?? "Unknown Genre",
                        TrackNumber = metadata.TrackNumber,
                        DateCreated = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow,
                        FilePath = filePath,
                        Duration = metadata.Duration,
                    };
                    var album = await GetOrCreateAlbumAsync(newTrack, filePath);
                    album.Tracks.Add(newTrack);
                    await _dbContext.SaveChangesAsync();
                    processedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError($"Error processing '{filePath}': {ex.Message}");
                }
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Music folder scan complete. Processed: {processedCount}, Skipped: {skippedCount}, Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error scanning music folder: {folderPath}", ex);
            throw;
        }
    }

    public async Task<Album> GetOrCreateAlbumAsync(Models.Track track, string filePath)
    {
        try
        {
            _logger.LogInformation($"Getting or creating album for track: {track.Title}");

            var folder = Path.GetDirectoryName(filePath);
            var album = await _dbContext.Albums
                .Include(a => a.Tracks)
                .FirstOrDefaultAsync(a =>
                        a.Title == track.AlbumTitle &&
                        a.AlbumArtist == track.Artist &&
                        a.FolderPath == folder);

            if (album != null)
            {
                _logger.LogInformation($"Found existing album: {album.Title}");
                return album;
            }

            // _dbContext.Entry(existingCollection).CurrentValues.SetValues(collection);
            _logger.LogInformation($"Creating new album: {track.AlbumTitle}");
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
            _logger.LogInformation($"Successfully created new album: {album.Title}");
            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/creating album for track: {track.Title}", ex);
            throw;
        }
    }
}
