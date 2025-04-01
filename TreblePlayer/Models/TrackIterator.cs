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

    public Track? Current => _tracks.Count > 0 ? _tracks[_currentIndex] : null;

    public int CurrentIndex => _currentIndex;

    public Track? Next() => HasNext ? _tracks[++_currentIndex] : null;

    public Track? Previous() => HasPrevious ? _tracks[--_currentIndex] : null;

    //getters
    public bool HasNext => _currentIndex + 1 < _tracks.Count;

    public bool HasPrevious => _currentIndex - 1 >= 0;


    public void Reset() => _currentIndex = 0; //setter

    public void Shuffle(Random? rng = null)
    {
        rng ??= new Random(); // if rng is null, assign it to a new Random()
        var shuffled = _tracks.OrderBy(_ => rng.Next()).ToList();
        _tracks.Clear();
        _tracks.AddRange(shuffled);
        _currentIndex = 0;
    }
}
