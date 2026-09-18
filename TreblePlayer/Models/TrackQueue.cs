using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace TreblePlayer.Models;

public class TrackQueue : ITrackCollection
{
    [Key]
    public int Id { get; set; } //queue Id, change to hashcode later

    public string Title { get; set; }
    public int? CollectionId { get; set; }
    public TrackCollectionType? OriginCollectionType { get; set; }
    public int? LastPlayedTrackId { get; set; }
    public bool IsManuallyModified { get; set; } = false;
    public int Size { get => Tracks?.Count ?? 0; }
    public LoopTrack LoopTrack { get; set; } = LoopTrack.None;
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }

    public bool IsSessionQueue { get; set; } = false;
    public bool IsLoopEnabled { get; set; } = false;
    public bool IsShuffleEnabled { get; set; } = false;

    public int? CurrentTrackIndex { get; set; } = 0;
    public float? LastPlaybackPositionSeconds { get; set; }

    public TrackCollectionType CollectionType => TrackCollectionType.TrackQueue;
    public ICollection<Track> Tracks { get; set; } = new List<Track>();

    // gonna store this as json
    public string? ShuffledTrackIds { get; set; }

    public static TrackQueue CreateFromCollection(ITrackCollection collection)
    {
        return new TrackQueue
        {
            Title = collection.Title,
            Tracks = collection.Tracks,
            DateCreated = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Returns the persisted queue order. The property is historically named
    /// ShuffledTrackIds, but it now stores the logical order for both shuffled
    /// and manually ordered queues.
    /// </summary>
    public List<int> GetPlaybackOrder()
    {
        if (!string.IsNullOrWhiteSpace(ShuffledTrackIds))
        {
            try
            {
                var persistedOrder = JsonSerializer.Deserialize<List<int>>(ShuffledTrackIds!);
                if (persistedOrder is not null && persistedOrder.Count > 0)
                {
                    var knownTrackIds = Tracks.Select(t => t.TrackId).ToHashSet();
                    var orderedKnownIds = persistedOrder.Where(knownTrackIds.Contains).Distinct().ToList();
                    var missingIds = Tracks
                        .Where(t => !orderedKnownIds.Contains(t.TrackId))
                        .OrderBy(t => t.DiscNumber)
                        .ThenBy(t => t.TrackNumber)
                        .ThenBy(t => t.TrackId)
                        .Select(t => t.TrackId);

                    return orderedKnownIds.Concat(missingIds).ToList();
                }
            }
            catch (JsonException)
            {
                // Fall back to the deterministic metadata order below.
            }
        }

        return Tracks
            .OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ThenBy(t => t.TrackId)
            .Select(t => t.TrackId)
            .ToList();
    }

    public void SetPlaybackOrder(IEnumerable<int> trackIds)
    {
        ShuffledTrackIds = JsonSerializer.Serialize(trackIds.Distinct().ToList());
    }

    // Kept as compatibility wrappers for existing callers.
    public List<int> GetShuffledOrder() => GetPlaybackOrder();

    public void SetShuffledOrder(List<int> trackIds) => SetPlaybackOrder(trackIds);
    public void AddTrack(Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track), "Track cannot be null");
        }
        Tracks.Add(track);
        LastModified = DateTime.UtcNow;
    }

    public void RemoveTrack(Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track), "Track cannot be null");
        }
        if (Tracks.Contains(track))
        {
            Tracks.Remove(track);
            LastModified = DateTime.Now;
        }
    }
}
