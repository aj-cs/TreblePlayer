namespace TreblePlayer.Services;

public interface IArtistNormalizationService
{
    string NormalizeArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists);
}

public class ArtistNormalizationService : IArtistNormalizationService
{
    private readonly IArtistAliasService _aliasService;
    private readonly IArtistNormalizationSettingsService _settingsService;

    private static readonly string[] KnownArtistsWithCommas = new[]
    {
        "tyler, the creator",
        "earth, wind & fire",
        "crosby, stills",
        "crosby, stills & nash",
        "crosby, stills, nash & young",
        "rob base & dj e-z rock"
    };

    public ArtistNormalizationService(IArtistAliasService aliasService)
        : this(aliasService, null)
    {
    }

    public ArtistNormalizationService(
        IArtistAliasService aliasService,
        IArtistNormalizationSettingsService? settingsService)
    {
        _aliasService = aliasService;
        _settingsService = settingsService ?? new DefaultArtistNormalizationSettingsService();
    }

    public string NormalizeArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists)
    {
        string parsedArtist = ParseArtistName(artistName, out fullArtistString, out collaborators, out featuredArtists);
        return _aliasService.GetCanonicalArtistName(parsedArtist);
    }

    private string ParseArtistName(string artistName, out string fullArtistString, out string collaborators, out string featuredArtists)
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

        foreach (var keyword in _settingsService.GetFeaturingKeywords())
        {
            int index = FindKeywordIndex(artistName, keyword);
            if (index > 0 && (firstFeaturingIndex == -1 || index < firstFeaturingIndex))
            {
                firstFeaturingIndex = index;
                matchedPattern = keyword.Trim();
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

    private static int FindKeywordIndex(string artistName, string keyword)
    {
        var normalizedKeyword = keyword.Trim();
        if (normalizedKeyword.Length == 0) return -1;

        var start = 0;
        while (start < artistName.Length)
        {
            var index = artistName.IndexOf(normalizedKeyword, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return -1;

            var beforeIsBoundary = index == 0 || char.IsWhiteSpace(artistName[index - 1]);
            var afterIndex = index + normalizedKeyword.Length;
            var afterIsBoundary = afterIndex >= artistName.Length || char.IsWhiteSpace(artistName[afterIndex]);
            if (beforeIsBoundary && afterIsBoundary) return index;

            start = afterIndex;
        }

        return -1;
    }

    private sealed class DefaultArtistNormalizationSettingsService : IArtistNormalizationSettingsService
    {
        private static readonly string[] Defaults = { "featuring", "feat.", "feat", "ft.", "ft" };

        public IReadOnlyList<string> GetFeaturingKeywords() => Defaults;
        public void SetFeaturingKeywords(IEnumerable<string> keywords) { }
    }
}
