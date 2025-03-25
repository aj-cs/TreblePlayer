namespace TreblePlayer.Models;

public class TrackIterator
{
    private readonly List<Track> _tracks;
    private int _currentIndex;

    public TrackIterator(IEnumerable<Track> tracks, int startIndex = 0)
    {
        _tracks = tracks.ToList();
        _currentIndex = Math.Clamp(startIndex, 0, _tracks.Count - 1);
    }

    public Track? Current()
    {
        if (_tracks.Count > 0)
        {
            return _tracks[_currentIndex];
        }
        else
        {
            return null;
        }
    }

    public Track? Next()
    {
        if (_currentIndex + 1 >= _tracks.Count)
        {
            return null;
        }
        _currentIndex++;
        return _tracks[_currentIndex];
    }

    public Track? Previous()
    {
        if (_currentIndex - 1 < 0)
        {
            return null;
        }
        _currentIndex--;
        return _tracks[_currentIndex];
    }

    public bool HasNext()
    {
        return _currentIndex + 1 < _tracks.Count;
    }

    public bool HasPrevious()
    {
        return _currentIndex - 1 >= 0;
    }

    public void Reset()
    {
        _currentIndex = 0;
    }

    public void Shuffle(Random? rng = null)
    {
        rng ??= new Random(); // if rng is null, assign it to a new Random()
        var shuffled = _tracks.OrderBy(_ => rng.Next()).ToList();
        _tracks.Clear();
        _tracks.AddRange(shuffled);
        _currentIndex = 0;
    }
}
