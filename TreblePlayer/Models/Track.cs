using System.ComponentModel.DataAnnotations;
namespace TreblePlayer.Models;

public class Track
{
    [Key]
    public int TrackId { get; set; }

    [Required]
    public string Title { get; set; } = "Unknown Title";

    [Required]
    public string Artist { get; set; } = "Unknown Artist";

    public string? Genre { get; set; }

    public int Bitrate { get; set; } = 128; // Default to 128 kbps if not provided

    [Required]
    public string FilePath { get; set; } = "Unknown FilePath";

    public int? Year { get; set; }

    [Required]
    public int Duration { get; set; } = 0; // Prevents null Duration

    [Required]
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    public string? AlbumTitle { get; set; } = "Unknown Album";
    public string? ArtworkPath { get; set; }
    public int? AlbumId { get; set; } // Foreign key to album 
    public Album? Album { get; set; }
    public int? TrackNumber { get; set; }

    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
    public ICollection<TrackQueue> TrackQueues { get; set; } = new List<TrackQueue>();
}

