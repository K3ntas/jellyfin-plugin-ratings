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
        // Secondary sanity gate only - the authoritative check is the persistent seen-media set
        // (stable content keys), because DateCreated is unreliable: a rescan/re-add resets it to
        // "now" on many setups (NAS/Docker), which is exactly how old media leaked through before.
        private static readonly TimeSpan MaxItemAgeForNotification = TimeSpan.FromDays(14);

        // Burst / mass-scan suppression. An initial library scan or a full re-add fires ItemAdded for
        // hundreds-to-thousands of items at once, every one stamped DateCreated=now - that is how a
        // whole existing collection got announced as "new". Genuine acquisitions trickle in. We count
        // DISTINCT NEW TITLES (a 60-episode season = one title) inside a rolling window; once that
        // exceeds the threshold we treat it as a scan and stay silent (recording everything as seen).
        // A single new season therefore still notifies, but importing a library does not.
        private static readonly TimeSpan BurstWindow = TimeSpan.FromMinutes(5);
        private const int BurstDistinctTitleThreshold = 20;
        private readonly object _burstLock = new object();
        private readonly Dictionary<string, DateTime> _recentNewTitles = new Dictionary<string, DateTime>();

        // False until the one-time baseline seeding of the existing library has finished. While false
        // we never notify - we only record items as seen - so the initial scan can never announce.
        private volatile bool _baselineReady;

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

            // Establish the one-time baseline off the startup path: record the whole existing library
            // as already-known so it is never announced as "new". Until this finishes, the event
            // handlers suppress (record-only), so even an in-progress first scan stays silent.
            _ = Task.Run(EnsureNotificationBaseline);

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
        /// One-time seeding of the existing library into the persistent seen-media set so the whole
        /// pre-existing collection is treated as already-known and is never announced as "new".
        /// Runs once ever (guarded by a disk marker); on every later start it just flips the ready flag.
        /// </summary>
        private void EnsureNotificationBaseline()
        {
            try
            {
                if (_repository.IsNotificationBaselineEstablished())
                {
                    return;
                }

                _logger.LogInformation("Establishing new-media notification baseline - recording existing library as known (no notifications will be sent for it)");

                var existing = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[]
                    {
                        Jellyfin.Data.Enums.BaseItemKind.Movie,
                        Jellyfin.Data.Enums.BaseItemKind.Series,
                        Jellyfin.Data.Enums.BaseItemKind.Episode
                    },
                    Recursive = true
                });

                _repository.MarkSeenMediaBulk(existing.Select(GetContentKey).Where(k => !string.IsNullOrEmpty(k)));
                _repository.SetNotificationBaselineEstablished();

                _logger.LogInformation("Notification baseline established for {Count} existing library items", existing.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error establishing notification baseline");
            }
            finally
            {
                // Always flip ready, even on failure: the seen-set + burst guard still protect us, and
                // we must not suppress genuine new media forever.
                _baselineReady = true;
            }
        }

        /// <summary>
        /// Builds a STABLE identity for a media item. Path is the most reliable per-file identity and
        /// survives Jellyfin re-creating the item with a new Guid; provider IDs are the fallback for
        /// items without a path; the Guid is the last resort. This - not the volatile Guid/DateCreated -
        /// is what we remember, so a re-add or refresh of an existing file is recognised as already-seen.
        /// </summary>
        private static string GetContentKey(BaseItem item)
        {
            var kind = item.GetType().Name;

            if (!string.IsNullOrEmpty(item.Path))
            {
                return kind + "|path|" + item.Path;
            }

            if (item.ProviderIds != null && item.ProviderIds.Count > 0)
            {
                var pid = item.ProviderIds
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => kv.Key + "=" + kv.Value)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(pid))
                {
                    return kind + "|pid|" + pid;
                }
            }

            return kind + "|id|" + item.Id.ToString("N");
        }

        /// <summary>
        /// Identity used to count DISTINCT titles for burst detection. Episodes collapse to their
        /// series, so adding a whole season counts as a single title (and still notifies), while a
        /// library import touches many distinct titles and trips the scan guard.
        /// </summary>
        private static string GetTitleKey(BaseItem item)
        {
            if (item is Episode ep)
            {
                if (ep.Series != null)
                {
                    return "series|" + ep.Series.Id.ToString("N");
                }

                if (!string.IsNullOrEmpty(ep.SeriesName))
                {
                    return "seriesName|" + ep.SeriesName;
                }
            }

            return GetContentKey(item);
        }

        /// <summary>
        /// Records a genuinely-new title in the rolling window and returns true if the number of
        /// distinct new titles in that window now exceeds the threshold (i.e. we are inside a scan/
        /// import burst rather than seeing individual acquisitions).
        /// </summary>
        private bool RegisterNewTitleAndIsBurst(BaseItem item)
        {
            var titleKey = GetTitleKey(item);
            var now = DateTime.UtcNow;

            lock (_burstLock)
            {
                _recentNewTitles[titleKey] = now;

                var cutoff = now - BurstWindow;
                var stale = _recentNewTitles.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
                foreach (var key in stale)
                {
                    _recentNewTitles.Remove(key);
                }

                return _recentNewTitles.Count > BurstDistinctTitleThreshold;
            }
        }

        /// <summary>
        /// True if the item (or, for an episode, its series) has a primary image, meaning metadata is
        /// complete enough to announce.
        /// </summary>
        private static bool HasUsableImage(BaseItem item)
        {
            if (item is Episode ep)
            {
                return ep.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ||
                       (ep.Series?.HasImage(MediaBrowser.Model.Entities.ImageType.Primary) ?? false);
            }

            return item.HasImage(MediaBrowser.Model.Entities.ImageType.Primary);
        }

        /// <summary>
        /// Single decision path for both ItemAdded and ItemUpdated. Decides whether a movie/series/
        /// episode is genuinely newly-acquired media that should be announced, and records what it
        /// has seen so the same item can never be announced twice.
        /// </summary>
        private void HandleItem(BaseItem? item)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableNewMediaNotifications != true)
            {
                return;
            }

            if (item is not (Movie or Series or Episode))
            {
                return;
            }

            var key = GetContentKey(item);

            // (1) Until the existing library has been baselined, never announce - just remember.
            //     This makes the initial scan of a brand-new install silent.
            if (!_baselineReady)
            {
                _repository.MarkSeenMedia(key);
                return;
            }

            // (2) Already seen: existing library, previously announced, OR a re-add / metadata refresh
            //     of the same underlying file that Jellyfin handed a new Guid and a reset DateCreated.
            //     This is the core fix for "already-present media keeps pinging as new".
            if (_repository.HasSeenMedia(key))
            {
                return;
            }

            // (3) Secondary sanity gate on "date added". Anything dated long ago is existing library.
            if (DateTime.UtcNow - item.DateCreated > MaxItemAgeForNotification)
            {
                _logger.LogDebug(
                    "Skipping '{Title}' - added {Date:u}, older than the {Days}-day new-media window",
                    item.Name, item.DateCreated, MaxItemAgeForNotification.TotalDays);
                _repository.MarkSeenMedia(key);
                return;
            }

            // (4) Burst / mass-scan guard: a library import floods with distinct new titles. Treat it
            //     as a scan and stay silent (recording everything as seen) once over the threshold.
            if (RegisterNewTitleAndIsBurst(item))
            {
                _logger.LogDebug("Suppressing '{Title}' - inside a mass-scan burst, treating as library scan not new media", item.Name);
                _repository.MarkSeenMedia(key);
                return;
            }

            // (5) Genuinely new, but only announce once a primary image exists. If not yet, DEFER:
            //     do not mark seen, so a later ItemUpdated (image arrived) gets another chance.
            if (!HasUsableImage(item))
            {
                _logger.LogDebug("Deferring '{Title}' - no primary image yet, awaiting metadata", item.Name);
                return;
            }

            Announce(item, key);
        }

        /// <summary>
        /// Creates the appropriate notification for a confirmed-new item and records it as seen so it
        /// is never announced again.
        /// </summary>
        private void Announce(BaseItem item, string key)
        {
            // Logged at Information level on purpose. When someone reports "it pinged for media
            // that is not new" (issue #65), this single line says exactly which identity the item
            // was filed under and how old the server thinks it is - which is what distinguishes a
            // genuine bug (key changed between scans, so the seen-set missed it) from a genuinely
            // new file. Without it the report is not actionable.
            _logger.LogInformation(
                "New-media notification for '{Title}' (key={Key}, dateCreated={Date:u}, ageDays={Age:F1})",
                item.Name,
                key,
                item.DateCreated,
                (DateTime.UtcNow - item.DateCreated).TotalDays);

            if (item is Movie movie)
            {
                CreateNotification(movie.Id, movie.Name, "Movie", movie.ProductionYear, item);
            }
            else if (item is Series series)
            {
                CreateNotification(series.Id, series.Name, "Series", series.ProductionYear, item);
            }
            else if (item is Episode episode)
            {
                CreateEpisodeNotification(episode);
            }

            _repository.MarkSeenMedia(key);
        }

        /// <summary>
        /// Handles item added events from the library.
        /// </summary>
        private void OnItemAdded(object? sender, ItemChangeEventArgs e)
        {
            try
            {
                HandleItem(e.Item);
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
                HandleItem(e.Item);
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

            // Queue notification for delayed release. The item is recorded as seen by the caller
            // (Announce -> MarkSeenMedia) so it is never re-announced, even across restarts/re-adds.
            _notificationQueue.Enqueue(notification);
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

            // The episode is recorded as seen by the caller (Announce -> MarkSeenMedia).
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
