using TreblePlayer.Services;

namespace TreblePlayer.Tests;

public class LibraryScanProgressTests
{
    [Fact]
    public void ReportCalculatesProgressAndSkippedFiles()
    {
        var service = new LibraryScanProgressService();

        service.Start(totalFiles: 80, discoveredFiles: 100);
        var progress = service.Report(processedFiles: 40, addedTracks: 38, failedFiles: 2);

        Assert.True(progress.IsScanning);
        Assert.Equal(50, progress.Percent);
        Assert.Equal(20, progress.SkippedExistingFiles);
        Assert.Equal(38, progress.AddedTracks);
        Assert.Equal(2, progress.FailedFiles);
    }

    [Fact]
    public void CompleteLeavesFinalResultsAvailable()
    {
        var service = new LibraryScanProgressService();

        service.Start(totalFiles: 10, discoveredFiles: 10);
        var progress = service.Complete(processedFiles: 10, addedTracks: 9, failedFiles: 1);

        Assert.False(progress.IsScanning);
        Assert.Equal(100, progress.Percent);
        Assert.Equal(9, progress.AddedTracks);
        Assert.Equal(1, progress.FailedFiles);
        Assert.NotNull(progress.CompletedAt);
    }
}
