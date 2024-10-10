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
    public DateTime DateAdded { get; set; }
    public int AlbumId { get; set; } // foreign key to album 
    public Album Album { get; set; } // album reference
                                     //public int TrackNumber { get; set; }  // For albums

}
