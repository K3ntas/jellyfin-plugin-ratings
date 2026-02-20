using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Background service that processes scheduled media deletions.
    /// </summary>
    public class DeletionService : IHostedService, IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly RatingsRepository _repository;
        private readonly ILogger<DeletionService> _logger;
        private Timer? _deletionTimer;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeletionService"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="repository">Ratings repository.</param>
        /// <param name="logger">Logger instance.</param>
        public DeletionService(
            ILibraryManager libraryManager,
            RatingsRepository repository,
            ILogger<DeletionService> logger)
        {
            _libraryManager = libraryManager;
            _repository = repository;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deletion service starting");

            // Run cleanup of expired data on startup
            try
            {
                await _repository.CleanupExpiredDataAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during startup cleanup");
            }

            // Check for due deletions every hour
            _deletionTimer = new Timer(
                ProcessPendingDeletions,
                null,
                TimeSpan.FromMinutes(1), // Initial delay - check shortly after startup
                TimeSpan.FromHours(1));  // Then check every hour
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deletion service stopping");

            _deletionTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">Whether disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _deletionTimer?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Processes all pending deletions that are due.
        /// </summary>
        private async void ProcessPendingDeletions(object? state)
        {
            try
            {
                // Run periodic cleanup of expired data (expired bans, inactive users, etc.)
                await _repository.CleanupExpiredDataAsync().ConfigureAwait(false);

                // Check if media management is enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableMediaManagement != true)
                {
                    return;
                }

                var pendingDeletions = _repository.GetPendingDeletions();

                if (pendingDeletions.Count == 0)
                {
                    return;
                }

                _logger.LogInformation("Processing {Count} pending deletions", pendingDeletions.Count);

                foreach (var deletion in pendingDeletions)
                {
                    try
                    {
                        // Get the item from library
                        var item = _libraryManager.GetItemById(deletion.ItemId);

                        if (item == null)
                        {
                            _logger.LogWarning("Item {ItemId} ({Title}) not found in library - may have been deleted manually. Removing deletion record.", deletion.ItemId, deletion.ItemTitle);
                            await _repository.RemoveDeletionAsync(deletion.ItemId).ConfigureAwait(false);
                            continue;
                        }

                        // Delete the item
                        _logger.LogInformation("Deleting item: {Title} ({ItemId})", deletion.ItemTitle, deletion.ItemId);

                        var deleteOptions = new DeleteOptions
                        {
                            DeleteFileLocation = true
                        };

                        _libraryManager.DeleteItem(item, deleteOptions);

                        // Remove the deletion record
                        await _repository.RemoveDeletionAsync(deletion.ItemId).ConfigureAwait(false);

                        _logger.LogInformation("Successfully deleted item: {Title} ({ItemId})", deletion.ItemTitle, deletion.ItemId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting item {ItemId} ({Title})", deletion.ItemId, deletion.ItemTitle);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending deletions");
            }
        }
    }
}
