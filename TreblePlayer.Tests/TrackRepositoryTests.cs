using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Services;

namespace TreblePlayer.Tests;

public class TrackRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<MusicPlayerDbContext> _contextOptions;

    public TrackRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<MusicPlayerDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new MusicPlayerDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetTrackByFilePath_IsCaseInsensitiveWithSqlite()
    {
        const string storedPath = "/music/Album/Track.mp3";

        using (var context = new MusicPlayerDbContext(_contextOptions))
        {
            context.Tracks.Add(new Track
            {
                Title = "Track",
                Artist = "Artist",
                FilePath = storedPath,
                Duration = 180
            });
            await context.SaveChangesAsync();
        }

        using (var context = new MusicPlayerDbContext(_contextOptions))
        {
            var repository = new TrackRepository(
                context,
                Mock.Of<ILoggingService>());

            var result = await repository.GetTrackByFilePathAsync("/MUSIC/album/TRACK.MP3");

            Assert.NotNull(result);
            Assert.Equal(storedPath, result.FilePath);
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
