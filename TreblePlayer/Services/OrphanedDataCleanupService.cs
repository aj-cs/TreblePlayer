using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // Use Microsoft.Extensions.Logging
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore; // Add this import for ToListAsync
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TreblePlayer.Data; // For repositories
using TreblePlayer.Models; // For Track
using Microsoft.AspNetCore.SignalR; // For Hub Context
using TreblePlayer.Core;
// using TreblePlayer.Hubs;      // Assuming DataHub is in root namespace now

namespace TreblePlayer.Services
{
    public class OrphanedDataCleanupService : IHostedService, IDisposable
    {
        // Use ILogger provided by .NET Core logging framework
        private readonly ILogger<OrphanedDataCleanupService> _logger; 
        private readonly IServiceScopeFactory _scopeFactory;
        private List<string> _monitoredFolders = new();
        private Timer? _timer;
        // Make intervals configurable or keep as constants
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // How often to check
        private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(1); // Delay after startup

        public OrphanedDataCleanupService(
            // Inject standard ILogger instead of custom ILoggingService for Hosted Services
            ILogger<OrphanedDataCleanupService> logger, 
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Orphaned Data Cleanup Service starting.");

            // Load monitored folders from database
            await LoadMonitoredFoldersAsync();

            if (!_monitoredFolders.Any())
            {
                _logger.LogWarning("OrphanedDataCleanupService: No monitored folders found. Service will not run cleanup checks.");
                return;
            }

            _logger.LogInformation($"Configured check interval: {_checkInterval}, Initial delay: {_initialDelay}");

            // Start the timer after an initial delay, then repeat at the interval
            _timer = new Timer(DoWorkCallback, null, _initialDelay, _checkInterval);
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
                
                // Filter to only existing directories
                _monitoredFolders = folders.Where(Directory.Exists).ToList();
                
                _logger.LogInformation($"Loaded {_monitoredFolders.Count} valid folders for monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading monitored folders: {ex.Message}", ex);
                
                // Fallback to default music directory
                var musicDirectory = Path.Combine(Directory.GetCurrentDirectory(), "music");
                if (Directory.Exists(musicDirectory))
                {
                    _monitoredFolders = new List<string> { musicDirectory };
                    _logger.LogInformation($"Using fallback music directory: {musicDirectory}");
                }
            }
        }

        // Wrapper method to handle async void limitation of Timer callback
        private void DoWorkCallback(object? state)
        {
            _logger.LogTrace("Timer callback triggered.");
            // Fire and forget the async work to avoid blocking the timer thread
            _ = DoWorkAsync(); 
        }

