using System;
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
    public class NotificationService : IHostedService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly RatingsRepository _repository;
        private readonly ILogger<NotificationService> _logger;

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
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService starting - subscribing to library events");

            // Subscribe to library item added events
            _libraryManager.ItemAdded += OnItemAdded;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("NotificationService stopping - unsubscribing from library events");

            // Unsubscribe from events
            _libraryManager.ItemAdded -= OnItemAdded;

            return Task.CompletedTask;
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

                // Only notify for movies and series (not episodes, seasons, etc.)
                if (item is Movie movie)
                {
                    CreateNotification(movie.Id, movie.Name, "Movie", movie.ProductionYear, item);
                }
                else if (item is Series series)
                {
                    CreateNotification(series.Id, series.Name, "Series", series.ProductionYear, item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling item added event");
            }
        }

        /// <summary>
        /// Creates a notification for a new media item.
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

            _repository.AddNotification(notification);
            _logger.LogInformation("Created notification for new {MediaType}: '{Title}' ({Year})", mediaType, title, year);
        }
    }
}
