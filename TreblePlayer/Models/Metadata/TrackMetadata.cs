using ATL;
namespace TreblePlayer.Models.Metadata;
public class TrackMetadata
{
    public string Title { get; set; }
    public string? Album { get; set; }
    public string Artist { get; set; }
    public string Genre { get; set; }
    public int Duration { get; set; }  // Duration in seconds
    public int? Year { get; set; }
    public int? TrackNumber { get; set; }
    public string FilePath { get; set; }

    public TrackMetadata(string filePath)
    {
        FilePath = filePath;
        var track = new ATL.Track(filePath);

        Title = track.Title;
        Album = track.Album;
        Artist = track.Artist;
        Genre = track.Genre;
        TrackNumber = track.TrackNumber;
        Duration = track.Duration;
        Year = track.Year;
    }

    public static async Task<TrackMetadata> CreateAsync(string filePath)
    {
        return await Task.Run(() => new TrackMetadata(filePath));
    }
}