        private async Task DoWorkAsync()
        {
            _logger.LogInformation("Orphaned Data Cleanup Service is checking for unavailable folders/files...");

            bool changesMade = false;

            // Create a scope to resolve scoped services like repositories and DbContext
            using (var scope = _scopeFactory.CreateScope()) 
            {
                var trackRepository = scope.ServiceProvider.GetRequiredService<ITrackRepository>();
                var collectionRepository = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
                // Ensure DataHub is accessible here. If it's in a namespace, add a using statement at the top.
                var dataHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DataHub>>(); 

                List<Track> allDbTracks; 
                try
                {
                    allDbTracks = (await trackRepository.GetAllTracksAsync()).ToList();
                     _logger.LogInformation($"Found {allDbTracks.Count} tracks in the database to check.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching all tracks from database during cleanup check.");
                    return; // Cannot proceed without track list
                }
                
                var orphanedTracksOverall = new List<Track>();

                // 1. Check for missing folders
                foreach (var folderPath in _monitoredFolders)
                {
                    var absolutePath = Path.GetFullPath(folderPath);
                    try
                    {
                        if (!Directory.Exists(absolutePath))
                        {
                            _logger.LogWarning($"Monitored folder no longer exists: {absolutePath}. Checking for associated tracks.");

                            // Find tracks whose paths start with the missing folder path
                            string prefix = absolutePath.EndsWith(Path.DirectorySeparatorChar) 
                                ? absolutePath 
                                : absolutePath + Path.DirectorySeparatorChar;
                                
                            var orphanedTracksInFolder = allDbTracks
                                .Where(t => t.FilePath != null &&
                                            t.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            if (orphanedTracksInFolder.Any())
                            {
                                _logger.LogInformation($"Identified {orphanedTracksInFolder.Count} orphaned tracks for missing folder {absolutePath}.");
                                orphanedTracksOverall.AddRange(orphanedTracksInFolder);
                                // Remove from the main list to avoid checking them individually later if file check is enabled
                                allDbTracks.RemoveAll(t => orphanedTracksInFolder.Contains(t));
                            }
                        }
                    }
                    catch (Exception ex) 
                    {
                         _logger.LogError(ex, $"Error checking existence of folder {absolutePath}. Skipping cleanup for this folder.");
                    }
                }

                // Optional: 2. Check individual files within EXISTING folders (slower, uncomment if needed)
                /*
                _logger.LogInformation("Starting check for individual missing track files...");
                var tracksWithMissingFiles = new List<Track>();
                foreach (var track in allDbTracks) // Iterate remaining tracks
                {
                    try
                    {
                        if (track.FilePath != null && !File.Exists(track.FilePath))
                        {
                            _logger.LogWarning($"Track file missing: {track.FilePath}. Marking track ID {track.TrackId} for removal.");
                            tracksWithMissingFiles.Add(track);
                        }
                    }
                    catch(Exception ex)
                    {
                         _logger.LogError(ex, $"Error checking existence of file {track.FilePath}. Skipping check for this file.");
                    }
                }
                if(tracksWithMissingFiles.Any())
                {
                    orphanedTracksOverall.AddRange(tracksWithMissingFiles);
                }
                _logger.LogInformation("Finished check for individual missing track files.");
                */
                
                // --- Perform Deletion if any orphans were found --- 
                if (orphanedTracksOverall.Any())
                {
                    _logger.LogInformation($"Attempting to remove {orphanedTracksOverall.Count} total orphaned tracks from DB.");
                    try
                    {
                        await trackRepository.RemoveTracksFromDb(orphanedTracksOverall);
                        _logger.LogInformation("Orphaned tracks removed from DB. Now cleaning up empty collections.");
                        
                        // Check if albums/artists became empty after track removal
                        await collectionRepository.CleanupEmptyCollectionsAsync();
                        changesMade = true;
                    }
                    catch (Exception ex)
                    {
                         _logger.LogError(ex, "Error removing orphaned tracks or cleaning collections.");
                         // Depending on the error, maybe stop or continue
                    }
                }
                else
                {
                     _logger.LogInformation("No orphaned tracks found based on folder/file checks.");
                }

                // --- Send notification if cleanup occurred --- 
                if (changesMade)
                {
                    _logger.LogInformation("Cleanup finished and DB changes were made. Sending LibraryCleaned notification.");
                    try
                    {
                        await dataHubContext.Clients.All.SendAsync("LibraryCleaned");
                    }
                    catch (Exception ex)
                    {
                         _logger.LogError(ex, "Error sending LibraryCleaned SignalR notification.");
                    }
                }
            } // End of using scope

            _logger.LogInformation("Orphaned Data Cleanup Service check complete.");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Orphaned Data Cleanup Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            GC.SuppressFinalize(this);
        }

        // New public method to clean up tracks from a specific removed folder
        public async Task CleanupRemovedFolderAsync(string folderPath)
        {
            _logger.LogInformation($"Immediate cleanup requested for removed folder: {folderPath}");
            
            bool changesMade = false;
            
            // Create a scope to resolve scoped services
            using (var scope = _scopeFactory.CreateScope()) 
            {
                var trackRepository = scope.ServiceProvider.GetRequiredService<ITrackRepository>();
                var collectionRepository = scope.ServiceProvider.GetRequiredService<ITrackCollectionRepository>();
                var dataHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<DataHub>>();
                
                List<Track> allDbTracks;
                try
                {
                    allDbTracks = (await trackRepository.GetAllTracksAsync()).ToList();
                    _logger.LogInformation($"Found {allDbTracks.Count} tracks in the database to check against removed folder.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error fetching tracks from database during removed folder cleanup.");
                    throw; // Rethrow to let caller handle it
                }
                
                var orphanedTracks = new List<Track>();
                var absolutePath = Path.GetFullPath(folderPath);
                
                // Ensure path ends with directory separator for accurate path matching
                string prefix = absolutePath.EndsWith(Path.DirectorySeparatorChar) 
                    ? absolutePath 
                    : absolutePath + Path.DirectorySeparatorChar;
                    
                // Find all tracks whose file paths start with the removed folder path
                orphanedTracks = allDbTracks
                    .Where(t => t.FilePath != null && 
                                t.FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (orphanedTracks.Any())
                {
                    _logger.LogInformation($"Found {orphanedTracks.Count} tracks to remove from deleted folder: {folderPath}");
                    
                    try
                    {
                        // Remove the orphaned tracks
                        await trackRepository.RemoveTracksFromDb(orphanedTracks);
                        
                        // Clean up empty albums and playlists
                        await collectionRepository.CleanupEmptyCollectionsAsync();
                        
                        changesMade = true;
                        _logger.LogInformation($"Successfully removed {orphanedTracks.Count} tracks from deleted folder: {folderPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error removing tracks from deleted folder: {folderPath}");
                        throw; // Rethrow to let caller handle it
                    }
                }
                else
                {
                    _logger.LogInformation($"No tracks found in the database from folder: {folderPath}");
                }
                
                // Send notification if changes were made
                if (changesMade)
                {
                    try
                    {
                        await dataHubContext.Clients.All.SendAsync("LibraryUpdated");
                        await dataHubContext.Clients.All.SendAsync("LibraryCleaned");
                        _logger.LogInformation("Sent library update notifications after folder cleanup");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending notifications after folder cleanup");
                    }
                }
            }
            
            _logger.LogInformation($"Completed immediate cleanup for removed folder: {folderPath}");
        }
    }
} 