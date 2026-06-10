using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection; // Required for IServiceScopeFactory
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore; // Add this import for ToListAsync

namespace TreblePlayer.Services;

public class FolderMonitoringService : IHostedService, IDisposable
{
    private readonly ILoggingService _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<string> _monitoredFolders = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentQueue<string> _pendingChanges = new();
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private const int DebounceMilliseconds = 1500; // 1.5 seconds
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    private Timer? _debounceTimer;

    public FolderMonitoringService(
        ILoggingService logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        // We'll load the monitored folders in StartAsync
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting folder monitoring service");

        // Load monitored folders from database
        await LoadMonitoredFoldersAsync();

        if (_monitoredFolders.Count == 0)
        {
            _logger.LogWarning("No folders to monitor. Service will start but won't watch any folders");
            return;
        }

        // Set up file system watchers
        foreach (var folder in _monitoredFolders)
        {
            try
            {
                CreateAndStartWatcher(folder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start monitoring folder '{folder}': {ex.Message}", ex);
            }
        }
        
        // Perform initial scan of all folders on startup
        try
        {
            _logger.LogInformation("Performing initial scan of all monitored folders on startup");
            
            // Create a scope to resolve dependencies
            using var scope = _scopeFactory.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
            
            // Create a copy of the list to avoid possible modification during iteration
            var foldersToScan = _monitoredFolders.ToList();
            
            if (foldersToScan.Any())
            {
                _logger.LogInformation($"Starting initial scan of {foldersToScan.Count} folders");
                await metadataService.ScanMusicFromDirectoryAsync(foldersToScan);
                _logger.LogInformation("Initial folder scan completed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error performing initial folder scan: {ex.Message}", ex);
            // Continue with service startup despite scan errors
        }
    }

    private async Task LoadMonitoredFoldersAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MusicPlayerDbContext>();

            // Fetch monitored folders from database
            var folders = await dbContext.MonitoredFolders
                .Select(mf => mf.Path)
                .ToListAsync();

            // Add music directory from app root as a default if no folders are configured
            if (folders.Count == 0)
            {
                var musicDirectory = Path.Combine(Directory.GetCurrentDirectory(), "music");
                if (Directory.Exists(musicDirectory))
                {
                    folders.Add(musicDirectory);
                    _logger.LogInformation($"No monitored folders found in database. Using default: {musicDirectory}");
                }
            }

            // Filter to only existing directories and log warnings for missing ones
            foreach (var folder in folders)
            {
                if (Directory.Exists(folder))
                {
                    _monitoredFolders.Add(folder);
                }
                else
                {
                    _logger.LogWarning($"Monitored folder does not exist: {folder}. Consider removing it from settings.");
                }
            }

            _logger.LogInformation($"Loaded {_monitoredFolders.Count} valid folders for monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading monitored folders: {ex.Message}", ex);

            // Fallback to default music directory
            var musicDirectory = Path.Combine(Directory.GetCurrentDirectory(), "music");
            if (Directory.Exists(musicDirectory))
            {
                _monitoredFolders.Add(musicDirectory);
                _logger.LogInformation($"Using fallback music directory: {musicDirectory}");
            }
        }
    }

    private void CreateAndStartWatcher(string folderPath)
    {
        var watcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Created += OnFileSystemChanged;
        watcher.Changed += OnFileSystemChanged;
        watcher.Deleted += OnFileSystemChanged;
        watcher.Renamed += OnFileSystemRenamed;
        watcher.Error += OnWatcherError;

        _watchers.Add(watcher);
        _logger.LogInformation($"Monitoring folder: {folderPath}");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping folder monitoring service");

        _cts.Cancel();

        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stopping watcher: {ex.Message}", ex);
            }
        }

        _watchers.Clear();
        return Task.CompletedTask;
    }

    private bool IsMusicFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        _logger.LogError($"File system watcher error: {exception.Message}", exception);

        if (sender is FileSystemWatcher watcher)
        {
            try
            {
                // Attempt to restart the watcher
                var path = watcher.Path;
                _logger.LogInformation($"Attempting to restart watcher for {path}");

                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(watcher);

                CreateAndStartWatcher(path);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to restart watcher: {ex.Message}", ex);
            }
        }
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            if (IsMusicFile(e.OldFullPath) || IsMusicFile(e.FullPath))
            {
                _logger.LogInformation($"File renamed: {e.OldFullPath} -> {e.FullPath}");

                // Queue both the old path (for removal) and the new path (for addition)
                QueueChangeProcessing(e.OldFullPath);
                QueueChangeProcessing(e.FullPath);
            }
            else if (Directory.Exists(e.FullPath))
            {
                _logger.LogInformation($"Directory renamed: {e.OldFullPath} -> {e.FullPath}");

                // Queue the entire directory for processing
                QueueChangeProcessing(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing renamed event: {ex.Message}", ex);
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted || IsMusicFile(e.FullPath) || Directory.Exists(e.FullPath))
            {
                _logger.LogInformation($"File system change detected: {e.ChangeType} - {e.FullPath}");
                QueueChangeProcessing(e.FullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing change event: {ex.Message}", ex);
        }
    }

    private void QueueChangeProcessing(string path)
    {
        _pendingChanges.Enqueue(path);

        // Reset or start the debounce timer
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(ProcessPendingChangesAsync, null, DebounceMilliseconds, Timeout.Infinite);
    }

    private void ProcessPendingChangesAsync(object? state)
    {
        _ = ProcessPendingChangesTaskAsync();
    }

    private async Task ProcessPendingChangesTaskAsync()
    {
        // Ensure we're not already processing changes
        if (!await _processingLock.WaitAsync(0))
        {
            _logger.LogDebug("Change processing already in progress, skipping this batch");
            return;
        }

        try
        {
            // Process all pending changes
            if (_pendingChanges.IsEmpty)
            {
                _logger.LogDebug("No pending changes to process");
                return;
            }

            _logger.LogInformation("Processing pending file system changes");

            // Collect all paths for batch processing
            var pathsToProcess = new HashSet<string>();
            while (_pendingChanges.TryDequeue(out var path))
            {
                pathsToProcess.Add(path);
            }

            // Create a service scope for database operations
            using var scope = _scopeFactory.CreateScope();
            var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();

            await metadataService.ProcessFileSystemChangesAsync(pathsToProcess.ToList());

            _logger.LogInformation($"Completed processing {pathsToProcess.Count} file system changes");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing file system changes: {ex.Message}", ex);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();

        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error disposing watcher: {ex.Message}", ex);
            }
        }

        _processingLock.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
