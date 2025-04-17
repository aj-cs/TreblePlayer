using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public class Playlist : ITrackCollection
{
    [Key]
    public int Id { get; set; } //playlist Id, change to hashcode later
    public string Title { get; set; }

    public int Size { get => Tracks?.Count ?? 0; }

    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
    public TrackCollectionType CollectionType => TrackCollectionType.Playlist;
    public ICollection<Track> Tracks { get; set; } = new List<Track>();
    public string? ArtworkPath { get; set; }
    public string? Genre { get; set; }

    //TODO: change Tracks to private

    public void AddTrack(Track track)
    {
        if (track == null)
        {
            throw new ArgumentNullException(nameof(track), "Track cannot be null");
        }
        Tracks.Add(track);
        LastModified = DateTime.Now;
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
