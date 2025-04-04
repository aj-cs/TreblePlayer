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

    public async Task AddOrUpdateTrackAsync(Track track)
    {
        try
        {
            var existingTrack = await _dbContext.Tracks
                .FirstOrDefaultAsync(t => t.TrackId == track.TrackId);

            if (existingTrack == null)
            {
                _logger.LogInformation($"Adding new track: {track.Title} (ID: {track.TrackId})");
                await _dbContext.Tracks.AddAsync(track);
            }
            else
            {
                _logger.LogInformation($"Updating track: {track.Title} (ID: {track.TrackId})");
                existingTrack.Title = track.Title;
                existingTrack.AlbumId = track.AlbumId;
                existingTrack.Artist = track.Artist;
                existingTrack.Duration = track.Duration;
            }
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding/updating track {track.TrackId}", ex);
            throw;
        }
    }

    public async Task SaveChangesAsync()
    {
        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Database changes saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("Error saving database changes", ex);
            throw;
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
        var tracks = await _dbContext.Playlists
            .Where(t => t.Id == playlistId)
            .SelectMany(p => p.Tracks)
            .ToListAsync();

        if (!tracks.Any())
        {
            _logger.LogWarning($"No tracks found for playlist ID: {playlistId}");
        }

        return tracks;
    }
}
