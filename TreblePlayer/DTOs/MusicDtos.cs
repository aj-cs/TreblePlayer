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
    public string? OriginalArtist { get; set; }
    public string ArtistSortKey { get; set; } = string.Empty;
    public string ArtworkUrl { get; set; } = string.Empty;
    public int? Year { get; set; }
    public DateTime LastModified { get; set; }
    public int TrackCount { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class TrackDto
{
    public int Id { get; set; }
    public int? Number { get; set; }
    public int Disc { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AlbumTitle { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string ArtworkUrl { get; set; } = string.Empty;
}

public class TrackPageDto
{
    public int Offset { get; set; }
    public int Limit { get; set; }
    public int TotalCount { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class QueueDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CurrentTrackIndex { get; set; }
    public float? LastPlaybackPositionSeconds { get; set; }
    public int? LastPlayedTrackId { get; set; }
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class QueueMetadataDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public int TotalDuration { get; set; }
    public bool IsActive { get; set; }
    public int? LastPlayedTrackId { get; set; }
}

public class PlaylistDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TrackCount { get; set; }
    public int TotalDuration { get; set; }
    public string ArtworkUrl { get; set; } = string.Empty;
    public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();
}

public class ArtistDto
{
    public string Name { get; set; } = string.Empty;
    public int AlbumCount { get; set; }
    public int TrackCount { get; set; }
    public List<AlbumDto> Albums { get; set; } = new List<AlbumDto>();
}
