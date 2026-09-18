using TreblePlayer.Models;

namespace TreblePlayer.Tests;

public class TrackQueueTests
{
    [Fact]
    public void PlaybackOrder_PreservesManualReorder()
    {
        var queue = new TrackQueue
        {
            Tracks = new List<Track>
            {
                new() { TrackId = 1, TrackNumber = 1 },
                new() { TrackId = 2, TrackNumber = 2 },
                new() { TrackId = 3, TrackNumber = 3 }
            }
        };

        queue.SetPlaybackOrder(new[] { 3, 1, 2 });

        Assert.Equal(new[] { 3, 1, 2 }, queue.GetPlaybackOrder());
    }

    [Fact]
    public void PlaybackOrder_IgnoresUnknownIdsAndAppendsNewTracks()
    {
        var queue = new TrackQueue
        {
            Tracks = new List<Track>
            {
                new() { TrackId = 1, DiscNumber = 1, TrackNumber = 1 },
                new() { TrackId = 2, DiscNumber = 1, TrackNumber = 2 },
                new() { TrackId = 3, DiscNumber = 2, TrackNumber = 1 }
            }
        };

        queue.SetPlaybackOrder(new[] { 99, 2 });

        Assert.Equal(new[] { 2, 1, 3 }, queue.GetPlaybackOrder());
    }
}
