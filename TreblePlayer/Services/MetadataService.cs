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
using Microsoft.AspNetCore.SignalR;
using TreblePlayer.Core;

namespace TreblePlayer.Services;

public class MetadataService : IMetadataService
{
    private readonly MusicPlayerDbContext _dbContext;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly IArtworkService _artworkService;
    private readonly ILoggingService _logger;
    private readonly IHubContext<DataHub> _dataHub;
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public MetadataService(
        MusicPlayerDbContext dbContext,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        IArtworkService artworkService,
        ILoggingService logger,
        IHubContext<DataHub> dataHub)
    {
        _dbContext = dbContext;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _artworkService = artworkService;
        _logger = logger;
        _dataHub = dataHub;
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

            await _dataHub.Clients.All.SendAsync("LibraryUpdated");
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
                    
                    // Get the normalized artist name
                    string artistName = metadata.Artist ?? "Unknown Artist";
                    string fullArtistString;
                    string collaborators;
                    string featuredArtists;
                    string normalizedArtist = NormalizeArtistName(artistName, out fullArtistString, out collaborators, out featuredArtists);
                    
                    var newTrack = new Models.Track
                    {
                        Title = metadata.Title ?? Path.GetFileNameWithoutExtension(filePath),
                        Artist = artistName, // Keep original artist name for track
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
                    
                    // The GetOrCreateAlbumAsync method now handles normalization
                    var album = await GetOrCreateAlbumAsync(newTrack, filePath);
                    album.Tracks.Add(newTrack);

                    // Set artwork after adding the track to the album's collection
                    await _artworkService.SetAlbumArtworkAsync(album);
                    await _artworkService.SetTrackArtworkAsync(newTrack);

                    await _dbContext.SaveChangesAsync(); // Save track, album, and artwork paths
                    processedCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError($"Error processing '{filePath}': {ex.Message}");
                }
            }

