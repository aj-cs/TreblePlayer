using TreblePlayer.Models;
using TreblePlayer.Models.Metadata;

namespace TreblePlayer.Services;

public interface IArtworkService
{
    /// <summary>
    /// Attempts to extract and save artwork for a track and/or its album.
    /// </summary>
    /// <param name="track">The track to extract artwork from.</param>
    /// <returns>The file path to the saved artwork image, or null if none found.</returns>
    Task<string?> ExtractAndSaveArtworkAsync(Track track);

    /// <summary>
    /// Gets the local file path to the artwork for a specific track.
    /// Prioritizes track artwork, then album artwork, then fallback.
    /// </summary>
    /// <param name="track">The track to get artwork for.</param>
    /// <returns>The path to the artwork image or a default fallback image.</returns>
    Task<string> GetArtworkPathAsync(Track track);

    //Task<string?> FetchArtworkFromWebAsync(Album album)

    /// <summary>
    /// Attempts to assign artwork to an album using the following priority:
    /// 1. A local image (cover.png) in the album folder
    /// 2. Embedded artwork from one of the album's track
    /// 3. A default placeholder image
    /// </summary>
    /// <param name="album"/>The album to assign artwork for. </param>
    /// <returns>The file path to the selected or generated artwork. </returns>
    Task<string> SetAlbumArtworkAsync(Album album);
    Task<string> SetTrackArtworkAsync(Track track);
    string GetDefaultArtworkPath();
}
