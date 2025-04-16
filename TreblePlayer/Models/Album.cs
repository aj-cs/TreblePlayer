using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public class Album : ITrackCollection
{
    [Key]
    public int Id { get; set; } // album Id

    public string Title { get; set; }
    public int Size { get => Tracks?.Count ?? 0; }

    public string? AlbumArtist { get; set; }
    public string? ArtworkPath {get; set;}

    public string Genre { get; set; }
    public string FolderPath { get; set; }
    public int? Year { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
    public TrackCollectionType CollectionType => TrackCollectionType.Album;
    //TODO: change Tracks to private
    public ICollection<Track> Tracks { get; set; } = new List<Track>();

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
