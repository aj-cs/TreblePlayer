namespace TreblePlayer.Services;

public interface IArtistNormalizationService
{
    string NormalizeArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists);
}

public class ArtistNormalizationService : IArtistNormalizationService
{
    private static readonly string[] KnownArtistsWithCommas = new[]
    {
        "tyler, the creator",
        "earth, wind & fire",
        "crosby, stills",
        "crosby, stills & nash",
        "crosby, stills, nash & young",
        "rob base & dj e-z rock"
    };

    private static readonly string[] FeaturingPatterns = new[]
    {
        " featuring ",
        " ft. ",
        " ft ",
        " feat. ",
        " feat "
    };

    public string NormalizeArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists)
    {
        if (string.IsNullOrEmpty(artistName))
        {
            fullArtistString = "Unknown Artist";
            collaborators = string.Empty;
            featuredArtists = string.Empty;
            return fullArtistString;
        }

        fullArtistString = artistName.Trim();
        collaborators = string.Empty;
        featuredArtists = string.Empty;

        if (KnownArtistsWithCommas.Any(a => artistName.Trim().ToLowerInvariant().Equals(a)))
        {
            return artistName.Trim();
        }

        int firstFeaturingIndex = -1;
        string? matchedPattern = null;

        foreach (var pattern in FeaturingPatterns)
        {
            int index = artistName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index > 0 && (firstFeaturingIndex == -1 || index < firstFeaturingIndex))
            {
                firstFeaturingIndex = index;
                matchedPattern = pattern;
            }
        }

        if (firstFeaturingIndex > 0 && matchedPattern != null)
        {
            featuredArtists = artistName.Substring(firstFeaturingIndex + matchedPattern.Length).Trim();
            return artistName.Substring(0, firstFeaturingIndex).Trim();
        }

        if (artistName.Contains(" & ") && !KnownArtistsWithCommas.Any(a => artistName.ToLowerInvariant().Contains(a)))
        {
            var parts = artistName.Split(new[] { " & " }, StringSplitOptions.None);
            if (parts.Length == 2 && parts.All(p => p.Trim().Length > 2))
            {
                collaborators = parts[1].Trim();
                return parts[0].Trim();
            }
        }

        if (artistName.Contains(",") && !KnownArtistsWithCommas.Any(a => artistName.ToLowerInvariant().Contains(a)))
        {
            if (artistName.ToLowerInvariant().Contains(", the "))
            {
                return artistName.Trim();
            }

            var parts = artistName.Split(',');
            if (parts.Length > 1 && parts.All(p => p.Trim().Length > 2))
            {
                collaborators = string.Join(", ", parts.Skip(1).Select(p => p.Trim()));
                return parts[0].Trim();
            }
        }

        return artistName.Trim();
    }
}
