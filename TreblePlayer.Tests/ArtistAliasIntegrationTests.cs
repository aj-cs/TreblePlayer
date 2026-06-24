using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.DTOs;
using TreblePlayer.Services;
using Xunit;

namespace TreblePlayer.Tests;

public class ArtistAliasIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MusicPlayerDbContext> _contextOptions;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

    public ArtistAliasIntegrationTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<MusicPlayerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var context = new MusicPlayerDbContext(_contextOptions))
        {
            context.Database.EnsureCreated();
        }

        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(MusicPlayerDbContext)))
            .Returns(() => new MusicPlayerDbContext(_contextOptions));

        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
    }

    [Fact]
    public async Task AddAlias_ShouldStoreInDatabaseAndResolveToCanonicalName()
    {
        // Arrange
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);
        var normalizationService = new ArtistNormalizationService(aliasService);

        // Act
        await aliasService.AddAliasAsync("Madvillain", "MF DOOM");

        // Assert - Verify it resolves correctly via lookup
        var resolvedName = aliasService.GetCanonicalArtistName("Madvillain");
        Assert.Equal("MF DOOM", resolvedName);

        // Assert - Verify normalization also applies it
        var normalizedName = normalizationService.NormalizeArtistName("Madvillain", out _, out _, out _);
        Assert.Equal("MF DOOM", normalizedName);

        // Assert - Confirm database persistence directly
        using var context = new MusicPlayerDbContext(_contextOptions);
        var dbEntity = await context.ArtistAliases.FirstOrDefaultAsync(a => a.AliasName == "Madvillain");
        Assert.NotNull(dbEntity);
        Assert.Equal("MF DOOM", dbEntity.CanonicalName);
    }

    [Fact]
    public async Task RemoveAlias_ShouldDeleteFromDatabaseAndStopResolving()
    {
        // Arrange
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);
        await aliasService.AddAliasAsync("Viktor Vaughn", "MF DOOM");

        // Act
        await aliasService.RemoveAliasAsync("Viktor Vaughn", "MF DOOM");

        // Assert - Lookup should fall back to original name
        var resolvedName = aliasService.GetCanonicalArtistName("Viktor Vaughn");
        Assert.Equal("Viktor Vaughn", resolvedName);

        // Assert - Confirm it was deleted from the SQLite DB rows
        using var context = new MusicPlayerDbContext(_contextOptions);
        var dbEntity = await context.ArtistAliases.FirstOrDefaultAsync(a => a.AliasName == "Viktor Vaughn");
        Assert.Null(dbEntity);
    }

    [Fact]
    public async Task GetCanonicalArtistName_ShouldReturnOriginalName_WhenAliasDoesNotExist()
    {
        // Arrange
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);

        // Act
        var resolvedName = aliasService.GetCanonicalArtistName("King Geedorah");

        // Assert
        Assert.Equal("King Geedorah", resolvedName);
    }

    [Fact]
    public async Task NormalizedArtists_ShouldSortByCanonicalGroupThenByReleaseYear()
    {
        // Arrange
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);
        await aliasService.AddAliasAsync("Madvillain", "MF DOOM");
        await aliasService.AddAliasAsync("Viktor Vaughn", "MF DOOM");

        // Populate track data variants using raw SQLite mapping values
        var albumQueue = new[]
        {
        new { Title = "Vaudeville Villain", Artist = "Viktor Vaughn", Year = 2003 },
        new { Title = "Operation: Doomsday", Artist = "MF DOOM", Year = 1999 },
        new { Title = "Madvillainy", Artist = "Madvillain", Year = 2004 }
    };

        // Act - Map items to their matching canonical grouping name via lookup service
        var sortedAlbums = albumQueue
            .Select(a => new
            {
                a.Title,
                DisplayArtist = a.Artist,
                CanonicalArtist = aliasService.GetCanonicalArtistName(a.Artist),
                a.Year
            })
            // Sort by the master Grouping Name first, then chronologically by year
            .OrderBy(a => a.CanonicalArtist)
            .ThenBy(a => a.Year)
            .ToList();

        // Assert - Verify group sorting behavior
        Assert.Equal("Operation: Doomsday", sortedAlbums[0].Title); // 1999 (MF DOOM)
        Assert.Equal("Vaudeville Villain", sortedAlbums[1].Title);  // 2003 (Viktor Vaughn -> MF DOOM group)
        Assert.Equal("Madvillainy", sortedAlbums[2].Title);        // 2004 (Madvillain -> MF DOOM group)

        // Ensure all shared the same foundational sorting group bucket
        Assert.All(sortedAlbums, album => Assert.Equal("MF DOOM", album.CanonicalArtist));
    }
    [Fact]
    public async Task NormalizeArtistName_ShouldGroupMultipleArtistsAndAliasesChronologically()
    {
        // Arrange
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);
        var normalizationService = new ArtistNormalizationService(aliasService);

        // 1. Establish all artist alias associations in our in-memory SQLite DB
        // MF DOOM aliases
        await aliasService.AddAliasAsync("Madvillain", "MF DOOM");
        await aliasService.AddAliasAsync("Viktor Vaughn", "MF DOOM");
        await aliasService.AddAliasAsync("King Geedorah", "MF DOOM");

        // Mac Miller aliases
        await aliasService.AddAliasAsync("Larry Fisherman", "Mac Miller");

        // 2. Mock a comprehensive library across diverse artists using raw metadata structures
        var mixedLibrary = new List<Track>
    {
        // MF DOOM & Co.
        new Track { Title = "Madvillainy", Artist = "Madvillain", Year = 2004 },
        new Track { Title = "Operation: Doomsday", Artist = "MF DOOM", Year = 1999 },
        new Track { Title = "Vaudeville Villain", Artist = "Viktor Vaughn", Year = 2003 },

        // Mac Miller / Larry Fisherman
        new Track { Title = "Swimming", Artist = "Mac Miller", Year = 2018 },
        new Track { Title = "Stolen Youth", Artist = "Larry Fisherman", Year = 2013 }, // Collaborative mixtape entry
        new Track { Title = "Circles", Artist = "Mac Miller", Year = 2020 },

        // Kendrick Lamar
        new Track { Title = "Good Kid, M.A.A.D City", Artist = "Kendrick Lamar", Year = 2012 },
        new Track { Title = "To Pimp a Butterfly", Artist = "Kendrick Lamar", Year = 2015 },

        // Vince Staples
        new Track { Title = "Summertime '06", Artist = "Vince Staples", Year = 2015 },
        new Track { Title = "Big Fish Theory", Artist = "Vince Staples", Year = 2017 },

        // Denzel Curry
        new Track { Title = "TA13OO", Artist = "Denzel Curry", Year = 2018 },

        // Radiohead
        new Track { Title = "OK Computer", Artist = "Radiohead", Year = 1997 },
        new Track { Title = "Kid A", Artist = "Radiohead", Year = 2000 },

        // Led Zeppelin
        new Track { Title = "Led Zeppelin IV", Artist = "Led Zeppelin", Year = 1971 },

        // King Crimson
        new Track { Title = "In the Court of the Crimson King", Artist = "King Crimson", Year = 1969 }
    };

        // Act - Process the collection through the normalization sorting layer
        var sortedLibrary = mixedLibrary
            .Select(t => new
            {
                Track = t,
                CanonicalArtist = normalizationService.NormalizeArtistName(t.Artist, out _, out _, out _)
            })
            // Step 1: Sort globally by the Canonical Artist Group name (Alphabetical)
            .OrderBy(item => item.CanonicalArtist)
            // Step 2: Sort internally within each artist group chronologically by year
            .ThenBy(item => item.Track.Year)
            // Step 3: Secondary sort by original billed title
            .ThenBy(item => item.Track.Title)
            .ToList();

        // Assert - Verify global distribution and grouping boundaries

        // 1. Check Denzel Curry
        var denzelSection = sortedLibrary.Where(i => i.CanonicalArtist == "Denzel Curry").ToList();
        Assert.Single(denzelSection);
        Assert.Equal("TA13OO", denzelSection[0].Track.Title);

        // 2. Check Kendrick Lamar Timeline
        var kendrickSection = sortedLibrary.Where(i => i.CanonicalArtist == "Kendrick Lamar").ToList();
        Assert.Equal(2, kendrickSection.Count);
        Assert.Equal("Good Kid, M.A.A.D City", kendrickSection[0].Track.Title); // 2012
        Assert.Equal("To Pimp a Butterfly", kendrickSection[1].Track.Title);    // 2015

        // 3. Check King Crimson
        var crimsonSection = sortedLibrary.Where(i => i.CanonicalArtist == "King Crimson").ToList();
        Assert.Equal("In the Court of the Crimson King", crimsonSection[0].Track.Title); // 1969

        // 4. Check Led Zeppelin
        var zeppelinSection = sortedLibrary.Where(i => i.CanonicalArtist == "Led Zeppelin").ToList();
        Assert.Equal("Led Zeppelin IV", zeppelinSection[0].Track.Title); // 1971

        // 5. Check Mac Miller / Larry Fisherman Consolidation & Timeline
        var macSection = sortedLibrary.Where(i => i.CanonicalArtist == "Mac Miller").ToList();
        Assert.Equal(3, macSection.Count);
        Assert.Equal("Stolen Youth", macSection[0].Track.Title); // 2013 (Billed as Larry Fisherman, grouped under Mac Miller)
        Assert.Equal("Swimming", macSection[1].Track.Title);     // 2018
        Assert.Equal("Circles", macSection[2].Track.Title);      // 2020
        Assert.All(macSection, item => Assert.Equal("Mac Miller", item.CanonicalArtist));

        // 6. Check MF DOOM Timeline
        var doomSection = sortedLibrary.Where(i => i.CanonicalArtist == "MF DOOM").ToList();
        Assert.Equal(3, doomSection.Count);
        Assert.Equal("Operation: Doomsday", doomSection[0].Track.Title); // 1999 (MF DOOM)
        Assert.Equal("Vaudeville Villain", doomSection[1].Track.Title); // 2003 (Viktor Vaughn)
        Assert.Equal("Madvillainy", doomSection[2].Track.Title);       // 2004 (Madvillain)
        Assert.All(doomSection, item => Assert.Equal("MF DOOM", item.CanonicalArtist));

        // 7. Check Radiohead Timeline
        var radioheadSection = sortedLibrary.Where(i => i.CanonicalArtist == "Radiohead").ToList();
        Assert.Equal(2, radioheadSection.Count);
        Assert.Equal("OK Computer", radioheadSection[0].Track.Title); // 1997
        Assert.Equal("Kid A", radioheadSection[1].Track.Title);       // 2000

        // 8. Check Vince Staples Timeline
        var vinceSection = sortedLibrary.Where(i => i.CanonicalArtist == "Vince Staples").ToList();
        Assert.Equal(2, vinceSection.Count);
        Assert.Equal("Summertime '06", vinceSection[0].Track.Title); // 2015
        Assert.Equal("Big Fish Theory", vinceSection[1].Track.Title); // 2017

        // Verify global alphabetical order of the master groups
        var executionOrderOfGroups = sortedLibrary
            .Select(i => i.CanonicalArtist)
            .Distinct()
            .ToList();

        var expectedOrder = new List<string>
    {
        "Denzel Curry",
        "Kendrick Lamar",
        "King Crimson",
        "Led Zeppelin",
        "Mac Miller",
        "MF DOOM",
        "Radiohead",
        "Vince Staples"
    };

        Assert.Equal(expectedOrder, executionOrderOfGroups);
    }
    [Fact]
    public async Task InspectDatabaseLayout_QueryDemo()
    {
        // Arrange - Setup the services
        var aliasService = new ArtistAliasService(_mockScopeFactory.Object);
        var normalizationService = new ArtistNormalizationService(aliasService);

        // 1. Seed some aliases into the in-memory SQLite DB
        await aliasService.AddAliasAsync("Madvillain", "MF DOOM");
        await aliasService.AddAliasAsync("Larry Fisherman", "Mac Miller");

        // 2. Add sample track rows straight to the DB via a tracking context instance
        using (var context = new MusicPlayerDbContext(_contextOptions))
        {
            context.Tracks.AddRange(
                new Track { Title = "Madvillainy", Artist = "Madvillain", Year = 2004, FilePath = "/music/mv.mp3" },
                new Track { Title = "Swimming", Artist = "Mac Miller", Year = 2018, FilePath = "/music/swimming.mp3" },
                new Track { Title = "Stolen Youth", Artist = "Larry Fisherman", Year = 2013, FilePath = "/music/sy.mp3" },
                new Track { Title = "Good Kid, M.A.A.D City", Artist = "Kendrick Lamar", Year = 2012, FilePath = "/music/gkmc.mp3" }
            );
            await context.SaveChangesAsync();
        }

        // Act & Assert - Open a clean database query context just to look through it
        using (var context = new MusicPlayerDbContext(_contextOptions))
        {
            // Query 1: Look at all raw registered aliases inside the SQLite table
            var dbAliases = await context.ArtistAliases.ToListAsync();
            Assert.Equal(2, dbAliases.Count);

            // Query 2: Look through tracks, resolve their aliases on the fly, and group them
            var tracksFromDb = await context.Tracks.ToListAsync();

            var localizedInspection = tracksFromDb
                .Select(t => new
                {
                    t.Title,
                    OriginalArtistBilling = t.Artist,
                    // Check what the normalization service maps this row to
                    CanonicalGroupName = normalizationService.NormalizeArtistName(t.Artist, out _, out _, out _),
                    t.Year
                })
                .OrderBy(a => a.CanonicalGroupName)
                .ThenBy(a => a.Year)
                .ToList();

            // ---- Alphabetical Inspection Check ----
            // Index 0 must be Kendrick Lamar because "K" comes before "M"
            Assert.Equal("Kendrick Lamar", localizedInspection[0].CanonicalGroupName);
            Assert.Equal("Good Kid, M.A.A.D City", localizedInspection[0].Title);

            // ---- Isolated Alias & Group Verification ----
            // Filter specifically down to Mac Miller's group to verify the chronological layout
            var macMillerTracks = localizedInspection.Where(a => a.CanonicalGroupName == "Mac Miller").ToList();
            Assert.Equal(2, macMillerTracks.Count);

            // Stolen Youth (2013) should sort ahead of Swimming (2018)
            Assert.Equal("Stolen Youth", macMillerTracks[0].Title);
            Assert.Equal("Larry Fisherman", macMillerTracks[0].OriginalArtistBilling);

            Assert.Equal("Swimming", macMillerTracks[1].Title);
            Assert.Equal("Mac Miller", macMillerTracks[1].OriginalArtistBilling);

            // Verify MF DOOM's group resolves properly too
            var doomTracks = localizedInspection.Where(a => a.CanonicalGroupName == "MF DOOM").ToList();
            Assert.Single(doomTracks);
            Assert.Equal("Madvillainy", doomTracks[0].Title);
            Assert.Equal("Madvillain", doomTracks[0].OriginalArtistBilling);
        }
    }
    public void Dispose()
    {
        // Close the connection to completely wipe the in-memory database instance
        _connection.Close();
        _connection.Dispose();
    }
}
