using System;
using System.Collections.Concurrent;
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
        private readonly Random _random;
        private Timer? _queueTimer;
        private bool _disposed;

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
            _random = new Random();
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService starting - subscribing to library events");

            // Subscribe to library item added events
            _libraryManager.ItemAdded += OnItemAdded;

            // Start queue processing timer - checks every 30 seconds
            _queueTimer = new Timer(ProcessNotificationQueue, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService stopping - unsubscribing from library events");

            // Unsubscribe from events
            _libraryManager.ItemAdded -= OnItemAdded;

            // Stop timer
            _queueTimer?.Change(Timeout.Infinite, 0);

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
            }

            _disposed = true;
        }

        /// <summary>
        /// Processes the notification queue, releasing one notification at a time with random delays.
        /// </summary>
        private void ProcessNotificationQueue(object? state)
        {
            if (_notificationQueue.TryDequeue(out var notification))
            {
                _repository.AddNotification(notification);
                _logger.LogInformation(
                    "Released queued notification: {MediaType} - '{Title}'",
                    notification.MediaType,
                    notification.Title);

                // If there are more notifications, schedule next one with random delay (1-3 minutes)
                if (!_notificationQueue.IsEmpty)
                {
                    var delayMs = _random.Next(60000, 180001); // 1-3 minutes in milliseconds
                    _logger.LogInformation("Next notification will be released in {Seconds} seconds", delayMs / 1000);
                    _queueTimer?.Change(delayMs, Timeout.Infinite);
                }
                else
                {
                    // Resume regular 30-second checks
                    _queueTimer?.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                }
            }
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

                // Notify for movies, series, and episodes
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item added event");
            }
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

            var notification = new NewMediaNotification
            {
                ItemId = itemId,
                Title = title,
                MediaType = mediaType,
                Year = year,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                IsTest = false
            };

            // Queue notification for delayed release
            _notificationQueue.Enqueue(notification);
            _logger.LogInformation("Queued notification for new {MediaType}: '{Title}' ({Year}). Queue size: {QueueSize}", mediaType, title, year, _notificationQueue.Count);
        }

        /// <summary>
        /// Creates a notification for a new episode and queues it for delayed release.
        /// </summary>
        private void CreateEpisodeNotification(Episode episode)
        {
            // Build image URL - prefer episode image, fall back to series image
            string? imageUrl = null;
            if (episode.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
            {
                imageUrl = $"/Items/{episode.Id}/Images/Primary";
            }
            else if (episode.Series != null && episode.Series.HasImage(MediaBrowser.Model.Entities.ImageType.Primary))
            {
                imageUrl = $"/Items/{episode.Series.Id}/Images/Primary";
            }

            var notification = new NewMediaNotification
            {
                ItemId = episode.Id,
                Title = episode.Name,
                MediaType = "Episode",
                Year = episode.ProductionYear ?? episode.PremiereDate?.Year,
                SeriesName = episode.SeriesName,
                SeasonNumber = episode.ParentIndexNumber,
                EpisodeNumber = episode.IndexNumber,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                IsTest = false
            };

            // Queue notification for delayed release
            _notificationQueue.Enqueue(notification);
            _logger.LogInformation(
                "Queued notification for new Episode: '{SeriesName}' S{Season:D2}E{Episode:D2} - '{Title}'. Queue size: {QueueSize}",
                episode.SeriesName,
                episode.ParentIndexNumber,
                episode.IndexNumber,
                episode.Name,
                _notificationQueue.Count);
        }
    }
}
