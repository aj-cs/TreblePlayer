using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public interface ITrackCollection
{
    [Key]
    public int Id { get; set; } //change to hashcode later

    public string Title { get; set; }
    public int Size { get; }

    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }

    public ICollection<Track> Tracks { get; set; }

    public TrackCollectionType CollectionType { get; }
    public void AddTrack(Track track);
    public void RemoveTrack(Track track);
    //public void RenameCollection(string title);
    //public void DeleteCollection();
    public IEnumerable<Track> GetTracks();

}
