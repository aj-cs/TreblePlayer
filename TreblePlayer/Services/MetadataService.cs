using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Models.Metadata;

namespace TreblePlayer.Services;

public class MetadataService : IMetadataService
{
    private readonly MusicPlayerDbContext _dbContext;
    private readonly ITrackRepository _trackRepository;
    private readonly ITrackCollectionRepository _collectionRepository;
    private readonly IArtworkService _artworkService;
    private readonly IArtistNormalizationService _artistNormalization;
    private readonly ILoggingService _logger;
    private readonly PlaybackWebSocketHandler _webSocketHandler;

    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public MetadataService(
        MusicPlayerDbContext dbContext,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        IArtworkService artworkService,
        IArtistNormalizationService artistNormalization,
        ILoggingService logger,
        PlaybackWebSocketHandler webSocketHandler)
    {
        _dbContext = dbContext;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _artworkService = artworkService;
        _artistNormalization = artistNormalization;
        _logger = logger;
        _webSocketHandler = webSocketHandler;
    }

    public async Task<List<TrackMetadata>> GetTrackMetadataFromFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);

        var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        var tasks = filePaths.Select(TrackMetadata.CreateAsync);
        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<AlbumMetadata> GetAlbumMetadataAsync(IEnumerable<string> trackFilePaths)
    {
        return await AlbumMetadata.CreateAsync(trackFilePaths);
    }

    public async Task<AlbumMetadata> GetAlbumMetadataFromFolderAsync(string folderPath)
    {
        var tracks = await GetTrackMetadataFromFolderAsync(folderPath);
        return await GetAlbumMetadataAsync(tracks.Select(t => t.FilePath));
    }

    public async Task<List<TrackMetadata>> GetTracksByAlbumAsync(string folderPath, string artistName)
    {
        var allTracks = await GetTrackMetadataFromFolderAsync(folderPath);
        return allTracks.Where(t => string.Equals(t.Artist, artistName, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<TrackMetadata> GetTrackMetadataFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);
        return await TrackMetadata.CreateAsync(filePath);
    }

    public async Task ScanMusicFromDirectoryAsync(List<string> directories)
    {
        var allFolders = new ConcurrentBag<string>();
        var folderTasks = directories.Select(async dir =>
        {
            if (Directory.Exists(dir))
            {
                foreach (var folder in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
                    allFolders.Add(folder);
            }
        });
        await Task.WhenAll(folderTasks);

        const int batchSize = 5;
        var folderList = allFolders.ToList();
        for (int i = 0; i < folderList.Count; i += batchSize)
        {
            var tasks = folderList.Skip(i).Take(batchSize).Select(ScanMusicFolderAsync);
            await Task.WhenAll(tasks);
            if (i + batchSize < folderList.Count) await Task.Delay(100);
        }

        _webSocketHandler.BroadcastNotification("LibraryUpdated");
    }

    public async Task ScanMusicFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        foreach (var path in filePaths)
        {
            if (await _dbContext.Tracks.AnyAsync(t => t.FilePath == path)) continue;

            try
            {
                var metadata = new ATL.Track(path);
                var track = new Models.Track
                {
                    Title = metadata.Title ?? Path.GetFileNameWithoutExtension(path),
                    Artist = metadata.Artist ?? "Unknown Artist",
                    AlbumTitle = metadata.Album ?? "Unknown Album",
                    Bitrate = metadata.Bitrate,
                    Year = metadata.Year,
                    Genre = metadata.Genre ?? "Unknown Genre",
                    TrackNumber = metadata.TrackNumber,
                    DiscNumber = (metadata.DiscNumber.HasValue && metadata.DiscNumber.Value > 0) ? metadata.DiscNumber.Value : 1,
                    FilePath = path,
                    Duration = metadata.Duration,
                    DateCreated = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                var album = await GetOrCreateAlbumAsync(track, path);
                album.Tracks.Add(track);

                await _artworkService.SetAlbumArtworkAsync(album);
                await _artworkService.SetTrackArtworkAsync(track);

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing {path}: {ex.Message}");
            }
        }
    }

    public async Task<Album> GetOrCreateAlbumAsync(Models.Track track, string filePath)
    {
        string primaryArtist = _artistNormalization.NormalizeArtistName(track.Artist, out var full, out var collabs, out var feats);
        var folder = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        // Handle multi-disc subfolders (e.g., "Disc 1", "CD 2", "Book 1")
        var folderName = Path.GetFileName(folder);
        var compareFolder = folder;
        if (folderName != null && (
            folderName.StartsWith("Disc", StringComparison.OrdinalIgnoreCase) || 
            folderName.StartsWith("CD", StringComparison.OrdinalIgnoreCase) ||
            folderName.StartsWith("Book", StringComparison.OrdinalIgnoreCase) ||
            folderName.StartsWith("Part", StringComparison.OrdinalIgnoreCase) ||
            folderName.StartsWith("Vol", StringComparison.OrdinalIgnoreCase)))
        {
            compareFolder = Path.GetDirectoryName(folder) ?? folder;
        }

        var album = await _dbContext.Albums
            .Include(a => a.Tracks)
            .FirstOrDefaultAsync(a => a.Title == track.AlbumTitle && 
                                     (a.AlbumArtist == primaryArtist || a.AlbumArtist == track.Artist) && 
                                     (a.FolderPath == compareFolder || a.FolderPath == folder || a.FolderPath.StartsWith(compareFolder)));

        if (album != null) 
        {
            if (album.FolderPath.Length > compareFolder.Length) album.FolderPath = compareFolder;
            return album;
        }

        string enrichedGenre = track.Genre ?? "Unknown Genre";
        if (full != primaryArtist)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(collabs)) parts.Add($"Collaborators: {collabs}");
            if (!string.IsNullOrEmpty(feats)) parts.Add($"Featured: {feats}");
            if (parts.Any()) enrichedGenre = $"{enrichedGenre} ({string.Join(" | ", parts)})";
        }

        album = new Album
        {
            Title = track.AlbumTitle ?? "Unknown Album",
            AlbumArtist = primaryArtist,
            Genre = enrichedGenre,
            FolderPath = compareFolder,
            Year = track.Year,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };
        _dbContext.Albums.Add(album);
        return album;
    }

    public async Task ProcessFileSystemChangesAsync(List<string> pathsToProcess)
    {
        bool changed = false;
        foreach (var path in pathsToProcess)
        {
            try
            {
                if (File.Exists(path) && IsMusicFile(path))
                {
                    var metadata = new ATL.Track(path);
                    var track = new Models.Track
                    {
                        Title = metadata.Title ?? Path.GetFileNameWithoutExtension(path),
                        Artist = metadata.Artist ?? "Unknown Artist",
                        AlbumTitle = metadata.Album ?? "Unknown Album",
                        Bitrate = metadata.Bitrate,
                        Year = metadata.Year,
                        Genre = metadata.Genre ?? "Unknown Genre",
                        TrackNumber = metadata.TrackNumber,
                        FilePath = path,
                        Duration = metadata.Duration,
                        DateCreated = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow
                    };

                    var album = await GetOrCreateAlbumAsync(track, path);
                    track.AlbumId = album.Id;

                    if (await _trackRepository.AddOrUpdateTrackAsync(track))
                    {
                        await _artworkService.SetAlbumArtworkAsync(album);
                        await _artworkService.SetTrackArtworkAsync(track);
                        changed = true;
                    }
                }
                else if (!File.Exists(path) && !Directory.Exists(path))
                {
                    var track = await _trackRepository.GetTrackByFilePathAsync(path);
                    if (track != null)
                    {
                        await _trackRepository.RemoveTracksFromDb(new List<Models.Track> { track });
                        await _collectionRepository.CleanupEmptyCollectionsAsync();
                        changed = true;
                    }
                }
                else if (Directory.Exists(path))
                {
                    await ScanMusicFromDirectoryAsync(new List<string> { path });
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing FS change for {path}: {ex.Message}");
            }
        }

        if (changed) _webSocketHandler.BroadcastNotification("LibraryUpdated");
    }

    private bool IsMusicFile(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public async Task NormalizeExistingArtistNames()
    {
        var albums = await _dbContext.Albums.ToListAsync();
        int updated = 0;

        foreach (var album in albums)
        {
            string primary = _artistNormalization.NormalizeArtistName(album.AlbumArtist, out var full, out var collabs, out var feats);
            if (primary != album.AlbumArtist)
            {
                album.AlbumArtist = primary;
                string enriched = album.Genre ?? "Unknown Genre";
                if (!enriched.Contains("(Collaborators:") && !enriched.Contains("(Featured:") && !enriched.Contains("(Full Artist:"))
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(collabs)) parts.Add($"Collaborators: {collabs}");
                    if (!string.IsNullOrEmpty(feats)) parts.Add($"Featured: {feats}");
                    if (parts.Any()) enriched = $"{enriched} ({string.Join(" | ", parts)})";
                    else if (full != primary) enriched = $"{enriched} (Full Artist: {full})";
                    album.Genre = enriched;
                }
                album.LastModified = DateTime.UtcNow;
                updated++;
            }
        }

        if (updated > 0)
        {
            await _dbContext.SaveChangesAsync();
            _webSocketHandler.BroadcastNotification("LibraryUpdated");
        }
    }
}
