namespace TreblePlayer.DTOs;

public class PlaylistCreateWithItemsModel
{
    public string Title { get; set; } = string.Empty;
    public List<int> TrackIds { get; set; } = new List<int>();
}

public class AlbumDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class TrackDto
{
    public int Id { get; set; }
    public int? Number { get; set; }
    public int Disc { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string ArtworkUrl { get; set; } = string.Empty;
}

public class QueueDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CurrentTrackIndex { get; set; }
    public float? LastPlaybackPositionSeconds { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class ArtistDto
{
    public string Name { get; set; } = string.Empty;
    public int AlbumCount { get; set; }
    public int TrackCount { get; set; }
    public List<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
}

