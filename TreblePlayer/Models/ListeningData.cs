namespace TreblePlayer.Models;

public class ListeningData {
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MusicTrackId { get; set; }
    public DateTime LastPlayed { get; set; }
    public int PlayCount { get; set; }
}