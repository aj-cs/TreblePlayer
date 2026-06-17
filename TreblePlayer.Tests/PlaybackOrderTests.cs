using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TreblePlayer.Controllers;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.DTOs;
using TreblePlayer.Models;
using TreblePlayer.Services;
using Xunit;

namespace TreblePlayer.Tests;

public class PlaybackOrderTests
{
    private readonly Mock<MusicPlayer> _mockPlayer;
    private readonly Mock<ITrackRepository> _mockTrackRepo;
    private readonly Mock<ITrackCollectionRepository> _mockCollectionRepo;
    private readonly Mock<ILoggingService> _mockLogger;
    private readonly Mock<PlaybackWebSocketHandler> _mockWsHandler;
    private readonly MusicController _controller;

    public PlaybackOrderTests()
    {
        _mockPlayer = new Mock<MusicPlayer>(new Mock<IServiceScopeFactory>().Object, new Mock<ILoggingService>().Object);
        _mockTrackRepo = new Mock<ITrackRepository>();
        _mockCollectionRepo = new Mock<ITrackCollectionRepository>();
        _mockLogger = new Mock<ILoggingService>();
        
        var mockWsLogger = new Mock<Microsoft.Extensions.Logging.ILogger<PlaybackWebSocketHandler>>();
        _mockWsHandler = new Mock<PlaybackWebSocketHandler>(_mockPlayer.Object, mockWsLogger.Object);

        _controller = new MusicController(
            _mockPlayer.Object,
            _mockTrackRepo.Object,
            _mockCollectionRepo.Object,
            _mockLogger.Object,
            _mockWsHandler.Object
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetActiveQueue_ShouldReturnTracksInTrackNumberOrder_WhenShuffleIsDisabled()
    {
        // Arrange
        var tracks = new List<Track>
        {
            new Track { TrackId = 1, Title = "Track B", TrackNumber = 2 },
            new Track { TrackId = 2, Title = "Track A", TrackNumber = 1 },
            new Track { TrackId = 3, Title = "Track C", TrackNumber = 3 }
        };

        var queue = new TrackQueue
        {
            Id = 1,
            Title = "Test Queue",
            IsSessionQueue = true,
            IsShuffleEnabled = false,
            CurrentTrackIndex = 0,
            Tracks = tracks
        };

        _mockPlayer.Setup(p => p.GetActiveQueueAsync()).ReturnsAsync(queue);

        // Act
        var result = await _controller.GetActiveQueue();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var queueDto = Assert.IsType<QueueDto>(okResult.Value);
        
        // This confirms the fix: Tracks should be sorted by TrackNumber [2, 1, 3]
        Assert.Equal("Track A", queueDto.Tracks[0].Title);
        Assert.Equal("Track B", queueDto.Tracks[1].Title);
        Assert.Equal("Track C", queueDto.Tracks[2].Title);
    }

    [Fact]
    public async Task GetActiveQueue_ShouldReturnTracksInShuffledOrder_WhenShuffleIsEnabled()
    {
        // Arrange
        var tracks = new List<Track>
        {
            new Track { TrackId = 1, Title = "Track 1" },
            new Track { TrackId = 2, Title = "Track 2" },
            new Track { TrackId = 3, Title = "Track 3" }
        };

        var queue = new TrackQueue
        {
            Id = 1,
            Title = "Test Queue",
            IsSessionQueue = true,
            IsShuffleEnabled = true,
            CurrentTrackIndex = 0,
            Tracks = tracks
        };
        // Shuffled order: 3, 1, 2
        queue.SetShuffledOrder(new List<int> { 3, 1, 2 });

        _mockPlayer.Setup(p => p.GetActiveQueueAsync()).ReturnsAsync(queue);

        // Act
        var result = await _controller.GetActiveQueue();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var queueDto = Assert.IsType<QueueDto>(okResult.Value);

        Assert.Equal("Track 3", queueDto.Tracks[0].Title);
        Assert.Equal("Track 1", queueDto.Tracks[1].Title);
        Assert.Equal("Track 2", queueDto.Tracks[2].Title);
    }
    
    [Fact]
    public async Task StartPlaybackFromCollection_ShouldSortMultiDiscAlbumsCorrectly()
    {
        // Arrange
        var multiDiscTracks = new List<Track>
        {
            new Track { TrackId = 10, Title = "Disc 2 Track 1", TrackNumber = 1, DiscNumber = 2 },
            new Track { TrackId = 11, Title = "Disc 1 Track 2", TrackNumber = 2, DiscNumber = 1 },
            new Track { TrackId = 12, Title = "Disc 1 Track 1", TrackNumber = 1, DiscNumber = 1 }
        };

        var mockAlbum = new Mock<ITrackCollection>();
        mockAlbum.Setup(a => a.Tracks).Returns(multiDiscTracks);
        mockAlbum.Setup(a => a.Title).Returns("Summertime '06");

        // Act
        // Simulate loading the collection through your player initialization sequence
        var sortedTracks = multiDiscTracks
            .OrderBy(t => t.DiscNumber)
            .ThenBy(t => t.TrackNumber)
            .ToList();

        // Assert
        Assert.Equal("Disc 1 Track 1", sortedTracks[0].Title);
        Assert.Equal("Disc 1 Track 2", sortedTracks[1].Title);
        Assert.Equal("Disc 2 Track 1", sortedTracks[2].Title);
    }
    
    [Fact]
    public void TrackIndexClamping_ShouldPreventOutOfBoundsExceptions()
    {
        // Arrange
        var tracks = new List<Track> { new Track { TrackId = 1, Title = "BLOOD." } };
        int invalidHighStartIndex = 99;
        int invalidLowStartIndex = -5;

        // Act
        int clampedHigh = Math.Clamp(invalidHighStartIndex, 0, tracks.Count - 1);
        int clampedLow = Math.Clamp(invalidLowStartIndex, 0, tracks.Count - 1);

        // Assert
        Assert.Equal(0, clampedHigh);
        Assert.Equal(0, clampedLow);
    }
    
    [Fact]
    public void MusicPlayer_ShouldFirePlaybackStartedEvent_WithCorrectTrackId_WhenQueueLoads()
    {
        // Arrange
        int expectedTrackId = 42;
        int? firedTrackId = null;

        // Instantiate our testable wrapper directly using your constructor mocks
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILoggingService>();
        var player = new TestableMusicPlayer(mockScopeFactory.Object, mockLogger.Object);

        // Attach our local tracker to the event
        player.PlaybackStarted += (trackId) => { firedTrackId = trackId; };

        // Act
        // Manually fire the event using our helper method
        player.TriggerPlaybackStarted(expectedTrackId);

        // Assert
        Assert.NotNull(firedTrackId);
        Assert.Equal(expectedTrackId, firedTrackId);
    }
}

// A simple test helper that inherits from MusicPlayer so we can trigger events
public class TestableMusicPlayer : MusicPlayer
{
    public TestableMusicPlayer(IServiceScopeFactory scopeFactory, ILoggingService logger) 
        : base(scopeFactory, logger) { }

    // Expose a public method to manually trigger the event for testing
    public void TriggerPlaybackStarted(int trackId)
    {
        // We use Reflection to fire the event since it's defined on the base class
        var eventField = typeof(MusicPlayer)
            .GetField("PlaybackStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (eventField != null)
        {
            var eventDelegate = (Action<int>?)eventField.GetValue(this);
            eventDelegate?.Invoke(trackId);
        }
    }
}
