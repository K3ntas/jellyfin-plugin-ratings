using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
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
        private readonly ISessionManager _sessionManager;
        private readonly RatingsRepository _repository;
        private readonly ILogger<NotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="sessionManager">Session manager for sending messages to clients.</param>
        /// <param name="repository">Ratings repository.</param>
        /// <param name="logger">Logger instance.</param>
        public NotificationService(
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            RatingsRepository repository,
            ILogger<NotificationService> logger)
        {
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
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

            // Send DisplayMessage to all native app clients
            _ = SendDisplayMessageToAllSessionsAsync(title, mediaType, year);
        }

        /// <summary>
        /// Sends a DisplayMessage to all active sessions for native app support.
        /// </summary>
        /// <param name="title">Media title.</param>
        /// <param name="mediaType">Type of media (Movie/Series).</param>
        /// <param name="year">Production year.</param>
        private async Task SendDisplayMessageToAllSessionsAsync(string title, string mediaType, int? year)
        {
            try
            {
                // Log ALL sessions for debugging
                var allSessions = _sessionManager.Sessions.ToList();
                _logger.LogInformation("Total sessions: {Count}", allSessions.Count);
                foreach (var s in allSessions)
                {
                    _logger.LogInformation("Session: Id={Id}, Device={Device}, Client={Client}, IsActive={IsActive}, SupportsRemoteControl={SupportsRemote}, SupportsMediaControl={SupportsMedia}",
                        s.Id, s.DeviceName, s.Client, s.IsActive, s.SupportsRemoteControl, s.SupportsMediaControl);
                }

                // Try sending to ALL active sessions
                var sessions = _sessionManager.Sessions
                    .Where(s => s.IsActive)
                    .ToList();

                if (sessions.Count == 0)
                {
                    _logger.LogWarning("No active sessions to send DisplayMessage to");
                    return;
                }

                // Warn about sessions without WebSocket (SupportsRemoteControl=false)
                // These sessions cannot receive DisplayMessage - typically caused by blocked WebSocket at reverse proxy
                var incapableSessions = sessions.Where(s => !s.SupportsRemoteControl).ToList();
                if (incapableSessions.Count > 0)
                {
                    _logger.LogWarning(
                        "WARNING: {Count} session(s) have SupportsRemoteControl=false and CANNOT receive notifications. " +
                        "This is usually caused by WebSocket being blocked at the reverse proxy. " +
                        "Affected devices: {Devices}",
                        incapableSessions.Count,
                        string.Join(", ", incapableSessions.Select(s => $"{s.DeviceName} ({s.Client})")));
                }

                var capableSessions = sessions.Where(s => s.SupportsRemoteControl).ToList();
                _logger.LogInformation("Sessions with WebSocket support: {Count}, without: {Count2}",
                    capableSessions.Count, incapableSessions.Count);

                var yearText = year.HasValue ? $" ({year})" : string.Empty;
                var header = mediaType == "Movie" ? "New Movie Available" : "New Series Available";
                var text = $"{title}{yearText}";

                var command = new GeneralCommand
                {
                    Name = GeneralCommandType.DisplayMessage
                };
                command.Arguments["Header"] = header;
                command.Arguments["Text"] = text;
                command.Arguments["TimeoutMs"] = "8000";

                foreach (var session in sessions)
                {
                    try
                    {
                        await _sessionManager.SendGeneralCommand(null, session.Id, command, CancellationToken.None).ConfigureAwait(false);
                        _logger.LogDebug("Sent DisplayMessage to session {SessionId} ({DeviceName})", session.Id, session.DeviceName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to send DisplayMessage to session {SessionId}", session.Id);
                    }
                }

                _logger.LogInformation("Sent DisplayMessage to {Count} active sessions for: {Title}", sessions.Count, title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending DisplayMessage to sessions");
            }
        }
    }
}
