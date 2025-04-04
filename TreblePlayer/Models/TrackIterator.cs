using TreblePlayer.Services;

namespace TreblePlayer.Models;

public class TrackIterator
{
    private readonly List<Track> _tracks;
    private int _currentIndex;
    private readonly ILoggingService _logger;

    public TrackIterator(IEnumerable<Track> tracks, int startIndex = 0, ILoggingService? logger = null)
    {
        _tracks = tracks.ToList();
        _currentIndex = Math.Clamp(startIndex, 0, _tracks.Count - 1);
        _logger = logger ?? new LoggingService();
        _logger.LogDebug($"TrackIterator initialized with {_tracks.Count} tracks, starting at index {_currentIndex}");
    }

    public Track? Current
    {
        get
        {
            var track = _tracks.Count > 0 ? _tracks[_currentIndex] : null;
            _logger.LogDebug($"Current track: {(track?.Title ?? "None")} at index {_currentIndex}");
            return track;
        }
    }

    public int CurrentIndex => _currentIndex;

    public Track? Next
    {
        get
        {
            if (!HasNext)
            {
                _logger.LogDebug("No next track available");
                return null;
            }
            _currentIndex++;
            var track = _tracks[_currentIndex];
            _logger.LogDebug($"Moving to next track: {track.Title} at index {_currentIndex}");
            return track;
        }
    }

    public Track? Previous
    {
        get
        {
            if (!HasPrevious)
            {
                _logger.LogDebug("No previous track available");
                return null;
            }
            _currentIndex--;
            var track = _tracks[_currentIndex];
            _logger.LogDebug($"Moving to previous track: {track.Title} at index {_currentIndex}");
            return track;
        }
    }

    //getters
    public bool HasNext => _currentIndex + 1 < _tracks.Count;

    public bool HasPrevious => _currentIndex - 1 >= 0;


    public void Reset()
    {
        _logger.LogDebug($"Resetting iterator from index {_currentIndex} to 0");
        _currentIndex = 0;
    }

    public void Shuffle(Random? rng = null)
    {
        _logger.LogInformation($"Shuffling {_tracks.Count} tracks");
        rng ??= new Random();
        var shuffled = _tracks.OrderBy(_ => rng.Next()).ToList();
        _tracks.Clear();
        _tracks.AddRange(shuffled);
        _currentIndex = 0;
        _logger.LogDebug("Shuffle complete, iterator reset to first track");
    }
}
