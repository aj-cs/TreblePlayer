using System.Text.Json;

namespace TreblePlayer.Services;

public interface IArtistNormalizationSettingsService
{
    IReadOnlyList<string> GetFeaturingKeywords();
    void SetFeaturingKeywords(IEnumerable<string> keywords);
}

/// <summary>
/// Persists the small amount of user-controlled artist parsing configuration
/// separately from the music tables. This keeps the setting available to both
/// scans and sorting without requiring a database migration for every parser
/// option added later.
/// </summary>
public sealed class ArtistNormalizationSettingsService : IArtistNormalizationSettingsService
{
    private static readonly string[] DefaultFeaturingKeywords =
    {
        "featuring",
        "feat.",
        "feat",
        "ft.",
        "ft"
    };

    private readonly object _sync = new();
    private readonly string _settingsPath;
    private List<string> _featuringKeywords;

    public ArtistNormalizationSettingsService(IHostEnvironment environment)
    {
        _settingsPath = Path.Combine(environment.ContentRootPath, "artist-normalization-settings.json");
        _featuringKeywords = LoadKeywords();
    }

    public IReadOnlyList<string> GetFeaturingKeywords()
    {
        lock (_sync)
        {
            return _featuringKeywords.ToList();
        }
    }

    public void SetFeaturingKeywords(IEnumerable<string> keywords)
    {
        var normalized = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        if (normalized.Count == 0)
        {
            normalized = DefaultFeaturingKeywords.ToList();
        }

        lock (_sync)
        {
            _featuringKeywords = normalized;
            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(new ArtistNormalizationSettingsFile
            {
                FeaturingKeywords = normalized
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
    }

    private List<string> LoadKeywords()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var file = JsonSerializer.Deserialize<ArtistNormalizationSettingsFile>(
                    File.ReadAllText(_settingsPath));
                var saved = file?.FeaturingKeywords?
                    .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
                    .Select(keyword => keyword.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (saved is { Count: > 0 }) return saved;
            }
        }
        catch
        {
            // A malformed settings file should not prevent the player from
            // starting; the defaults are safe and can overwrite it later.
        }

        return DefaultFeaturingKeywords.ToList();
    }

    private sealed class ArtistNormalizationSettingsFile
    {
        public List<string> FeaturingKeywords { get; set; } = new();
    }
}
