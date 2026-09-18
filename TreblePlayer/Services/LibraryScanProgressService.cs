namespace TreblePlayer.Services;

public sealed record LibraryScanProgress
{
    public bool IsScanning { get; init; }
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public int AddedTracks { get; init; }
    public int FailedFiles { get; init; }
    public int SkippedExistingFiles { get; init; }
    public int Percent { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public sealed class LibraryScanProgressService
{
    private readonly object _sync = new();
    private LibraryScanProgress _current = new();

    public LibraryScanProgress Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public LibraryScanProgress Start(int totalFiles, int discoveredFiles)
    {
        lock (_sync)
        {
            _current = new LibraryScanProgress
            {
                IsScanning = true,
                TotalFiles = totalFiles,
                SkippedExistingFiles = Math.Max(0, discoveredFiles - totalFiles),
                Percent = totalFiles == 0 ? 100 : 0,
                StartedAt = DateTimeOffset.UtcNow
            };

            return _current;
        }
    }

    public LibraryScanProgress Report(int processedFiles, int addedTracks, int failedFiles)
    {
        lock (_sync)
        {
            var percent = _current.TotalFiles == 0
                ? 100
                : Math.Clamp(processedFiles * 100 / _current.TotalFiles, 0, 100);

            _current = _current with
            {
                ProcessedFiles = processedFiles,
                AddedTracks = addedTracks,
                FailedFiles = failedFiles,
                Percent = percent
            };

            return _current;
        }
    }

    public LibraryScanProgress Complete(int processedFiles, int addedTracks, int failedFiles)
    {
        lock (_sync)
        {
            _current = _current with
            {
                IsScanning = false,
                ProcessedFiles = processedFiles,
                AddedTracks = addedTracks,
                FailedFiles = failedFiles,
                Percent = 100,
                CompletedAt = DateTimeOffset.UtcNow
            };

            return _current;
        }
    }

    public LibraryScanProgress Fail(string error)
    {
        lock (_sync)
        {
            _current = _current with
            {
                IsScanning = false,
                Error = error,
                CompletedAt = DateTimeOffset.UtcNow
            };

            return _current;
        }
    }
}
