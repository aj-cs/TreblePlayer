using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace TreblePlayer.Models;

public class TrackRepository : ITrackRepository
{
    private readonly MusicPlayerDbContext _dbContext;
    public TrackRepository(MusicPlayerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<Track>> GetTracksByIdAsync(IEnumerable<int> trackIds)
    {
        return await _dbContext.Tracks
            .Where(t => trackIds.Contains(t.TrackId))
            .ToListAsync();

    }

    public async Task<Track> GetTrackByIdAsync(int trackId)
    {
        return await _dbContext.Tracks.FirstOrDefaultAsync(t => t.TrackId == trackId);
    }

    public async Task RemoveTracksFromDb(ICollection<Track> tracks)
    {
        _dbContext.Tracks.RemoveRange(tracks);
        // TODO: double check the following:
        // remove range doesnt have an async method i think
        // runs the same for both
        //
        await _dbContext.SaveChangesAsync();//idk if this is necessary
    }
    public async Task AddOrUpdateTrackAsync(Track track)
    {
        var existingTrack = await _dbContext.Tracks
            .FirstOrDefaultAsync(t => t.TrackId == track.TrackId);

        if (existingTrack == null)
        {
            await _dbContext.Tracks.AddAsync(track);
        }
        else
        {
            existingTrack.Title = track.Title;
            existingTrack.AlbumId = track.AlbumId;
            existingTrack.Artist = track.Artist;
            existingTrack.Duration = track.Duration;
            //existingTrack.TrackNumber = track.TrackNumber
        }
    }
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
    public async Task<IEnumerable<Track>> GetTracksByAlbumIdAsync(int albumId)
    {
        return await _dbContext.Tracks
            .Where(t => t.AlbumId == albumId)
            .ToListAsync();
    }
    public async Task<IEnumerable<Track>> GetTracksByPlaylistIdAsync(int playlistId)
    {
        return await _dbContext.Playlists
            .Where(t => t.Id == playlistId)
            .SelectMany(p => p.Tracks)
            .ToListAsync();
    }
}
