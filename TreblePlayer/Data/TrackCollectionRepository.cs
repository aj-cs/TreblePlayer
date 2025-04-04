using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TreblePlayer.Services;
namespace TreblePlayer.Data;

public class TrackCollectionRepository : ITrackCollectionRepository
{
    private readonly MusicPlayerDbContext _dbContext;
    private readonly ILoggingService _logger;

    public TrackCollectionRepository(MusicPlayerDbContext dbContext, ILoggingService logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ITrackCollection> GetTrackCollectionByIdAsync(int collectionId, TrackCollectionType collectionType)
    {
        try
        {
            var collection = collectionType switch
            {
                TrackCollectionType.Album => (ITrackCollection)await _dbContext.Albums.Include(a => a.Tracks)
                    .FirstOrDefaultAsync(a => a.Id == collectionId),
                TrackCollectionType.Playlist => (ITrackCollection)await _dbContext.Playlists.Include(p => p.Tracks)
                    .FirstOrDefaultAsync(p => p.Id == collectionId),
                TrackCollectionType.TrackQueue => (ITrackCollection)await _dbContext.TrackQueues.Include(q => q.Tracks)
                    .FirstOrDefaultAsync(q => q.Id == collectionId),
                _ => throw new ArgumentException("Unsupported track collection type.")
            };

            if (collection == null)
            {
                _logger.LogWarning($"Collection not found: ID {collectionId}, Type {collectionType}");
            }

            return collection;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting collection {collectionId} of type {collectionType}", ex);
            throw;
        }
    }

    public async Task SaveAsync(ITrackCollection collection)
    {
        try
        {
            var existingCollection = await GetTrackCollectionByIdAsync(collection.Id, collection.CollectionType);
            if (existingCollection == null)
            {
                _logger.LogWarning($"Cannot save: Collection {collection.Id} not found");
                throw new ArgumentException("Collection not found.");
            }

            _dbContext.Entry(existingCollection).CurrentValues.SetValues(collection);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Saved collection: {collection.Title} (ID: {collection.Id})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving collection {collection.Id}", ex);
            throw;
        }
    }

    public async Task<string> GetCollectionTitleByIdAsync(int collectionId, TrackCollectionType type)
    {
        try
        {
            var collection = await GetTrackCollectionByIdAsync(collectionId, type);
            if (collection == null)
            {
                _logger.LogWarning($"Cannot get title: Collection {collectionId} not found");
                throw new ArgumentException("Collection not found.");
            }
            return collection.Title;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting title for collection {collectionId}", ex);
            throw;
        }
    }

    public async Task<List<ITrackCollection>> GetCollectionsByTitleAsync(string title)
    {
        try
        {
            _logger.LogInformation($"Searching collections by title: {title}");
            var albums = await _dbContext.Albums.Where(a => a.Title.Contains(title)).ToListAsync();
            var playlists = await _dbContext.Playlists.Where(a => a.Title.Contains(title)).ToListAsync();
            var queues = await _dbContext.TrackQueues.Where(a => a.Title.Contains(title)).ToListAsync();
            
            var results = albums.Cast<ITrackCollection>()
                .Concat(playlists.Cast<ITrackCollection>())
                .Concat(queues.Cast<ITrackCollection>())
                .ToList();

            _logger.LogInformation($"Found {results.Count} collections matching title: {title}");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error searching collections by title: {title}", ex);
            throw;
        }
    }

    public async Task<List<ITrackCollection>> GetCollectionsByTrackAsync(int trackId)
    {
        try
        {
            _logger.LogInformation($"Searching collections containing track: {trackId}");
            var albums = await _dbContext.Albums.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
            var playlists = await _dbContext.Playlists.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
            var queues = await _dbContext.TrackQueues.Where(a => a.Tracks.Any(t => t.TrackId == trackId)).ToListAsync();
            
            var results = albums.Cast<ITrackCollection>()
                .Concat(playlists.Cast<ITrackCollection>())
                .Concat(queues.Cast<ITrackCollection>())
                .ToList();

            _logger.LogInformation($"Found {results.Count} collections containing track: {trackId}");
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error searching collections by track: {trackId}", ex);
            throw;
        }
    }

    public async Task<Album> GetAlbumByIdAsync(int albumId)
    {
        try
        {
            var album = await _dbContext.Albums
                .Include(a => a.Tracks)
                .FirstOrDefaultAsync(a => a.Id == albumId);

            if (album == null)
            {
                _logger.LogWarning($"Album not found: ID {albumId}");
            }

            return album;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting album {albumId}", ex);
            throw;
        }
    }

    public async Task<Playlist> GetPlaylistByIdAsync(int playlistId)
    {
        try
        {
            var playlist = await _dbContext.Playlists
                .Include(a => a.Tracks)
                .FirstOrDefaultAsync(a => a.Id == playlistId);

            if (playlist == null)
            {
                _logger.LogWarning($"Playlist not found: ID {playlistId}");
            }

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting playlist {playlistId}", ex);
            throw;
        }
    }

    public async Task RemoveCollectionFromDb(ITrackCollection collection)
    {
        try
        {
            _logger.LogInformation($"Removing collection: {collection.Title} (ID: {collection.Id})");
            
            if (collection is Album album)
            {
                _dbContext.Albums.Remove(album);
            }
            else if (collection is Playlist playlist)
            {
                _dbContext.Playlists.Remove(playlist);
            }
            else if (collection is TrackQueue queue)
            {
                _dbContext.TrackQueues.Remove(queue);
            }
            else
            {
                _logger.LogError($"Unsupported collection type: {collection.GetType()}");
                throw new ArgumentException("Unsupported Track Collection");
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully removed collection: {collection.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing collection {collection.Id}", ex);
            throw;
        }
    }

    public async Task RemoveAlbumAndTracksAsync(Album album)
    {
        try
        {
            if (album == null)
            {
                _logger.LogWarning("Cannot remove: Album is null");
                throw new ArgumentException("Album not found.");
            }

            if (!_dbContext.Albums.Contains(album))
            {
                _logger.LogWarning($"Album does not exist in DB: {album.Title} (ID: {album.Id})");
                return;
            }

            _logger.LogInformation($"Removing album and tracks: {album.Title} (ID: {album.Id})");
            _dbContext.Albums.Remove(album);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully removed album: {album.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing album {album?.Id}", ex);
            throw;
        }
    }

    public async Task UpdateCollectionAsync(ITrackCollection collection)
    {
        try
        {
            _logger.LogInformation($"Updating collection: {collection.Title} (ID: {collection.Id})");
            _dbContext.Update(collection);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully updated collection: {collection.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating collection {collection.Id}", ex);
            throw;
        }
    }

    public async Task<TrackQueue> GetQueueByIdAsync(int queueId)
    {
        try
        {
            var queue = await _dbContext.TrackQueues
                .Include(q => q.Tracks)
                .FirstOrDefaultAsync(q => q.Id == queueId);

            if (queue == null)
            {
                _logger.LogWarning($"Queue not found: ID {queueId}");
            }

            return queue;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting queue {queueId}", ex);
            throw;
        }
    }

    public async Task<List<TrackQueue>> GetAllQueuesAsync()
    {
        try
        {
            var queues = await _dbContext.TrackQueues
                .Include(q => q.Tracks)
                .ToListAsync();
            
            _logger.LogInformation($"Retrieved {queues.Count} queues");
            return queues;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error getting all queues", ex);
            throw;
        }
    }

    public async Task AddQueueAsync(TrackQueue queue)
    {
        try
        {
            _logger.LogInformation($"Adding new queue: {queue.Title}");
            
            var trackIds = queue.Tracks.Select(t => t.TrackId).ToList();
            var trackedTracks = await _dbContext.Tracks
                .Where(t => trackIds.Contains(t.TrackId))
                .ToListAsync();

            queue.Tracks = trackedTracks;

            await _dbContext.TrackQueues.AddAsync(queue);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully added queue: {queue.Title} (ID: {queue.Id})");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding queue {queue.Title}", ex);
            throw;
        }
    }

    public async Task RemoveTrackFromQueueAsync(int queueId, int trackId)
    {
        try
        {
            _logger.LogInformation($"Removing track {trackId} from queue {queueId}");
            
            var queue = await _dbContext.TrackQueues
                .Include(q => q.Tracks)
                .FirstOrDefaultAsync(q => q.Id == queueId);
                
            if (queue == null)
            {
                _logger.LogWarning($"Queue not found: ID {queueId}");
                throw new Exception($"Queue {queueId} not found");
            }

            var track = queue.Tracks.FirstOrDefault(t => t.TrackId == trackId);
            if (track == null)
            {
                _logger.LogWarning($"Track not found in queue: ID {trackId}");
                throw new Exception($"Track {trackId} not found in queue {queueId}");
            }

            queue.Tracks.Remove(track);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully removed track {trackId} from queue {queueId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing track {trackId} from queue {queueId}", ex);
            throw;
        }
    }

    public async Task ClearQueueAsync(int queueId)
    {
        try
        {
            _logger.LogInformation($"Clearing queue: ID {queueId}");
            
            var queue = await _dbContext.TrackQueues
                .Include(q => q.Tracks)
                .FirstOrDefaultAsync(q => q.Id == queueId);
                
            if (queue == null)
            {
                _logger.LogWarning($"Queue not found: ID {queueId}");
                throw new Exception($"Queue: {queueId} not found.");
            }

            queue.Tracks.Clear();
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation($"Successfully cleared queue: {queue.Title}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error clearing queue {queueId}", ex);
            throw;
        }
    }
}

