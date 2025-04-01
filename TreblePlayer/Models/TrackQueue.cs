using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace TreblePlayer.Models;

public class TrackQueue : ITrackCollection
{
    [Key]
    public int Id { get; set; } //queue Id, change to hashcode later

    public string Title { get; set; }
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

    public List<int> GetShuffledOrder()
    {
        return string.IsNullOrWhiteSpace(ShuffledTrackIds)
            ? Tracks.Select(t => t.TrackId).ToList()
            : JsonSerializer.Deserialize<List<int>>(ShuffledTrackIds!) ?? new();
    }

    public void SetShuffledOrder(List<int> trackIds)
    {
        ShuffledTrackIds = JsonSerializer.Serialize(trackIds);
    }
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
