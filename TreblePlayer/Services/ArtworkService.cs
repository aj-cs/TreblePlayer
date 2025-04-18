using ATL.AudioData;
using Microsoft.Extensions.Logging;
using TreblePlayer.Models;
using System.IO;

namespace TreblePlayer.Services;

public class ArtworkService : IArtworkService
{
    private readonly ILoggingService _logger;
    private readonly string _artworkBaseDirectory = Path.Combine(AppContext.BaseDirectory, "artwork");

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
        var possibleFiles = new[] { "cover.jpg", "cover.jpeg", "cover.png", "folder.jpg", "folder.jpeg", "folder.png" };
        foreach (var file in possibleFiles)
        {
            var filePath = Path.Combine(folderPath, file);
            if (File.Exists(filePath))
            {
                return filePath;
            }
        }
        return null;
    }

    private string? ExtractEmbeddedArtwork(string trackFilePath, string saveToPath)
    {
        var file = new ATL.Track(trackFilePath);
        var pic = file.EmbeddedPictures.FirstOrDefault();

        if (pic != null)
        {
            var fileName = Path.Combine(saveToPath, $"{Path.GetFileNameWithoutExtension(trackFilePath)}.jpg");
            try
            {
                _logger.LogDebug($"Attempting to write embedded artwork for {trackFilePath}");
                File.WriteAllBytes(fileName, pic.PictureData);
                _logger.LogDebug($"Successfully wrote embedded artwork for {trackFilePath}");
                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write embedded artwork for {trackFilePath}: {ex.Message}");
            }
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
        var embedded = ExtractEmbeddedArtwork(track.FilePath, _artworkBaseDirectory);
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

        //check for local image first
        var local = FindCoverInFolder(album.FolderPath);
        if (local != null)
        {
            var saved = SaveArtworkToAlbum(local, album);
            album.ArtworkPath = saved;
            return saved;
        }

        // fallback to embedded image from first track
        var firstTrack = album.Tracks.FirstOrDefault();
        if (firstTrack != null)
        {
            var embedded = ExtractEmbeddedArtwork(firstTrack.FilePath, _artworkBaseDirectory);
            if (embedded != null)
            {
                album.ArtworkPath = embedded;
                return embedded;
            }
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

        var embedded = ExtractEmbeddedArtwork(track.FilePath, _artworkBaseDirectory);
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
