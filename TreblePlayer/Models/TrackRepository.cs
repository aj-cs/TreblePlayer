using TreblePlayer.Data;
using Microsoft.EntityFrameworkCore;
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
        return await _dbContext.Tracks.FindAsync(trackId);
    }

    public async Task RemoveTracksFromDb(ICollection<Track> tracks)
    {
        _dbContext.Tracks.RemoveRange(tracks);
        // TODO: double check the following:
        // remove range doesnt have an async method i think
        // runs the same for both
    }
    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
