using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
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
    private readonly LibraryScanProgressService _scanProgress;

    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };
    private const int MetadataReadParallelism = 4;
    private const int ScanBatchSize = 100;

    public MetadataService(
        MusicPlayerDbContext dbContext,
        ITrackRepository trackRepository,
        ITrackCollectionRepository collectionRepository,
        IArtworkService artworkService,
        IArtistNormalizationService artistNormalization,
        ILoggingService logger,
        PlaybackWebSocketHandler webSocketHandler,
        LibraryScanProgressService scanProgress)
    {
        _dbContext = dbContext;
        _trackRepository = trackRepository;
        _collectionRepository = collectionRepository;
        _artworkService = artworkService;
        _artistNormalization = artistNormalization;
        _logger = logger;
        _webSocketHandler = webSocketHandler;
        _scanProgress = scanProgress;
    }

    public async Task<List<TrackMetadata>> GetTrackMetadataFromFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) throw new DirectoryNotFoundException(folderPath);

        var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(IsMusicFile)
            .ToList();

        var results = new ConcurrentBag<TrackMetadata>();
        await Parallel.ForEachAsync(
            filePaths,
            new ParallelOptions { MaxDegreeOfParallelism = MetadataReadParallelism },
            (path, _) =>
            {
                results.Add(new TrackMetadata(path));
                return ValueTask.CompletedTask;
            });

        return results.OrderBy(metadata => metadata.FilePath).ToList();
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
        var filePaths = directories
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(directory => Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(IsMusicFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation($"Discovered {filePaths.Count} supported music files across {directories.Count} folder(s).");

        await ScanMusicFilesInBulkAsync(filePaths);

        _webSocketHandler.BroadcastNotification("LibraryUpdated");
    }

    public async Task ScanMusicFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        var filePaths = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(IsMusicFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation($"Discovered {filePaths.Count} supported music files under {folderPath}.");

        await ScanMusicFilesInBulkAsync(filePaths);
    }

    private async Task ScanMusicFilesInBulkAsync(IReadOnlyCollection<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            var emptyScan = _scanProgress.Start(0, 0);
            BroadcastScanProgress("ScanStarted", emptyScan);
            BroadcastScanProgress("ScanCompleted", _scanProgress.Complete(0, 0, 0));
            return;
        }

        // Read existing paths once instead of issuing one EF query per file.
        var existingPaths = (await _dbContext.Tracks
                .AsNoTracking()
                .Select(track => track.FilePath)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pathsToRead = filePaths
            .Where(path => !existingPaths.Contains(path))
            .ToList();

        BroadcastScanProgress(
            "ScanStarted",
            _scanProgress.Start(pathsToRead.Count, filePaths.Count));

        if (pathsToRead.Count == 0)
        {
            _logger.LogInformation($"Bulk scan found no new music files among {filePaths.Count} files.");
            BroadcastScanProgress("ScanCompleted", _scanProgress.Complete(0, 0, 0));
            return;
        }

        _logger.LogInformation($"Starting bulk scan of {pathsToRead.Count} new music files in batches of {ScanBatchSize}.");

        var importedCount = 0;
        var failedCount = 0;
        try
        {
            for (var offset = 0; offset < pathsToRead.Count; offset += ScanBatchSize)
            {
                var batchPaths = pathsToRead
                    .Skip(offset)
                    .Take(ScanBatchSize)
                    .ToList();
                var parsedTracks = new ConcurrentBag<ScannedTrack>();
                var failedPaths = new ConcurrentBag<string>();

                // ATL parsing is CPU/file IO work and does not touch EF. Limit the
                // parallelism so an external drive is not flooded with reads.
                await Parallel.ForEachAsync(
                    batchPaths,
                    new ParallelOptions { MaxDegreeOfParallelism = MetadataReadParallelism },
                    (path, _) =>
                    {
                        try
                        {
                            parsedTracks.Add(ReadTrackFromFile(path));
                        }
                        catch (Exception ex)
                        {
                            failedPaths.Add(path);
                            _logger.LogError($"Error reading metadata from {path}: {ex.Message}", ex);
                        }

                        return ValueTask.CompletedTask;
                    });

                var tracks = parsedTracks.OrderBy(track => track.Track.FilePath).ToList();
                var examinedCount = Math.Min(offset + batchPaths.Count, pathsToRead.Count);

                // Publish the read phase before the database/artwork write phase. A
                // small batch can otherwise sit at 0% while persistence is doing
                // real work, which makes an active scan look frozen in the UI.
                var readProgress = _scanProgress.Report(examinedCount, importedCount, failedCount + failedPaths.Count);
                BroadcastScanProgress("ScanProgress", readProgress);

                if (tracks.Count > 0)
                {
                    await PersistTracksInBulkAsync(tracks);
                    importedCount += tracks.Count;
                }

                failedCount += failedPaths.Count;
                var progress = _scanProgress.Report(examinedCount, importedCount, failedCount);
                BroadcastScanProgress("ScanProgress", progress);
                _logger.LogInformation(
                    $"Bulk scan progress: {examinedCount}/{pathsToRead.Count} files examined, " +
                    $"{importedCount} tracks saved" +
                    (failedCount == 0 ? "." : $", {failedCount} files failed metadata parsing."));
            }

            if (importedCount == 0)
            {
                _logger.LogWarning($"Bulk scan produced no readable tracks from {pathsToRead.Count} new files.");
            }

            // Re-apply the current artist rules after importing. This also
            // folds legacy albums that were split before metadata-first
            // grouping or before an alias/feature keyword was configured.
            await NormalizeExistingArtistNames();

            BroadcastScanProgress(
                "ScanCompleted",
                _scanProgress.Complete(pathsToRead.Count, importedCount, failedCount));
        }
        catch (Exception ex)
        {
            BroadcastScanProgress("ScanFailed", _scanProgress.Fail(ex.Message));
            throw;
        }
    }

    public async Task<Album> GetOrCreateAlbumAsync(Models.Track track, string filePath)
    {
        return await GetOrCreateAlbumAsync(track, filePath, albumArtistOverride: null);
    }

    private async Task<Album> GetOrCreateAlbumAsync(
        Models.Track track,
        string filePath,
        string? albumArtistOverride)
    {
        var artistForAlbum = string.IsNullOrWhiteSpace(albumArtistOverride)
            ? track.Artist
            : albumArtistOverride;
        string primaryArtist = _artistNormalization.NormalizeArtistName(artistForAlbum, out var full, out var collabs, out var feats);
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

        var albumCandidates = await _dbContext.Albums
            .Where(a => a.Title.ToLower() == (track.AlbumTitle ?? string.Empty).ToLower())
            .ToListAsync();
        var album = albumCandidates
            .Where(candidate =>
                string.Equals(candidate.AlbumArtist, primaryArtist, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.AlbumArtist, track.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => string.Equals(candidate.FolderPath, folder, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => string.Equals(candidate.FolderPath, compareFolder, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (album != null) 
        {
            if (string.IsNullOrWhiteSpace(album.OriginalArtist))
                album.OriginalArtist = track.Artist;
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
            OriginalArtist = track.Artist,
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

        var directories = pathsToProcess
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A large copy can produce one watcher event per file. Process new
        // files from that batch through the same bulk path as an initial scan.
        // Files below a directory event are omitted because that directory will
        // be scanned as a whole below.
        var musicFiles = pathsToProcess
            .Where(path => File.Exists(path) && IsMusicFile(path))
            .Where(path => !directories.Any(directory => IsPathInside(path, directory)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingPaths = musicFiles.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _dbContext.Tracks
                    .AsNoTracking()
                    .Select(track => track.FilePath)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newMusicFiles = musicFiles
            .Where(path => !existingPaths.Contains(path))
            .ToList();
        if (newMusicFiles.Count > 0)
        {
            await ScanMusicFilesInBulkAsync(newMusicFiles);
            changed = true;
        }

        // Existing files may have changed metadata, so retain the incremental
        // update behavior for those paths.
        foreach (var path in musicFiles.Where(existingPaths.Contains))
        {
            try
            {
                changed |= await ProcessMusicFileAsync(path, skipExisting: false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing FS change for {path}: {ex.Message}");
            }
        }

        foreach (var path in pathsToProcess.Where(path => !File.Exists(path) && !Directory.Exists(path)))
        {
            try
            {
                var track = await _trackRepository.GetTrackByFilePathAsync(path);
                if (track != null)
                {
                    await _trackRepository.RemoveTracksFromDb(new List<Models.Track> { track });
                    await _collectionRepository.CleanupEmptyCollectionsAsync();
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing FS change for {path}: {ex.Message}");
            }
        }

        foreach (var directory in directories)
        {
            try
            {
                await ScanMusicFromDirectoryAsync(new List<string> { directory });
                changed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error scanning changed directory {directory}: {ex.Message}");
            }
        }

        if (changed) _webSocketHandler.BroadcastNotification("LibraryUpdated");
    }

    private bool IsMusicFile(string path)
    {
        var ext = Path.GetExtension(path)?.ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    private void BroadcastScanProgress(string type, LibraryScanProgress progress)
    {
        _webSocketHandler.BroadcastNotification(type, progress);
    }

    private static bool IsPathInside(string path, string directory)
    {
        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> ProcessMusicFileAsync(string path, bool skipExisting)
    {
        var existingTrack = await _trackRepository.GetTrackByFilePathAsync(path);
        if (skipExisting && existingTrack != null)
        {
            return false;
        }

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
            DiscNumber = metadata.DiscNumber is > 0 ? metadata.DiscNumber.Value : 1,
            FilePath = path,
            Duration = metadata.Duration,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        var album = await GetOrCreateAlbumAsync(track, path, metadata.AlbumArtist);
        bool changed;
        Track persistedTrack;

        if (existingTrack == null)
        {
            track.Album = album;
            album.Tracks.Add(track);
            _dbContext.Tracks.Add(track);
            persistedTrack = track;
            changed = true;
        }
        else
        {
            existingTrack.Album = album;
            existingTrack.AlbumId = album.Id;
            if (!album.Tracks.Any(existing => existing.TrackId == existingTrack.TrackId))
            {
                album.Tracks.Add(existingTrack);
            }

            changed = await _trackRepository.AddOrUpdateTrackAsync(track);
            persistedTrack = existingTrack;
        }

        // Save first so newly created album/track IDs are available to artwork storage.
        await _dbContext.SaveChangesAsync();
        await _artworkService.SetAlbumArtworkAsync(album);
        await _artworkService.SetTrackArtworkAsync(persistedTrack);
        await _dbContext.SaveChangesAsync();

        return changed;
    }

    private ScannedTrack ReadTrackFromFile(string path)
    {
        var metadata = new ATL.Track(path);
        var artist = metadata.Artist ?? "Unknown Artist";

        var track = new Models.Track
        {
            Title = metadata.Title ?? Path.GetFileNameWithoutExtension(path),
            Artist = artist,
            AlbumTitle = metadata.Album ?? "Unknown Album",
            Bitrate = metadata.Bitrate,
            Year = metadata.Year,
            Genre = metadata.Genre ?? "Unknown Genre",
            TrackNumber = metadata.TrackNumber,
            DiscNumber = metadata.DiscNumber is > 0 ? metadata.DiscNumber.Value : 1,
            FilePath = path,
            Duration = metadata.Duration,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        return new ScannedTrack(
            track,
            string.IsNullOrWhiteSpace(metadata.AlbumArtist) ? artist : metadata.AlbumArtist);
    }

    private async Task PersistTracksInBulkAsync(IReadOnlyCollection<ScannedTrack> scannedTracks)
    {
        var existingAlbums = await _dbContext.Albums.ToListAsync();
        var albumsByKey = new Dictionary<AlbumGroupKey, Album>();
        var tracksByAlbum = new Dictionary<Album, List<Models.Track>>();
        var tracks = scannedTracks.Select(scannedTrack => scannedTrack.Track).ToList();

        foreach (var group in scannedTracks.GroupBy(scannedTrack =>
                     GetAlbumGroupKey(scannedTrack.Track, scannedTrack.AlbumArtist, scannedTrack.Track.FilePath)))
        {
            var firstScannedTrack = group.First();
            var firstTrack = firstScannedTrack.Track;
            var primaryArtist = _artistNormalization.NormalizeArtistName(
                firstScannedTrack.AlbumArtist,
                out var fullArtist,
                out var collaborators,
                out var featuredArtists);
            var folder = Path.GetDirectoryName(firstTrack.FilePath) ?? string.Empty;
            var compareFolder = GetComparableAlbumFolder(folder);
            var albumKey = GetAlbumGroupKey(
                firstTrack,
                firstScannedTrack.AlbumArtist,
                firstTrack.FilePath);

            if (!albumsByKey.TryGetValue(albumKey, out var album))
            {
                album = existingAlbums
                    .Where(candidate => IsMatchingAlbum(
                        candidate,
                        firstTrack,
                        firstScannedTrack.AlbumArtist,
                        primaryArtist,
                        folder,
                        compareFolder))
                    .OrderByDescending(candidate => string.Equals(
                        candidate.FolderPath,
                        folder,
                        StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(candidate => string.Equals(
                        candidate.FolderPath,
                        compareFolder,
                        StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();

                if (album == null)
                {
                    var enrichedGenre = firstTrack.Genre ?? "Unknown Genre";
                    if (fullArtist != primaryArtist)
                    {
                        var artistDetails = new List<string>();
                        if (!string.IsNullOrEmpty(collaborators))
                            artistDetails.Add($"Collaborators: {collaborators}");
                        if (!string.IsNullOrEmpty(featuredArtists))
                            artistDetails.Add($"Featured: {featuredArtists}");
                        if (artistDetails.Any())
                            enrichedGenre = $"{enrichedGenre} ({string.Join(" | ", artistDetails)})";
                    }

                    album = new Album
                    {
                        Title = firstTrack.AlbumTitle ?? "Unknown Album",
                        AlbumArtist = primaryArtist,
                        OriginalArtist = firstTrack.Artist,
                        Genre = enrichedGenre,
                        FolderPath = compareFolder,
                        Year = firstTrack.Year,
                        DateCreated = DateTime.UtcNow,
                        LastModified = DateTime.UtcNow
                    };

                    _dbContext.Albums.Add(album);
                }

                if (string.IsNullOrWhiteSpace(album.OriginalArtist))
                    album.OriginalArtist = firstTrack.Artist;

                if (album.FolderPath.Length > compareFolder.Length)
                    album.FolderPath = compareFolder;

                albumsByKey[albumKey] = album;
            }

            if (!tracksByAlbum.TryGetValue(album, out var albumTracks))
            {
                albumTracks = new List<Models.Track>();
                tracksByAlbum[album] = albumTracks;
            }

            foreach (var scannedTrack in group)
            {
                var track = scannedTrack.Track;
                track.Album = album;
                album.Tracks.Add(track);
                albumTracks.Add(track);
            }
        }

        // One insert batch replaces one SaveChanges call per track. EF still
        // handles the generated album IDs and join relationships here.
        _dbContext.Tracks.AddRange(tracks);
        await _dbContext.SaveChangesAsync();

        // Album artwork is shared by the tracks in an album. Extract it once;
        // only fall back to per-track extraction when the album has no artwork.
        var defaultArtworkPath = _artworkService.GetDefaultArtworkPath();
        foreach (var (album, albumTracks) in tracksByAlbum)
        {
            try
            {
                var hasAlbumArtwork = !string.IsNullOrEmpty(album.ArtworkPath)
                    && !string.Equals(album.ArtworkPath, defaultArtworkPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(album.ArtworkPath);
                if (!hasAlbumArtwork)
                {
                    await _artworkService.SetAlbumArtworkAsync(album);
                    hasAlbumArtwork = !string.IsNullOrEmpty(album.ArtworkPath)
                        && !string.Equals(album.ArtworkPath, defaultArtworkPath, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(album.ArtworkPath);
                }

                foreach (var track in albumTracks)
                {
                    if (hasAlbumArtwork)
                    {
                        track.ArtworkPath = album.ArtworkPath;
                    }
                    else
                    {
                        await _artworkService.SetTrackArtworkAsync(track);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing artwork for album {album.Title}: {ex.Message}", ex);
            }
        }

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private AlbumGroupKey GetAlbumGroupKey(Models.Track track, string albumArtist, string filePath)
    {
        var primaryArtist = _artistNormalization.NormalizeArtistName(
            string.IsNullOrWhiteSpace(albumArtist) ? track.Artist : albumArtist,
            out _,
            out _,
            out _);
        var folder = Path.GetDirectoryName(filePath) ?? string.Empty;
        var compareFolder = HasUsableAlbumMetadata(track, albumArtist)
            ? string.Empty
            : GetComparableAlbumFolder(folder);

        return new AlbumGroupKey(
            NormalizeKeyPart(track.AlbumTitle),
            NormalizeKeyPart(primaryArtist),
            NormalizeKeyPart(compareFolder));
    }

    private static bool IsMatchingAlbum(
        Album album,
        Models.Track track,
        string albumArtist,
        string primaryArtist,
        string folder,
        string compareFolder)
    {
        var artistMatches = string.Equals(album.AlbumArtist, primaryArtist, StringComparison.OrdinalIgnoreCase)
            || string.Equals(album.AlbumArtist, track.Artist, StringComparison.OrdinalIgnoreCase)
            || string.Equals(album.AlbumArtist, albumArtist, StringComparison.OrdinalIgnoreCase);
        var metadataMatches = string.Equals(album.Title, track.AlbumTitle, StringComparison.OrdinalIgnoreCase)
            && artistMatches;
        var folderMatches = HasUsableAlbumMetadata(track, albumArtist)
            || string.IsNullOrEmpty(compareFolder)
            || string.Equals(album.FolderPath, compareFolder, StringComparison.OrdinalIgnoreCase)
            || string.Equals(album.FolderPath, folder, StringComparison.OrdinalIgnoreCase)
            || album.FolderPath.StartsWith(compareFolder, StringComparison.OrdinalIgnoreCase);

        return metadataMatches && folderMatches;
    }

    private static bool HasUsableAlbumMetadata(Models.Track track, string albumArtist)
    {
        return !string.IsNullOrWhiteSpace(track.AlbumTitle)
            && !string.Equals(track.AlbumTitle, "Unknown Album", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(albumArtist)
            && !string.Equals(albumArtist, "Unknown Artist", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetComparableAlbumFolder(string folder)
    {
        var folderName = Path.GetFileName(folder);
        if (folderName != null && (
                folderName.StartsWith("Disc", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("CD", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("Book", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("Part", StringComparison.OrdinalIgnoreCase)
                || folderName.StartsWith("Vol", StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetDirectoryName(folder) ?? folder;
        }

        return folder;
    }

    private static string NormalizeKeyPart(string? value)
    {
        return (value ?? string.Empty).ToLowerInvariant();
    }

    private readonly record struct AlbumGroupKey(string Title, string Artist, string FolderPath);
    private readonly record struct ScannedTrack(Models.Track Track, string AlbumArtist);

    public async Task NormalizeExistingArtistNames()
    {
        _dbContext.ChangeTracker.Clear();
        var albums = await _dbContext.Albums
            .Include(album => album.Tracks)
            .ToListAsync();
        int updated = 0;

        foreach (var album in albums)
        {
            var sourceArtist = string.IsNullOrWhiteSpace(album.OriginalArtist)
                ? album.AlbumArtist ?? "Unknown Artist"
                : album.OriginalArtist;
            string primary = _artistNormalization.NormalizeArtistName(
                sourceArtist,
                out var full,
                out var collabs,
                out var feats);
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

        var merged = 0;
        var duplicateGroups = albums
            .GroupBy(album => GetConsolidationKey(album))
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            var canonical = group
                .OrderByDescending(album => album.Tracks.Count)
                .ThenByDescending(album => !string.IsNullOrWhiteSpace(album.ArtworkPath))
                .ThenBy(album => album.Id)
                .First();

            foreach (var duplicate in group.Where(album => album.Id != canonical.Id))
            {
                foreach (var track in duplicate.Tracks.ToList())
                {
                    duplicate.Tracks.Remove(track);
                    canonical.Tracks.Add(track);
                    track.Album = canonical;
                    track.AlbumId = canonical.Id;
                }

                if (string.IsNullOrWhiteSpace(canonical.ArtworkPath)
                    && !string.IsNullOrWhiteSpace(duplicate.ArtworkPath))
                {
                    canonical.ArtworkPath = duplicate.ArtworkPath;
                }

                if (string.IsNullOrWhiteSpace(canonical.FolderPath)
                    || duplicate.FolderPath.Length < canonical.FolderPath.Length)
                {
                    canonical.FolderPath = duplicate.FolderPath;
                }

                canonical.LastModified = DateTime.UtcNow;
                _dbContext.Albums.Remove(duplicate);
                merged++;
            }
        }

        if (updated > 0 || merged > 0)
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Normalized {updated} album artist names and merged {merged} duplicate album record(s).");
            _webSocketHandler.BroadcastNotification("LibraryUpdated");
        }
    }

    /// <summary>
    /// Re-reads metadata from files already in the library. This is deliberately
    /// separate from normal scanning because it is an explicit, potentially slow
    /// operation requested after editing tags in an external tag editor.
    /// </summary>
    public Task RefreshArtistMetadataAsync() => RefreshLibraryMetadataAsync();

    public async Task RefreshLibraryMetadataAsync(int? albumId = null)
    {
        _dbContext.ChangeTracker.Clear();

        if (albumId.HasValue && !await _dbContext.Albums.AnyAsync(album => album.Id == albumId.Value))
            throw new KeyNotFoundException($"Album with ID {albumId.Value} not found.");

        var tracks = await _dbContext.Tracks
            .Where(track => !albumId.HasValue || track.AlbumId == albumId.Value)
            .ToListAsync();
        var albums = await _dbContext.Albums
            .Include(album => album.Tracks)
            .ToListAsync();

        var updates = new ConcurrentBag<(
            int TrackId,
            string Title,
            string Artist,
            string AlbumArtist,
            string AlbumTitle,
            string Genre,
            int Bitrate,
            int Duration,
            int? Year,
            int? TrackNumber,
            int DiscNumber,
            string FilePath)>();

        await Parallel.ForEachAsync(
            tracks,
            new ParallelOptions { MaxDegreeOfParallelism = MetadataReadParallelism },
            (track, _) =>
            {
                try
                {
                    if (!File.Exists(track.FilePath)) return ValueTask.CompletedTask;

                    var metadata = new ATL.Track(track.FilePath);
                    var artist = string.IsNullOrWhiteSpace(metadata.Artist)
                        ? track.Artist
                        : metadata.Artist.Trim();
                    var albumArtist = string.IsNullOrWhiteSpace(metadata.AlbumArtist)
                        ? artist
                        : metadata.AlbumArtist.Trim();
                    updates.Add((
                        track.TrackId,
                        string.IsNullOrWhiteSpace(metadata.Title) ? track.Title : metadata.Title.Trim(),
                        artist,
                        albumArtist,
                        string.IsNullOrWhiteSpace(metadata.Album) ? track.AlbumTitle ?? "Unknown Album" : metadata.Album.Trim(),
                        string.IsNullOrWhiteSpace(metadata.Genre) ? track.Genre ?? "Unknown Genre" : metadata.Genre.Trim(),
                        metadata.Bitrate,
                        metadata.Duration,
                        metadata.Year ?? track.Year,
                        metadata.TrackNumber ?? track.TrackNumber,
                        metadata.DiscNumber is > 0 ? metadata.DiscNumber.Value : track.DiscNumber,
                        track.FilePath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Could not refresh metadata for {track.FilePath}: {ex.Message}");
                }

                return ValueTask.CompletedTask;
            });

        var tracksById = tracks.ToDictionary(track => track.TrackId);
        var previousAlbums = tracks
            .Where(track => track.AlbumId.HasValue)
            .ToDictionary(track => track.TrackId, track => albums.FirstOrDefault(album => album.Id == track.AlbumId.Value));

        foreach (var update in updates)
        {
            if (!tracksById.TryGetValue(update.TrackId, out var track)) continue;

            track.Title = update.Title;
            track.Artist = update.Artist;
            track.AlbumTitle = update.AlbumTitle;
            track.Genre = update.Genre;
            track.Bitrate = update.Bitrate;
            track.Duration = update.Duration;
            track.Year = update.Year;
            track.TrackNumber = update.TrackNumber;
            track.DiscNumber = update.DiscNumber;
            track.LastModified = DateTime.UtcNow;

            var folder = Path.GetDirectoryName(update.FilePath) ?? string.Empty;
            var compareFolder = GetComparableAlbumFolder(folder);
            var normalizedAlbumArtist = _artistNormalization.NormalizeArtistName(
                update.AlbumArtist,
                out _,
                out _,
                out _);
            var previousAlbum = previousAlbums.GetValueOrDefault(update.TrackId);

            // Always recompute the target album. Trusting the old AlbumId is the
            // source of the stale blank album rows after MP3tag edits.
            var album = albums
                .Where(candidate => string.Equals(candidate.Title, update.AlbumTitle, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => string.Equals(
                    _artistNormalization.NormalizeArtistName(candidate.AlbumArtist ?? string.Empty, out _, out _, out _),
                    normalizedAlbumArtist,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(candidate => ReferenceEquals(candidate, previousAlbum))
                .ThenByDescending(candidate => string.Equals(candidate.FolderPath, folder, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => string.Equals(candidate.FolderPath, compareFolder, StringComparison.OrdinalIgnoreCase))
                .ThenBy(candidate => candidate.Id)
                .FirstOrDefault();

            if (album == null)
            {
                album = new Album
                {
                    Title = update.AlbumTitle,
                    AlbumArtist = normalizedAlbumArtist,
                    OriginalArtist = update.Artist,
                    Genre = update.Genre,
                    FolderPath = compareFolder,
                    Year = update.Year,
                    DateCreated = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };
                _dbContext.Albums.Add(album);
                albums.Add(album);
            }

            album.Title = update.AlbumTitle;
            album.AlbumArtist = normalizedAlbumArtist;
            album.Genre = update.Genre;
            album.Year ??= update.Year;
            if (string.IsNullOrWhiteSpace(album.FolderPath) || album.FolderPath.Length > compareFolder.Length)
                album.FolderPath = compareFolder;
            album.LastModified = DateTime.UtcNow;

            if (track.Album != null && !ReferenceEquals(track.Album, album))
                track.Album.Tracks.Remove(track);
            track.Album = album;
            track.AlbumId = album.Id;
            if (!album.Tracks.Any(existing => existing.TrackId == track.TrackId))
                album.Tracks.Add(track);
        }

        // The raw track credit is what the UI displays; the album artist remains
        // the normalized grouping/sorting value.
        foreach (var album in albums.Where(album => album.Tracks.Count > 0))
        {
            var firstTrack = album.Tracks
                .OrderBy(track => track.DiscNumber)
                .ThenBy(track => track.TrackNumber ?? int.MaxValue)
                .FirstOrDefault(track => !string.IsNullOrWhiteSpace(track.Artist));
            if (firstTrack != null)
                album.OriginalArtist = firstTrack.Artist;
        }

        await _dbContext.SaveChangesAsync();

        // Merge duplicate groups created by old tag values and rebuild artwork
        // after tracks have been moved to their new album records.
        await NormalizeExistingArtistNames();
        _dbContext.ChangeTracker.Clear();
        var refreshedAlbums = await _dbContext.Albums
            .Include(album => album.Tracks)
            .Where(album => album.Tracks.Any())
            .ToListAsync();
        foreach (var album in refreshedAlbums)
        {
            try
            {
                await _artworkService.SetAlbumArtworkAsync(album);
                foreach (var track in album.Tracks)
                    track.ArtworkPath = album.ArtworkPath;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not refresh artwork for album {album.Title}: {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
        _webSocketHandler.BroadcastNotification("LibraryUpdated");
        _logger.LogInformation(
            $"Refreshed full metadata for {updates.Count} track(s), including {refreshedAlbums.Count} album(s).");
    }

    private string GetConsolidationKey(Album album)
    {
        var normalizedArtist = _artistNormalization.NormalizeArtistName(
            album.AlbumArtist ?? string.Empty,
            out _,
            out _,
            out _);
        var title = NormalizeKeyPart(album.Title);
        var artist = NormalizeKeyPart(normalizedArtist);

        // Unknown metadata is not safe to merge globally; keep folder identity
        // as a fallback for those rows.
        if (string.IsNullOrWhiteSpace(title)
            || string.Equals(title, "unknown album", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(artist)
            || string.Equals(artist, "unknown artist", StringComparison.OrdinalIgnoreCase))
        {
            return $"{title}|{artist}|{NormalizeKeyPart(album.FolderPath)}";
        }

        return $"{title}|{artist}";
    }
}
