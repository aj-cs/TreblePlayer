namespace TreblePlayer.Models.Metadata;


public class AlbumMetadata
{
    public string Title { get; set; }
    public string AlbumArtist { get; set; }
    public string Genre { get; set; }
    public int? Year { get; set; }
    public string FolderPath { get; set; }

    public List<TrackMetadata> Tracks { get; set; }

    public AlbumMetadata(IEnumerable<string> trackFilePaths)
    {
        Tracks = trackFilePaths.Select(filePath => new TrackMetadata(filePath)).ToList();

        Title = Tracks.FirstOrDefault()?.Album;
        AlbumArtist = Tracks.FirstOrDefault()?.Artist;
        Genre = Tracks.FirstOrDefault()?.Genre;
        Year = Tracks.FirstOrDefault()?.Year ?? 2000;
    }

    public static async Task<AlbumMetadata> CreateAsync(IEnumerable<string> trackFilePaths)
    {
        return await Task.Run(() => new AlbumMetadata(trackFilePaths));
    }
}

