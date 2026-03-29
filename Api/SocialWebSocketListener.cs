using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Net.WebSocketMessages;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// WebSocket listener for real-time social status updates.
    /// Implements Jellyfin's IWebSocketListener to hook into the main /socket endpoint.
    /// </summary>
    public class SocialWebSocketListener : IWebSocketListener
    {
        private readonly ILogger<SocialWebSocketListener> _logger;
        private readonly SocialRepository _socialRepository;
        private readonly IUserManager _userManager;

        // Track connected users and their WebSocket connections
        private static readonly ConcurrentDictionary<Guid, ConcurrentBag<IWebSocketConnection>> _userConnections = new();

        // Track who is viewing which profile (viewerId -> profileUserId)
        private static readonly ConcurrentDictionary<Guid, Guid> _profileViewers = new();

        // Rate limiting for broadcasts
        private static readonly ConcurrentDictionary<Guid, DateTime> _lastBroadcast = new();
        private static readonly TimeSpan _minBroadcastInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialWebSocketListener"/> class.
        /// </summary>
        public SocialWebSocketListener(
            ILogger<SocialWebSocketListener> logger,
            SocialRepository socialRepository,
            IUserManager userManager)
        {
            _logger = logger;
            _socialRepository = socialRepository;
            _userManager = userManager;
        }

        /// <summary>
        /// Called when a WebSocket client connects.
        /// </summary>
        public Task ProcessWebSocketConnectedAsync(IWebSocketConnection connection, HttpContext httpContext)
        {
            var userId = connection.AuthorizationInfo?.UserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Task.CompletedTask;
            }

            // Add connection to user's connection bag
            var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentBag<IWebSocketConnection>());
            connections.Add(connection);

            _logger.LogDebug("[SocialWS] User {UserId} connected", userId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a WebSocket message is received.
        /// </summary>
        public async Task ProcessMessageAsync(WebSocketMessageInfo message)
        {
            // Handle our custom message types
            if (message.MessageType == SessionMessageType.KeepAlive)
            {
                return;
            }

            // Check for our custom Social message types
            var messageTypeStr = message.MessageType.ToString();

            // We use custom string types prefixed with "Social"
            if (messageTypeStr.StartsWith("Social", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSocialMessageAsync(message).ConfigureAwait(false);
            }
        }

        private async Task HandleSocialMessageAsync(WebSocketMessageInfo message)
        {
            var userId = message.Connection?.AuthorizationInfo?.UserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return;
            }

            var messageType = message.MessageType.ToString();

            switch (messageType)
            {
                case "SocialSubscribe":
                    // Client wants to subscribe to social updates
                    if (message.Connection != null)
                    {
                        await SendInitialStatusAsync(userId, message.Connection).ConfigureAwait(false);
                    }
                    break;

                case "SocialPing":
                    // Respond with pong
                    if (message.Connection != null)
                    {
                        await SendMessageAsync(message.Connection, "SocialPong", new { timestamp = DateTime.UtcNow }).ConfigureAwait(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Sends initial friend status to a newly subscribed client.
        /// </summary>
        private async Task SendInitialStatusAsync(Guid userId, IWebSocketConnection connection)
        {
            var friendIds = _socialRepository.GetFriendIds(userId);
            var statuses = _socialRepository.GetOnlineStatuses(friendIds);

            var friendStatuses = new List<object>();

            foreach (var friendId in friendIds)
            {
                var user = _userManager.GetUserById(friendId);
                var profile = _socialRepository.GetProfile(friendId);
                statuses.TryGetValue(friendId, out var friendStatus);

                var showStatus = profile == null ||
                    profile.Privacy.ShowOnlineStatus == "Everyone" ||
                    profile.Privacy.ShowOnlineStatus == "Friends";

                var showWatching = profile == null ||
                    profile.Privacy.ShowCurrentlyWatching == "Everyone" ||
                    profile.Privacy.ShowCurrentlyWatching == "Friends";

                var effectiveStatus = friendStatus?.Status ?? "Offline";
                if (effectiveStatus == "Invisible") effectiveStatus = "Offline";

                friendStatuses.Add(new
                {
                    userId = friendId,
                    username = user?.Username ?? "Unknown",
                    status = showStatus ? effectiveStatus : "Offline",
                    lastSeen = showStatus ? friendStatus?.LastSeen : null,
                    watching = showWatching && effectiveStatus != "Offline" && friendStatus?.Watching != null ? new
                    {
                        itemId = friendStatus.Watching.ItemId,
                        title = friendStatus.Watching.Title,
                        type = friendStatus.Watching.Type,
                        seriesName = friendStatus.Watching.SeriesName,
                        episodeInfo = friendStatus.Watching.EpisodeInfo,
                        position = friendStatus.Watching.FormattedPosition,
                        duration = friendStatus.Watching.FormattedDuration,
                        progress = friendStatus.Watching.ProgressPercent
                    } : null
                });
            }

            await SendMessageAsync(connection, "SocialInitialStatus", new { friends = friendStatuses }).ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcasts a status update to all friends of a user.
        /// Called from SocialController when heartbeat is received.
        /// </summary>
        public async Task BroadcastStatusUpdateAsync(Guid userId, string username, UserOnlineStatus status, CurrentlyWatching? watching, bool skipRateLimit = false)
        {
            // Rate limiting (skip for important events like going offline)
            if (!skipRateLimit && _lastBroadcast.TryGetValue(userId, out var lastTime) && DateTime.UtcNow - lastTime < _minBroadcastInterval)
            {
                return;
            }
            _lastBroadcast[userId] = DateTime.UtcNow;

            _logger.LogDebug("[SocialWS] Broadcasting status update for {UserId}: {Status}", userId, status.Status);

            // Get user's privacy settings
            var profile = _socialRepository.GetProfile(userId);

            // Get all friends
            var friendIds = _socialRepository.GetFriendIds(userId);

            // Build the update message
            var effectiveStatus = status.Status == "Invisible" ? "Offline" : status.Status;
            var updateData = new
            {
                userId = userId,
                username = username,
                status = effectiveStatus,
                lastSeen = status.LastSeen,
                watching = watching != null && effectiveStatus != "Offline" ? new
                {
                    itemId = watching.ItemId,
                    title = watching.Title,
                    type = watching.Type,
                    seriesName = watching.SeriesName,
                    episodeInfo = watching.EpisodeInfo,
                    position = watching.FormattedPosition,
                    duration = watching.FormattedDuration,
                    progress = watching.ProgressPercent
                } : null
            };

            // Send to each friend who is connected
            foreach (var friendId in friendIds)
            {
                // Check privacy settings
                if (profile?.Privacy.ShowOnlineStatus == "Nobody") continue;

                if (_userConnections.TryGetValue(friendId, out var friendConnections))
                {
                    var activeConnections = friendConnections.Where(c => c.State == WebSocketState.Open).ToList();

                    foreach (var conn in activeConnections)
                    {
                        try
                        {
                            await SendMessageAsync(conn, "SocialStatusUpdate", updateData).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[SocialWS] Failed to send to friend {FriendId}", friendId);
                        }
                    }
                }
            }

            // Also send to anyone viewing this user's profile
            var profileViewers = _profileViewers.Where(kvp => kvp.Value == userId).Select(kvp => kvp.Key).ToList();
            foreach (var viewerId in profileViewers)
            {
                if (viewerId == userId) continue; // Don't send to self (already handled above if friend)

                if (_userConnections.TryGetValue(viewerId, out var viewerConnections))
                {
                    var activeConnections = viewerConnections.Where(c => c.State == WebSocketState.Open).ToList();
                    foreach (var conn in activeConnections)
                    {
                        try
                        {
                            await SendMessageAsync(conn, "SocialProfileStatusUpdate", updateData).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[SocialWS] Failed to send profile status to viewer {ViewerId}", viewerId);
                        }
                    }
                }
            }

            // Clean up dead connections
            CleanupDeadConnections();
        }

        private async Task SendMessageAsync(IWebSocketConnection connection, string messageType, object data)
        {
            if (connection.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                // Use Jellyfin's SendAsync method with proper message format
                // We wrap our data in an outbound message with a custom payload
                var payload = new SocialWebSocketPayload
                {
                    SocialType = messageType,
                    SocialData = data
                };

                await connection.SendAsync(
                    new OutboundWebSocketMessage<SocialWebSocketPayload>
                    {
                        MessageType = SessionMessageType.KeepAlive, // Use KeepAlive as carrier
                        Data = payload
                    },
                    System.Threading.CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SocialWS] SendAsync failed");
            }
        }

        private void CleanupDeadConnections()
        {
            foreach (var kvp in _userConnections)
            {
                var activeConnections = kvp.Value.Where(c => c.State == WebSocketState.Open).ToList();

                if (activeConnections.Count == 0)
                {
                    _userConnections.TryRemove(kvp.Key, out _);
                }
                else if (activeConnections.Count != kvp.Value.Count)
                {
                    _userConnections[kvp.Key] = new ConcurrentBag<IWebSocketConnection>(activeConnections);
                }
            }
        }

        /// <summary>
        /// Gets the count of connected users.
        /// </summary>
        public int GetConnectedUserCount() => _userConnections.Count;

        /// <summary>
        /// Gets the total connection count.
        /// </summary>
        public int GetTotalConnectionCount() => _userConnections.Values.Sum(bag => bag.Count);

        /// <summary>
        /// Registers that a user is viewing a profile.
        /// </summary>
        public void RegisterProfileViewer(Guid viewerId, Guid profileUserId)
        {
            _profileViewers[viewerId] = profileUserId;
            _logger.LogDebug("[SocialWS] User {ViewerId} viewing profile {ProfileId}", viewerId, profileUserId);
        }

        /// <summary>
        /// Unregisters a profile viewer.
        /// </summary>
        public void UnregisterProfileViewer(Guid viewerId)
        {
            _profileViewers.TryRemove(viewerId, out _);
        }

        /// <summary>
        /// Broadcasts profile stats update to all users viewing that profile.
        /// </summary>
        public async Task BroadcastProfileStatsUpdateAsync(Guid profileUserId, object stats)
        {
            var updateData = new
            {
                profileUserId = profileUserId,
                stats = stats
            };

            // Find all users viewing this profile
            var viewers = _profileViewers.Where(kvp => kvp.Value == profileUserId).Select(kvp => kvp.Key).ToList();

            foreach (var viewerId in viewers)
            {
                if (_userConnections.TryGetValue(viewerId, out var viewerConnections))
                {
                    var activeConnections = viewerConnections.Where(c => c.State == WebSocketState.Open).ToList();
                    foreach (var conn in activeConnections)
                    {
                        try
                        {
                            await SendMessageAsync(conn, "SocialProfileStatsUpdate", updateData).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[SocialWS] Failed to send stats update to {ViewerId}", viewerId);
                        }
                    }
                }
            }

            // Also send to the profile owner themselves (in case they're viewing their own profile)
            if (_userConnections.TryGetValue(profileUserId, out var ownerConnections))
            {
                var activeOwnerConnections = ownerConnections.Where(c => c.State == WebSocketState.Open).ToList();
                foreach (var conn in activeOwnerConnections)
                {
                    try
                    {
                        await SendMessageAsync(conn, "SocialProfileStatsUpdate", updateData).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[SocialWS] Failed to send stats update to owner {OwnerId}", profileUserId);
                    }
                }
            }
        }

        /// <summary>
        /// Broadcasts ONLY watching update to friends. Does NOT include status.
        /// This is completely separate from status updates.
        /// </summary>
        public async Task BroadcastWatchingUpdateAsync(Guid userId, string username, CurrentlyWatching? watching)
        {
            // Get all friends
            var friendIds = _socialRepository.GetFriendIds(userId);

            // Build watching-only update
            var updateData = new
            {
                userId = userId,
                username = username,
                watching = watching != null ? new
                {
                    itemId = watching.ItemId,
                    title = watching.Title,
                    type = watching.Type,
                    seriesName = watching.SeriesName,
                    episodeInfo = watching.EpisodeInfo,
                    position = watching.FormattedPosition,
                    duration = watching.FormattedDuration,
                    progress = watching.ProgressPercent
                } : null
            };

            // Send to each friend
            foreach (var friendId in friendIds)
            {
                if (_userConnections.TryGetValue(friendId, out var friendConnections))
                {
                    var activeConnections = friendConnections.Where(c => c.State == WebSocketState.Open).ToList();
                    foreach (var conn in activeConnections)
                    {
                        try
                        {
                            await SendMessageAsync(conn, "SocialWatchingUpdate", updateData).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[SocialWS] Failed to send watching update to {FriendId}", friendId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Broadcasts a generic social event to specific users.
        /// </summary>
        public async Task BroadcastToUsersAsync(IEnumerable<Guid> userIds, string messageType, object data)
        {
            foreach (var userId in userIds)
            {
                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    var activeConnections = connections.Where(c => c.State == WebSocketState.Open).ToList();
                    foreach (var conn in activeConnections)
                    {
                        try
                        {
                            await SendMessageAsync(conn, messageType, data).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[SocialWS] Failed to broadcast to {UserId}", userId);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Payload for social WebSocket messages.
    /// </summary>
    public class SocialWebSocketPayload
    {
        /// <summary>
        /// Gets or sets the social message type.
        /// </summary>
        public string SocialType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the social message data.
        /// </summary>
        public object? SocialData { get; set; }
    }
}
