using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public class TrackQueue : ITrackCollection
{
    [Key]
    public int Id { get; set; } //queue Id, change to hashcode later

    public string Title { get; set; }
    public int Size { get => Tracks?.Count ?? 0; }
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsSessionQueue { get; set; }

    public TrackCollectionType CollectionType => TrackCollectionType.TrackQueue;
    public ICollection<Track> Tracks { get; set; } = new List<Track>();

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