            _logger.LogInformation($"Music folder scan complete. Processed: {processedCount}, Skipped: {skippedCount}, Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error scanning music folder: {folderPath}", ex);
            throw;
        }
    }

    // Enhanced method for normalizing artist names with context-aware parsing
    private string NormalizeArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists)
    {
        if (string.IsNullOrEmpty(artistName))
        {
            fullArtistString = "Unknown Artist";
            collaborators = string.Empty;
            featuredArtists = string.Empty;
            return fullArtistString;
        }

        // Store the original artist name
        fullArtistString = artistName.Trim();
        collaborators = string.Empty;
        featuredArtists = string.Empty;
        
        // Special cases - known artist names that contain commas or ampersands
        // This list could be expanded or moved to a configuration file
        string[] knownArtistsWithCommas = new[] 
        { 
            "tyler, the creator",
            "earth, wind & fire",
            "crosby, stills",
            "crosby, stills & nash",
            "crosby, stills, nash & young",
            "rob base & dj e-z rock"
        };

        // Check if the artist name (case insensitive) is in our list of exceptions
        if (knownArtistsWithCommas.Any(a => artistName.Trim().ToLowerInvariant().Equals(a)))
        {
            // This is a known artist with commas or ampersands, return as is
            return artistName.Trim();
        }

        // List of patterns that explicitly indicate featuring artists
        string[] featuringPatterns = new[]
        {
            " featuring ",
            " ft. ",
            " ft ",
            " feat. ",
            " feat "
        };

        // Find the first occurrence of any featuring pattern
        int firstFeaturingIndex = -1;
        string matchedPattern = null;
        
        foreach (var pattern in featuringPatterns)
        {
            int index = artistName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && (firstFeaturingIndex == -1 || index < firstFeaturingIndex))
            {
                firstFeaturingIndex = index;
                matchedPattern = pattern;
            }
        }

        // If a featuring pattern was found, separate primary artist from featured artists
        if (firstFeaturingIndex > 0 && matchedPattern != null)
        {
            featuredArtists = artistName.Substring(firstFeaturingIndex + matchedPattern.Length).Trim();
            return artistName.Substring(0, firstFeaturingIndex).Trim();
        }

        // Handle potential collaboration with "&" (if not part of a known artist name)
        if (artistName.Contains(" & ") && !knownArtistsWithCommas.Any(a => artistName.ToLowerInvariant().Contains(a)))
        {
            // Check if this looks like a legitimate collaboration
            var parts = artistName.Split(new[] { " & " }, StringSplitOptions.None);
            
            // If we have exactly two artists and both have reasonable length (to avoid splitting on "&" in a single name)
            if (parts.Length == 2 && parts.All(p => p.Trim().Length > 2))
            {
                collaborators = parts[1].Trim();
                return parts[0].Trim(); // Return first artist as primary
            }
        }

        // Handle potential collaboration with "," (if not part of a known artist name)
        if (artistName.Contains(",") && !knownArtistsWithCommas.Any(a => artistName.ToLowerInvariant().Contains(a)))
        {
            // Check for "the" after comma which would indicate it's likely part of a single name
            if (artistName.ToLowerInvariant().Contains(", the "))
            {
                return artistName.Trim(); // This is likely a single artist name like "Tyler, the Creator"
            }
            
            // Otherwise, treat as collaborators
            var parts = artistName.Split(',');
            
            // If we have multiple artists and they have reasonable length
            if (parts.Length > 1 && parts.All(p => p.Trim().Length > 2))
            {
                collaborators = string.Join(", ", parts.Skip(1).Select(p => p.Trim()));
                return parts[0].Trim(); // Return first artist as primary
            }
        }

        // No special patterns found, return the original name
        return artistName.Trim();
    }

    public async Task<Album> GetOrCreateAlbumAsync(Models.Track track, string filePath)
    {
        try
        {
            _logger.LogInformation($"Getting or creating album for track: {track.Title}");

            // Normalize the artist name with enhanced context-aware parsing
            string fullArtistString;
            string collaborators;
            string featuredArtists;
            string primaryArtist = NormalizeArtistName(track.Artist, out fullArtistString, out collaborators, out featuredArtists);

            var folder = Path.GetDirectoryName(filePath);
            var album = await _dbContext.Albums
                .Include(a => a.Tracks)
                .FirstOrDefaultAsync(a =>
                        a.Title == track.AlbumTitle &&
                        (a.AlbumArtist == primaryArtist || a.AlbumArtist == track.Artist) &&
                        a.FolderPath == folder);

            if (album != null)
            {
                _logger.LogInformation($"Found existing album: {album.Title}");
                // NOTE: maybe later add logic here to check/update artwork for existing albums if needed
                return album;
            }

            _logger.LogInformation($"Creating new album: {track.AlbumTitle}");
            
            // Construct rich metadata string preserving the artist information
            string enrichedGenre = track.Genre ?? "Unknown Genre";
            
            // Build rich metadata with detailed artist information
            if (fullArtistString != primaryArtist)
            {
                var metadataParts = new List<string>();
                
                if (!string.IsNullOrEmpty(collaborators))
                    metadataParts.Add($"Collaborators: {collaborators}");
                    
                if (!string.IsNullOrEmpty(featuredArtists))
                    metadataParts.Add($"Featured: {featuredArtists}");
                    
                if (metadataParts.Any())
                    enrichedGenre = $"{enrichedGenre} ({string.Join(" | ", metadataParts)})";
            }
            
            album = new Album
            {
                Title = track.AlbumTitle ?? "Unknown Album",
                AlbumArtist = primaryArtist, // Use normalized artist name
                Genre = enrichedGenre, // Store collaborators and featured artists here temporarily
                FolderPath = folder,
                Year = track.Year ?? null,
                DateCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow,
            };
            _dbContext.Albums.Add(album);
            // Artwork is handled in ScanMusicFolderAsync after this returns
            _logger.LogInformation($"Successfully created new album: {album.Title}");
            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting/creating album for track: {track.Title}", ex);
            throw;
        }
    }

    public async Task ProcessFileSystemChangesAsync(List<string> pathsToProcess)
    {
        _logger.LogInformation($"Processing {pathsToProcess.Count} file system changes triggered by watcher.");
        bool anyDbChangesMade = false;

        foreach (var path in pathsToProcess)
        {
            try
            {
                // --- Case 1: Path exists and is a music file (Created or Modified) ---
                if (File.Exists(path) && IsMusicFile(path))
                {
                    _logger.LogInformation($"Processing changed/added file: {path}");
                    // Extract metadata for the specific file
                    // Use your existing ATL/FileService logic (adapted from ScanMusicFolderAsync)
                    var metadata = new ATL.Track(path); // Assuming ATL is used directly here for simplicity
                    
                    // Get original and normalized artist name
                    string artistName = metadata.Artist ?? "Unknown Artist";
                    string fullArtistString;
                    string collaborators;
                    string featuredArtists;
                    string normalizedArtist = NormalizeArtistName(artistName, out fullArtistString, out collaborators, out featuredArtists);
                    
                    var trackData = new Models.Track // Create a Track model instance
                    {
                        Title = metadata.Title ?? Path.GetFileNameWithoutExtension(path),
                        Artist = artistName, // Use original artist name
                        AlbumTitle = metadata.Album ?? "Unknown Album",
                        Bitrate = metadata.Bitrate,
                        Year = metadata.Year ?? null,
                        Genre = metadata.Genre ?? "Unknown Genre",
                        TrackNumber = metadata.TrackNumber,
                        DateCreated = DateTime.UtcNow, // Or get from file system?
                        LastModified = DateTime.UtcNow,
                        FilePath = path,
                        Duration = metadata.Duration,
                    };

                    // Find or create the album for this track
                    var album = await GetOrCreateAlbumAsync(trackData, path); // Now uses normalized artist name
                    trackData.AlbumId = album.Id; // Link track to album ID

                    // Add or update the track in the repository
                    bool changed = await _trackRepository.AddOrUpdateTrackAsync(trackData);

                    if (changed)
                    {
                        // If track was added/updated, ensure artwork exists
                        await _artworkService.SetAlbumArtworkAsync(album); // Might be redundant if album existed, but safe
                        await _artworkService.SetTrackArtworkAsync(trackData); // Update track artwork if needed
                        anyDbChangesMade = true;
                        _logger.LogInformation($"DB change processed for file: {path}");
                    }
                }
                // --- Case 2: Path does not exist (Deleted) ---
                else if (!File.Exists(path) && !Directory.Exists(path)) // Ensure it's not just a directory change
                {
                    _logger.LogInformation($"Processing potentially deleted path: {path}");

                    // Check if a track with this exact file path existed
                    var trackToDelete = await _trackRepository.GetTrackByFilePathAsync(path);
                    if (trackToDelete != null)
                    {
                        _logger.LogInformation($"Removing track for deleted file: {path} (Track ID: {trackToDelete.TrackId})");
                        try
                        {
                            await _trackRepository.RemoveTracksFromDb(new List<Models.Track> { trackToDelete });
                            // Check if the album/artist associated with this track is now empty
                            await _collectionRepository.CleanupEmptyCollectionsAsync();
                            anyDbChangesMade = true;
                             _logger.LogInformation($"Successfully removed track and potentially cleaned collections for: {path}");
                        }
                        catch(Exception dbEx)
                        {
                            _logger.LogError($"Error removing track or cleaning collections for {path}: {dbEx.Message}", dbEx);
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Path does not exist and no track matched exactly: {path}. No DB action taken.");
                    }
                }
                 // --- Case 3: Path exists and is a Directory (Created/Renamed) ---
                 else if (Directory.Exists(path))
                 {
                    _logger.LogInformation($"Directory change detected (Created/Renamed): {path}. Performing automatic scan.");
                    
                    // Automatically scan the directory when it changes
                    try
                    {
                        await ScanMusicFromDirectoryAsync(new List<string> { path });
                        _logger.LogInformation($"Completed automatic scan of changed directory: {path}");
                        anyDbChangesMade = true; // Set to true since we've made changes
                    }
                    catch (Exception scanEx)
                    {
                        _logger.LogError($"Error during automatic scan of changed directory: {path}", scanEx);
                    }
                 }
                 // --- Case 4: Path exists but is not a music file ---
                 else if (File.Exists(path) && !IsMusicFile(path))
                 {
                      _logger.LogInformation($"Ignoring non-music file change: {path}");
                 }
                 else
                 {
                     _logger.LogWarning($"Unhandled file system change type for path: {path}");
                 }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing file system change for path {path}: {ex.Message}", ex);
                // Continue processing other paths
            }
        } // End foreach path

        // --- Send Notification AFTER processing all detected changes in the batch ---
        if (anyDbChangesMade)
        {
            _logger.LogInformation("File system change processing complete. Sending LibraryUpdated notification.");
            // Use the correct HubContext field name (_musicDataHubContext or _dataHub)
            await _dataHub.Clients.All.SendAsync("LibraryUpdated");
        }
        else
        {
            _logger.LogInformation("File system change processing complete. No database changes resulted from this batch.");
        }
    }

    // Helper method (already present in your code, ensure it's accessible)
    private bool IsMusicFile(string path)
    {
        var extension = Path.GetExtension(path)?.ToLowerInvariant();
        // Ensure your list is correct (.m4a not .m3a?)
        return extension == ".mp3" || extension == ".flac" || extension == ".alac" || extension == ".opus" || extension == ".wav" || extension == ".aac" || extension == ".ogg";
    }

    // Updated method to normalize existing artist names with enhanced parsing
    public async Task NormalizeExistingArtistNames()
    {
        try
        {
            _logger.LogInformation("Starting enhanced normalization of existing artist names in albums...");
            
            // Get all albums from the database
            var allAlbums = await _dbContext.Albums.ToListAsync();
            
            int updatedCount = 0;
            
            foreach (var album in allAlbums)
            {
                string originalArtist = album.AlbumArtist;
                string fullArtistString;
                string collaborators;
                string featuredArtists;
                string normalizedArtist = NormalizeArtistName(originalArtist, out fullArtistString, out collaborators, out featuredArtists);
                
                // If normalization changed the artist name, update the album
                if (normalizedArtist != originalArtist)
                {
                    _logger.LogInformation($"Normalizing album artist from '{originalArtist}' to '{normalizedArtist}' for album '{album.Title}'");
                    album.AlbumArtist = normalizedArtist;
                    
                    // Build rich metadata string with detailed artist information
                    string enrichedGenre = album.Genre ?? "Unknown Genre";
                    
                    // Skip if genre already contains artist metadata
                    if (!enrichedGenre.Contains("(Collaborators:") && 
                        !enrichedGenre.Contains("(Featured:") && 
                        !enrichedGenre.Contains("(Full Artist:"))
                    {
                        var metadataParts = new List<string>();
                        
                        if (!string.IsNullOrEmpty(collaborators))
                            metadataParts.Add($"Collaborators: {collaborators}");
                            
                        if (!string.IsNullOrEmpty(featuredArtists))
                            metadataParts.Add($"Featured: {featuredArtists}");
                            
                        if (metadataParts.Any())
                            enrichedGenre = $"{enrichedGenre} ({string.Join(" | ", metadataParts)})";
                        else if (fullArtistString != normalizedArtist)
                            enrichedGenre = $"{enrichedGenre} (Full Artist: {fullArtistString})";
                            
                        album.Genre = enrichedGenre;
                    }
                    
                    album.LastModified = DateTime.UtcNow;
                    updatedCount++;
                }
            }
            
            if (updatedCount > 0)
            {
                await _dbContext.SaveChangesAsync();
                
                // Notify clients of the library update
                await _dataHub.Clients.All.SendAsync("LibraryUpdated");
                
                _logger.LogInformation($"Completed enhanced normalization. Updated {updatedCount} albums.");
            }
            else
            {
                _logger.LogInformation("No albums needed artist name normalization.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error normalizing existing artist names: {ex.Message}", ex);
            throw;
        }
    }
}
