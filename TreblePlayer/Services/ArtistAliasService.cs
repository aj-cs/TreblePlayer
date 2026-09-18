using System.Collections.Concurrent;

using Microsoft.EntityFrameworkCore;

using TreblePlayer.Data;
using TreblePlayer.Models;

namespace TreblePlayer.Services;

public interface IArtistAliasService
{
    ///<summary>
    ///Adds <paramref name="aliasName"/> as an alias under <paramref name="canonicalName"/>
    ///</summary>
    Task AddAliasAsync(string aliasName, string canonicalName);

    ///<summary>
    ///Removes <paramref name="aliasName"/> as an alias from <paramref name="canonicalName"/>
    ///</summary>
    Task RemoveAliasAsync(string aliasName, string canonicalName);

    ///<summary>
    ///Checks if a raw string is an alias, returning the <paramref name="canonicalName"/> if found or the original string if not.
    ///</summary>
    string GetCanonicalArtistName(string name);

    ///<summary>
    ///Gets all registered alias mappings as a read-only dictionary (Key = Alias, Value = Canonical).
    ///</summary>
    IReadOnlyDictionary<string, string> GetAllAliases();
}

public class ArtistAliasService : IArtistAliasService
{
    private readonly IServiceScopeFactory _scopeFactory;

    //in mem cache map: Key = AliasName, Value = CanonicalName
    private readonly ConcurrentDictionary<string, string> _aliasCache = new(StringComparer.OrdinalIgnoreCase);

    public ArtistAliasService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicPlayerDbContext>();

        foreach (var alias in dbContext.ArtistAliases.ToList())
        {
            _aliasCache[alias.AliasName] = alias.CanonicalName;
        }
    }
    public async Task AddAliasAsync(string aliasName, string canonicalName)
    {
        aliasName = aliasName?.Trim() ?? string.Empty;
        canonicalName = canonicalName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(aliasName) || string.IsNullOrWhiteSpace(canonicalName))
            throw new ArgumentException("Both alias and canonical artist names are required.");
        if (string.Equals(aliasName, canonicalName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An artist cannot be an alias of itself.");

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicPlayerDbContext>();
        var existing = await dbContext.ArtistAliases
            .FirstOrDefaultAsync(a => a.AliasName.ToLower() == aliasName.ToLower());
        if (existing != null)
        {
            existing.CanonicalName = canonicalName;
        }
        else
        {
            await dbContext.ArtistAliases.AddAsync(new ArtistAlias
            {
                AliasName = aliasName,
                CanonicalName = canonicalName
            });
        }
        await dbContext.SaveChangesAsync();
        _aliasCache[aliasName] = canonicalName;
    }

    public string GetCanonicalArtistName(string name)
    {

        if (string.IsNullOrWhiteSpace(name)) return name;
        string formatted = name.Trim();
        if (_aliasCache.TryGetValue(formatted, out var canonicalName))
        {
            return canonicalName;
        }

        return name;
    }

    public async Task RemoveAliasAsync(string aliasName, string canonicalName)
    {
        string formatted = aliasName.Trim();
        if (!_aliasCache.ContainsKey(formatted)) return;
        _aliasCache.Remove(formatted, out _);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicPlayerDbContext>();
        var aliasEntity = await dbContext.ArtistAliases.FirstOrDefaultAsync(a =>
            a.AliasName.ToLower() == formatted.ToLower() &&
            a.CanonicalName.ToLower() == canonicalName.Trim().ToLower());

        if (aliasEntity == null)
        {
            return;
        }
        dbContext.ArtistAliases.Remove(aliasEntity);
        await dbContext.SaveChangesAsync();
    }

    public IReadOnlyDictionary<string, string> GetAllAliases()
    {
        return _aliasCache;
    }
}
