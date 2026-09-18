using ATL.AudioData;
using Microsoft.Extensions.Logging;
using TreblePlayer.Models;
using System.IO;

namespace TreblePlayer.Services;

public class ArtworkService : IArtworkService
{
    private readonly ILoggingService _logger;
    private readonly string _artworkBaseDirectory = Path.Combine(AppContext.BaseDirectory, "artwork");
    private static readonly string[] ImageExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
    };
    private static readonly string[] PreferredCoverNames =
    {
        "cover", "folder", "albumart", "album-art", "front", "front-cover", "artwork"
    };

    public ArtworkService(ILoggingService logger)
    {
        _logger = logger;
        if (!Directory.Exists(_artworkBaseDirectory))
        {
            Directory.CreateDirectory(_artworkBaseDirectory);
            _logger.LogInformation($"Created artwork directory: {_artworkBaseDirectory}");
        }
    }

    private string? FindCoverInFolder(string folderPath)
    {
        foreach (var preferredName in PreferredCoverNames)
        {
            var match = GetImageFiles(folderPath)
                .FirstOrDefault(path => string.Equals(
                    Path.GetFileNameWithoutExtension(path),
                    preferredName,
                    StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        return null;
    }

    private string? FindSingleImageInFolder(string folderPath)
    {
        var imageFiles = GetImageFiles(folderPath).ToList();
        return imageFiles.Count == 1 ? imageFiles[0] : null;
    }

    private IEnumerable<string> GetImageFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            return Enumerable.Empty<string>();

        try
        {
            return Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => ImageExtensions.Contains(
                    Path.GetExtension(path),
                    StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not inspect artwork in {folderPath}: {ex.Message}");
            return Enumerable.Empty<string>();
        }
    }

    private string? ExtractEmbeddedArtwork(string trackFilePath, string saveToPath, string outputName)
    {
        if (!File.Exists(trackFilePath)) return null;

        try
        {
            var file = new ATL.Track(trackFilePath);
            var pic = file.EmbeddedPictures.FirstOrDefault();

            if (pic != null)
            {
                var fileName = Path.Combine(saveToPath, $"{outputName}.jpg");
                _logger.LogDebug($"Attempting to write embedded artwork for {trackFilePath}");
                File.WriteAllBytes(fileName, pic.PictureData);
                _logger.LogDebug($"Successfully wrote embedded artwork for {trackFilePath}");
                return fileName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Could not extract embedded artwork from {trackFilePath}: {ex.Message}");
        }

        return null;
    }

    public async Task<string?> ExtractAndSaveArtworkAsync(Track track)
    {
        // local cover
        if (track.Album != null && track.Album.FolderPath != null)
        {
            var local = FindCoverInFolder(track.Album.FolderPath);
            if (local != null)
            {
                return SaveArtworkToTrack(local, track);
            }
        }

        // embedded
        var embedded = ExtractEmbeddedArtwork(track.FilePath!, _artworkBaseDirectory, $"track_{track.TrackId}");
        if (embedded != null)
        {
            track.ArtworkPath = embedded;
            return embedded;
        }
        return null;
    }


    public async Task<string> GetArtworkPathAsync(Track track)
    //split by overloading later
    {
        if (!string.IsNullOrEmpty(track.ArtworkPath) && File.Exists(track.ArtworkPath))
        {
            return track.ArtworkPath;
        }

        if (track.Album?.ArtworkPath != null && File.Exists(track.Album.ArtworkPath))
        {
            return track.Album.ArtworkPath;
        }
        return GetDefaultArtworkPath();
    }

    public async Task<string> SetAlbumArtworkAsync(Album album)
    {
        if (album.FolderPath == null)
        {
            _logger.LogWarning($"Album: {album.Id}, {album.Title} has no folder path");
            return GetDefaultArtworkPath();
        }

        // Prefer artwork embedded in the album files. Some libraries keep
        // their authoritative cover there even when a folder image exists.
        foreach (var track in album.Tracks
                     .OrderBy(track => track.DiscNumber)
                     .ThenBy(track => track.TrackNumber ?? int.MaxValue))
        {
            var embedded = ExtractEmbeddedArtwork(track.FilePath, _artworkBaseDirectory, $"album_{album.Id}");
            if (embedded != null)
            {
                album.ArtworkPath = embedded;
                return embedded;
            }
        }

        // Then use a deliberately named folder image, case-insensitively.
        var local = FindCoverInFolder(album.FolderPath);
        if (local != null)
        {
            var saved = SaveArtworkToAlbum(local, album);
            album.ArtworkPath = saved;
            return saved;
        }

        // Finally accept an unnamed image only when there is no ambiguity.
        var onlyImage = FindSingleImageInFolder(album.FolderPath);
        if (onlyImage != null)
        {
            var saved = SaveArtworkToAlbum(onlyImage, album);
            album.ArtworkPath = saved;
            return saved;
        }

        // default
        //
        album.ArtworkPath = GetDefaultArtworkPath();
        return album.ArtworkPath;
    }



    public async Task<string> SetTrackArtworkAsync(Track track)
    {
        if (track.FilePath != null && File.Exists(track.ArtworkPath))
        {
            return track.ArtworkPath;
        }

        var embedded = ExtractEmbeddedArtwork(track.FilePath!, _artworkBaseDirectory, $"track_{track.TrackId}");
        if (embedded != null)
        {
            track.ArtworkPath = embedded;
            return embedded;
        }

        //fallback to album artwork
        if (track.Album?.ArtworkPath != null)
        {
            return track.Album.ArtworkPath;
        }

        return GetDefaultArtworkPath();
    }

    public string GetDefaultArtworkPath()
    {
        return Path.Combine(_artworkBaseDirectory, "placeholder.png");
    }

    public string GetDefaultPlaylistArtworkPath()
    {
        return Path.Combine(_artworkBaseDirectory, "placeholder2.png");
    }

    private string SaveArtworkToAlbum(string sourceImagePath, Album album)
    {
        var extension = Path.GetExtension(sourceImagePath);
        var targetPath = Path.Combine(_artworkBaseDirectory, $"album_{album.Id}{extension}");
        File.Copy(sourceImagePath, targetPath, overwrite: true);
        _logger.LogDebug($"Saved artwork for Album {album.Id} to {targetPath}");
        return targetPath;
    }

    private string SaveArtworkToTrack(string sourceImagePath, Track track)
    {
        var extension = Path.GetExtension(sourceImagePath);
        var targetPath = Path.Combine(_artworkBaseDirectory, $"track_{track.TrackId}{extension}");
        File.Copy(sourceImagePath, targetPath, overwrite: true);
        _logger.LogDebug($"Saved artwork for Track {track.TrackId} to {targetPath}");
        return targetPath;
    }

    public string SaveArtworkToPlaylist(Playlist playlist, string sourceImagePath, string fileExtension)
    {
        if (!fileExtension.StartsWith('.')) fileExtension = "." + fileExtension;

        var targetPath = Path.Combine(_artworkBaseDirectory, $"playlist_{playlist.Id}{fileExtension}");
        try
        {
            File.Copy(sourceImagePath, targetPath, overwrite: true);
            _logger.LogDebug($"Saved artwork for Playlist {playlist.Id} to {targetPath}");
            return targetPath;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving artwork for Playlist {playlist.Id} from {sourceImagePath} to {targetPath}", ex);
            throw;
        }
    }
}
