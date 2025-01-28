using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public class Track
{
    [Key]
    public int TrackId { get; set; }

    public string Title { get; set; }
    public string Artist { get; set; }
    public string Genre { get; set; }
    public int Bitrate { get; set; } //kbps
    public string FilePath { get; set; }
    public int? Year { get; set; }
    public int Duration { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastModified { get; set; }
    public int AlbumId { get; set; } // foreign key to album 
                                     //public int TrackNumber { get; set; }  // For albums
                                     // Many-to-many relationship with Playlist
    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
    public ICollection<TrackQueue> TrackQueues { get; set; } = new List<TrackQueue>();
}
