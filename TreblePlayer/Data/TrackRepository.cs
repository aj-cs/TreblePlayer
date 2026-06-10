using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TreblePlayer.Services;
using TreblePlayer.Models;

namespace TreblePlayer.Data;

public class TrackRepository : ITrackRepository
{
    private readonly MusicPlayerDbContext _dbContext;
    private readonly ILoggingService _logger;

    public TrackRepository(MusicPlayerDbContext dbContext, ILoggingService logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }
    public async Task<IEnumerable<Track>> GetTracksByIdAsync(IEnumerable<int> trackIds)
    {
        var tracks = await _dbContext.Tracks
            .Where(t => trackIds.Contains(t.TrackId))
            .ToListAsync();

        if (!tracks.Any())
        {
            _logger.LogWarning($"No tracks found for IDs: {string.Join(", ", trackIds)}");
        }

        return tracks;
    }

    public async Task<Track> GetTrackByIdAsync(int trackId)
    {
        var track = await _dbContext.Tracks.FirstOrDefaultAsync(t => t.TrackId == trackId);
        if (track == null)
        {
            _logger.LogWarning($"Track not found with ID: {trackId}");
        }
        return track;
    }

    public async Task RemoveTracksFromDb(ICollection<Track> tracks)
    {
        if (tracks == null || !tracks.Any())
        {
            _logger.LogWarning("Attempted to remove an empty collection of tracks.");
            return;
        }
        try
        {
            _logger.LogInformation($"Removing {tracks.Count} tracks from database");
            _dbContext.Tracks.RemoveRange(tracks);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Tracks removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error removing tracks from database", ex);
            throw;
        }
    }

    public async Task<Track?> GetTrackByFilePathAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        return await _dbContext.Tracks
            .FirstOrDefaultAsync(t => t.FilePath != null && t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> AddOrUpdateTrackAsync(Track track)
    {
        if (track == null)
        {
            _logger.LogWarning("Attempted to add or update a null track.");
            return false;
        }

        try
        {
            var existingTrack = await GetTrackByFilePathAsync(track.FilePath);

            if (existingTrack == null)
            {
                if (existingTrack == null)
                {
                    _logger.LogInformation($"Adding new track: {track.Title} (Path: {track.FilePath})");
                    await _dbContext.Tracks.AddAsync(track);
                    await _dbContext.SaveChangesAsync();
                    return true;
                }
            }

            bool needsUpdate = false;
            if (existingTrack.Title != track.Title) { existingTrack.Title = track.Title; needsUpdate = true; }
            if (existingTrack.AlbumId != track.AlbumId) { existingTrack.AlbumId = track.AlbumId; needsUpdate = true; }
            if (existingTrack.Artist != track.Artist) { existingTrack.Artist = track.Artist; needsUpdate = true; }
            if (existingTrack.Duration != track.Duration) { existingTrack.Duration = track.Duration; needsUpdate = true; }
            if (existingTrack.TrackNumber != track.TrackNumber) { existingTrack.TrackNumber = track.TrackNumber; needsUpdate = true; }
            if (existingTrack.FilePath != track.FilePath) { existingTrack.FilePath = track.FilePath; needsUpdate = true; }

            if (needsUpdate)
            {
                _logger.LogInformation($"Updating existing track: {track.Title} (Path: {track.FilePath})");
                await _dbContext.SaveChangesAsync();
                return true;
            }
            else
            {
                _logger.LogInformation($"No significant changes detected for track: {track.Title} (Path: {track.FilePath})");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding/updating track with path {track.FilePath}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<IEnumerable<Track>> GetTracksByAlbumIdAsync(int albumId)
    {
        var tracks = await _dbContext.Tracks
            .Where(t => t.AlbumId == albumId)
            .ToListAsync();

        if (!tracks.Any())
        {
            _logger.LogWarning($"No tracks found for album ID: {albumId}");
        }

        return tracks;
    }

    public async Task<IEnumerable<Track>> GetTracksByPlaylistIdAsync(int playlistId)
    {
        var playlist = await _dbContext.Playlists
            .Include(p => p.Tracks)
            .FirstOrDefaultAsync(p => p.Id == playlistId);

        return playlist?.Tracks ?? Enumerable.Empty<Track>();
    }

    public async Task<IEnumerable<Track>> GetAllTracksAsync()
    {
        var tracks = await _dbContext.Tracks
            .ToListAsync();

        if (!tracks.Any())
        {
            _logger.LogWarning($"No tracks found.");
        }
        return tracks;
    }
}
