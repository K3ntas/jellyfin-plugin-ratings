using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Service that monitors library for new media additions and creates notifications.
    /// </summary>
    public class NotificationService : IHostedService, IDisposable
    {
        private readonly ILibraryManager _libraryManager;
        private readonly RatingsRepository _repository;
        private readonly ILogger<NotificationService> _logger;
        private readonly ConcurrentQueue<NewMediaNotification> _notificationQueue;
        private readonly ConcurrentDictionary<string, List<PendingEpisode>> _pendingEpisodes;
        private readonly Random _random;
        private readonly object _pendingLock = new object();
        private Timer? _queueTimer;
        private Timer? _batchTimer;
        private bool _disposed;

        // How long to wait for more episodes before batching (seconds)
        private const int BatchDelaySeconds = 60;

        // Items added longer ago than this are part of the EXISTING library, not "new media".
        // Without this gate a metadata refresh, image re-download, edit, or re-scan of OLD media
        // fires ItemAdded/ItemUpdated and gets announced as newly added (e.g. a 3-month-old movie
        // reappearing as new). DateCreated is Jellyfin's "date added to library" timestamp.
        private static readonly TimeSpan MaxItemAgeForNotification = TimeSpan.FromDays(14);

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="repository">Ratings repository.</param>
        /// <param name="logger">Logger instance.</param>
        public NotificationService(
            ILibraryManager libraryManager,
            RatingsRepository repository,
            ILogger<NotificationService> logger)
        {
            _libraryManager = libraryManager;
            _repository = repository;
            _logger = logger;
            _notificationQueue = new ConcurrentQueue<NewMediaNotification>();
            _pendingEpisodes = new ConcurrentDictionary<string, List<PendingEpisode>>();
            _random = new Random();
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Subscribe to library events - both Added and Updated to catch metadata completion
            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemUpdated += OnItemUpdated;

            // Start queue processing timer - checks every 30 seconds
            _queueTimer = new Timer(ProcessNotificationQueue, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Start batch processing timer - checks every 15 seconds for episodes ready to batch
            _batchTimer = new Timer(ProcessPendingEpisodes, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Unsubscribe from events
            _libraryManager.ItemAdded -= OnItemAdded;
            _libraryManager.ItemUpdated -= OnItemUpdated;

            // Stop timers
            _queueTimer?.Change(Timeout.Infinite, 0);
            _batchTimer?.Change(Timeout.Infinite, 0);

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
                _queueTimer?.Dispose();
                _batchTimer?.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Processes the notification queue, releasing one notification at a time with random delays.
        /// </summary>
        private void ProcessNotificationQueue(object? state)
        {
            try
            {
                var queueCount = _notificationQueue.Count;

                if (_notificationQueue.TryDequeue(out var notification))
                {
                    // Update CreatedAt to NOW (when actually released), not when queued
                    // This ensures browser/TV polling catches it with lastNotificationCheck
                    notification.CreatedAt = DateTime.UtcNow;

                    _repository.AddNotification(notification);

                    // Schedule next notification with random delay (2-10 minutes)
                    // This ensures ALL queued items get shown, one by one
                    var remainingCount = _notificationQueue.Count;
                    if (remainingCount > 0)
                    {
                        var delayMs = _random.Next(120000, 600001); // 2-10 minutes in milliseconds
                        _queueTimer?.Change(delayMs, Timeout.Infinite);
                    }
                    else
                    {
                        // Queue empty - resume regular checks to catch new items
                        _queueTimer?.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                    }
                }
                else if (queueCount > 0)
                {
                    // TryDequeue failed but queue had items - this shouldn't happen
                    _logger.LogWarning("TryDequeue failed but queue reported {Count} items. Retrying in 10 seconds.", queueCount);
                    _queueTimer?.Change(10000, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing notification queue");
                // Ensure timer keeps running even after error
                _queueTimer?.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>
        /// Determines whether an item is eligible to produce a "new media" notification.
        /// Rejects items that were added to the library too long ago (so refreshes/re-scans of OLD
        /// media are never announced) and items we have already announced (persistent dedup that
        /// survives restarts).
        /// </summary>
        private bool ShouldNotify(BaseItem? item)
        {
            if (item == null)
            {
                return false;
            }

            // Gate on "date added to library". Old media that merely got refreshed is not new.
            if (DateTime.UtcNow - item.DateCreated > MaxItemAgeForNotification)
            {
                _logger.LogDebug(
                    "Skipping notification for '{Title}' - added {Date:u}, older than the {Days}-day new-media window",
                    item.Name, item.DateCreated, MaxItemAgeForNotification.TotalDays);
                return false;
            }

            // Persistent dedup - never announce the same item twice, even across restarts.
            if (_repository.HasNotifiedItem(item.Id))
            {
                _logger.LogDebug("Skipping notification for '{Title}' - already announced previously", item.Name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Handles item added events from the library.
        /// </summary>
        private void OnItemAdded(object? sender, ItemChangeEventArgs e)
        {
            try
            {
                // Check if notifications are enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableNewMediaNotifications != true)
                {
                    return;
                }

                var item = e.Item;

                // Skip old media (refresh/re-scan) and anything already announced.
                if (!ShouldNotify(item))
                {
                    return;
                }

                // Notify for movies, series, and episodes - but only if they have an image (metadata complete)
                if (item is Movie movie)
                {
                    if (!movie.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
                    {
                        _logger.LogDebug("Skipping notification for movie '{Title}' - no primary image yet", movie.Name);
                        return;
                    }
                    CreateNotification(movie.Id, movie.Name, "Movie", movie.ProductionYear, item);
                }
                else if (item is Series series)
                {
                    if (!series.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
                    {
                        _logger.LogDebug("Skipping notification for series '{Title}' - no primary image yet", series.Name);
                        return;
                    }
                    CreateNotification(series.Id, series.Name, "Series", series.ProductionYear, item);
                }
                else if (item is Episode episode)
                {
                    // For episodes, check if either episode or series has an image
                    var hasImage = episode.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ||
                                   (episode.Series?.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ?? false);
                    if (!hasImage)
                    {
                        _logger.LogDebug("Skipping notification for episode '{Title}' - no primary image yet", episode.Name);
                        return;
                    }
                    CreateEpisodeNotification(episode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item added event");
            }
        }

        /// <summary>
        /// Handles item updated events - catches when metadata/images are added after initial item creation.
        /// </summary>
        private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
        {
            try
            {
                // Check if notifications are enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableNewMediaNotifications != true)
                {
                    return;
                }

                var item = e.Item;

                // Skip old media (this is the main culprit: a metadata refresh of months-old media
                // fires ItemUpdated) and anything already announced.
                if (!ShouldNotify(item))
                {
                    return;
                }

                // Only process movies, series, and episodes that now have images
                if (item is Movie movie && movie.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
                {
                    _logger.LogDebug("Item updated with image, creating notification for movie: {Title}", movie.Name);
                    CreateNotification(movie.Id, movie.Name, "Movie", movie.ProductionYear, item);
                }
                else if (item is Series series && series.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
                {
                    _logger.LogDebug("Item updated with image, creating notification for series: {Title}", series.Name);
                    CreateNotification(series.Id, series.Name, "Series", series.ProductionYear, item);
                }
                else if (item is Episode episode)
                {
                    var hasImage = episode.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ||
                                   (episode.Series?.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ?? false);
                    if (hasImage)
                    {
                        _logger.LogDebug("Item updated with image, creating notification for episode: {Title}", episode.Name);
                        CreateEpisodeNotification(episode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item updated event");
            }
        }

        /// <summary>
        /// Cleans a media title by removing IMDB ID patterns like [tt14364480].
        /// </summary>
        private static string CleanTitle(string? title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            // Remove IMDB ID pattern [ttXXXXXXX] and trim
            return Regex.Replace(title, @"\s*\[tt\d+\]\s*", " ").Trim();
        }

        /// <summary>
        /// Creates a notification for a new media item and queues it for delayed release.
        /// </summary>
        private void CreateNotification(Guid itemId, string title, string mediaType, int? year, BaseItem item)
        {
            // Build image URL
            string? imageUrl = null;
            if (item.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
            {
                imageUrl = $"/Items/{itemId}/Images/Primary";
            }

            var cleanedTitle = CleanTitle(title);
            var notification = new NewMediaNotification
            {
                ItemId = itemId,
                Title = cleanedTitle,
                MediaType = mediaType,
                Year = year,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                IsTest = false
            };

            // Queue notification for delayed release
            _notificationQueue.Enqueue(notification);

            // Persistently mark as announced so it is never re-notified (survives restarts).
            _repository.MarkItemNotified(itemId);
        }

        /// <summary>
        /// Creates a pending episode entry for batching. Episodes are grouped by series+season.
        /// If grouping is disabled, creates individual notifications immediately.
        /// </summary>
        private void CreateEpisodeNotification(Episode episode)
        {
            // Build image URL - prefer series image for consistency across grouped episodes
            string? imageUrl = null;
            if (episode.Series != null && episode.Series.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
            {
                imageUrl = $"/Items/{episode.Series.Id}/Images/Primary";
            }
            else if (episode.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
            {
                imageUrl = $"/Items/{episode.Id}/Images/Primary";
            }

            // Get series name - try SeriesName first, then Series.Name as fallback
            var seriesName = !string.IsNullOrEmpty(episode.SeriesName)
                ? episode.SeriesName
                : episode.Series?.Name;
            var cleanedSeriesName = CleanTitle(seriesName);
            var cleanedTitle = CleanTitle(episode.Name);

            // Get season number - try multiple fallbacks
            // 1. ParentIndexNumber (direct property)
            // 2. Season.IndexNumber (Season object)
            // 3. GetParent() - from library structure
            var parentItem = episode.GetParent();
            int? seasonNumber = episode.ParentIndexNumber
                ?? episode.Season?.IndexNumber
                ?? (parentItem as MediaBrowser.Controller.Entities.TV.Season)?.IndexNumber;
            var episodeNumber = episode.IndexNumber;

            // Check if episode grouping is enabled
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableEpisodeGrouping != true)
            {
                // Grouping disabled - create individual notification immediately
                var notification = new NewMediaNotification
                {
                    ItemId = episode.Id,
                    Title = cleanedTitle,
                    MediaType = "Episode",
                    Year = episode.ProductionYear ?? episode.PremiereDate?.Year,
                    SeriesName = cleanedSeriesName,
                    SeasonNumber = seasonNumber,
                    EpisodeNumber = episodeNumber,
                    ImageUrl = imageUrl,
                    CreatedAt = DateTime.UtcNow,
                    IsTest = false
                };

                _notificationQueue.Enqueue(notification);
                _repository.MarkItemNotified(episode.Id);
                return;
            }

            // Create batch key: SeriesName|SeasonNumber
            var batchKey = $"{cleanedSeriesName}|{seasonNumber ?? 0}";

            var pendingEpisode = new PendingEpisode
            {
                EpisodeId = episode.Id,
                SeriesId = episode.Series?.Id ?? Guid.Empty,
                SeriesName = cleanedSeriesName,
                SeasonNumber = seasonNumber ?? 0,
                EpisodeNumber = episodeNumber ?? 0,
                Year = episode.ProductionYear ?? episode.PremiereDate?.Year,
                ImageUrl = imageUrl,
                AddedAt = DateTime.UtcNow
            };

            // Add to pending episodes (thread-safe)
            lock (_pendingLock)
            {
                if (!_pendingEpisodes.TryGetValue(batchKey, out var episodes))
                {
                    episodes = new List<PendingEpisode>();
                    _pendingEpisodes[batchKey] = episodes;
                }

                // Avoid duplicate episodes in the same batch
                if (!episodes.Any(e => e.EpisodeId == episode.Id))
                {
                    episodes.Add(pendingEpisode);
                }
            }

            // Persistently mark this episode as announced so it is never re-notified.
            _repository.MarkItemNotified(episode.Id);
        }

        /// <summary>
        /// Processes pending episodes and creates batched notifications for episodes that have been waiting long enough.
        /// </summary>
        private void ProcessPendingEpisodes(object? state)
        {
            var now = DateTime.UtcNow;
            var batchesToProcess = new List<(string Key, List<PendingEpisode> Episodes)>();

            lock (_pendingLock)
            {
                foreach (var kvp in _pendingEpisodes.ToList())
                {
                    var episodes = kvp.Value;
                    if (episodes.Count == 0)
                    {
                        _pendingEpisodes.TryRemove(kvp.Key, out _);
                        continue;
                    }

                    // Check if the most recent episode was added more than BatchDelaySeconds ago
                    var mostRecentAdd = episodes.Max(e => e.AddedAt);
                    if ((now - mostRecentAdd).TotalSeconds >= BatchDelaySeconds)
                    {
                        batchesToProcess.Add((kvp.Key, new List<PendingEpisode>(episodes)));
                        _pendingEpisodes.TryRemove(kvp.Key, out _);
                    }
                }
            }

            // Create grouped notifications for each batch
            foreach (var batch in batchesToProcess)
            {
                CreateGroupedEpisodeNotification(batch.Episodes);
            }
        }

        /// <summary>
        /// Creates a single grouped notification for multiple episodes of the same series/season.
        /// </summary>
        private void CreateGroupedEpisodeNotification(List<PendingEpisode> episodes)
        {
            if (episodes.Count == 0) return;

            var first = episodes[0];
            var episodeNumbers = episodes
                .Select(e => e.EpisodeNumber)
                .Where(n => n > 0)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var notification = new NewMediaNotification
            {
                ItemId = first.SeriesId != Guid.Empty ? first.SeriesId : first.EpisodeId,
                Title = first.SeriesName,
                MediaType = "Episode",
                Year = first.Year,
                SeriesName = first.SeriesName,
                SeasonNumber = first.SeasonNumber,
                EpisodeNumber = episodeNumbers.Count == 1 ? episodeNumbers[0] : (int?)null,
                EpisodeNumbers = episodeNumbers.Count > 1 ? episodeNumbers : null,
                ImageUrl = first.ImageUrl,
                CreatedAt = DateTime.UtcNow,
                IsTest = false
            };

            // Queue the grouped notification for release
            _notificationQueue.Enqueue(notification);
        }

        /// <summary>
        /// Formats a list of episode numbers into a readable range string (e.g., "E04-E08" or "E01, E03, E05").
        /// </summary>
        private static string FormatEpisodeRange(List<int> episodeNumbers)
        {
            if (episodeNumbers.Count == 0) return string.Empty;
            if (episodeNumbers.Count == 1) return $"E{episodeNumbers[0]:D2}";

            // Check if episodes are consecutive
            var isConsecutive = true;
            for (int i = 1; i < episodeNumbers.Count; i++)
            {
                if (episodeNumbers[i] != episodeNumbers[i - 1] + 1)
                {
                    isConsecutive = false;
                    break;
                }
            }

            if (isConsecutive)
            {
                return $"E{episodeNumbers[0]:D2}-E{episodeNumbers[^1]:D2}";
            }
            else
            {
                return string.Join(", ", episodeNumbers.Select(n => $"E{n:D2}"));
            }
        }

        /// <summary>
        /// Holds information about a pending episode notification before batching.
        /// </summary>
        private class PendingEpisode
        {
            public Guid EpisodeId { get; set; }
            public Guid SeriesId { get; set; }
            public string SeriesName { get; set; } = string.Empty;
            public int SeasonNumber { get; set; }
            public int EpisodeNumber { get; set; }
            public int? Year { get; set; }
            public string? ImageUrl { get; set; }
            public DateTime AddedAt { get; set; }
        }
    }
}
