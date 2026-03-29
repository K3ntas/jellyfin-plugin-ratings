using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// Handles WebSocket connections for real-time social status updates.
    /// </summary>
    public class SocialWebSocketHandler
    {
        private readonly ILogger<SocialWebSocketHandler> _logger;
        private readonly SocialRepository _socialRepository;
        private readonly ISessionManager _sessionManager;
        private readonly IUserManager _userManager;

        // Thread-safe dictionary of connected clients: UserId -> List of connections
        private static readonly ConcurrentDictionary<Guid, ConcurrentBag<WebSocketConnection>> _connections = new();

        // Rate limiting: UserId -> last message time
        private static readonly ConcurrentDictionary<Guid, DateTime> _lastBroadcast = new();
        private static readonly TimeSpan _minBroadcastInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialWebSocketHandler"/> class.
        /// </summary>
        public SocialWebSocketHandler(
            ILogger<SocialWebSocketHandler> logger,
            SocialRepository socialRepository,
            ISessionManager sessionManager,
            IUserManager userManager)
        {
            _logger = logger;
            _socialRepository = socialRepository;
            _sessionManager = sessionManager;
            _userManager = userManager;
        }

        /// <summary>
        /// Handles a new WebSocket connection.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="webSocket">The WebSocket.</param>
        /// <returns>Task.</returns>
        public async Task HandleConnectionAsync(HttpContext context, WebSocket webSocket)
        {
            Guid? userId = null;

            try
            {
                // Authenticate the connection
                userId = await AuthenticateAsync(context).ConfigureAwait(false);
                if (userId == null)
                {
                    _logger.LogWarning("[SocialWS] Unauthorized connection attempt from {IP}", context.Connection.RemoteIpAddress);
                    await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unauthorized", CancellationToken.None);
                    return;
                }

                var connection = new WebSocketConnection
                {
                    UserId = userId.Value,
                    Socket = webSocket,
                    ConnectedAt = DateTime.UtcNow,
                    ConnectionId = Guid.NewGuid()
                };

                // Add to connections
                var userConnections = _connections.GetOrAdd(userId.Value, _ => new ConcurrentBag<WebSocketConnection>());
                userConnections.Add(connection);

                _logger.LogInformation("[SocialWS] User {UserId} connected (ConnectionId: {ConnectionId})", userId, connection.ConnectionId);

                // Send initial status of all friends
                await SendInitialStatusAsync(connection).ConfigureAwait(false);

                // Keep connection alive and handle incoming messages
                await ReceiveLoopAsync(connection).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                _logger.LogDebug(ex, "[SocialWS] WebSocket error for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SocialWS] Unexpected error for user {UserId}", userId);
            }
            finally
            {
                // Remove from connections
                if (userId.HasValue)
                {
                    RemoveConnection(userId.Value, webSocket);
                    _logger.LogInformation("[SocialWS] User {UserId} disconnected", userId);
                }

                // Ensure socket is closed
                if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
                    }
                    catch
                    {
                        // Ignore close errors
                    }
                }
            }
        }

        /// <summary>
        /// Broadcasts a status update to all friends of a user.
        /// </summary>
        /// <param name="userId">The user whose status changed.</param>
        /// <param name="username">The username.</param>
        /// <param name="status">The new status.</param>
        /// <param name="watching">What they're watching.</param>
        /// <returns>Task.</returns>
        public async Task BroadcastStatusUpdateAsync(Guid userId, string username, UserOnlineStatus status, CurrentlyWatching? watching)
        {
            // Rate limiting - don't spam updates
            if (_lastBroadcast.TryGetValue(userId, out var lastTime) && DateTime.UtcNow - lastTime < _minBroadcastInterval)
            {
                return;
            }

            _lastBroadcast[userId] = DateTime.UtcNow;

            // Get user's privacy settings
            var profile = _socialRepository.GetProfile(userId);

            // Get all friends
            var friendIds = _socialRepository.GetFriendIds(userId);
            if (friendIds.Count == 0) return;

            // Build the update message
            var effectiveStatus = status.Status == "Invisible" ? "Offline" : status.Status;
            var message = new SocialWebSocketMessage
            {
                Type = "StatusUpdate",
                Data = new
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
                }
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            // Send to each friend who is connected
            foreach (var friendId in friendIds)
            {
                // Check privacy settings
                if (profile != null)
                {
                    if (profile.Privacy.ShowOnlineStatus == "Nobody") continue;
                    // Friends visibility is OK since we're only sending to friends
                }

                if (_connections.TryGetValue(friendId, out var friendConnections))
                {
                    foreach (var conn in friendConnections.ToList())
                    {
                        await SendMessageAsync(conn, bytes).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the count of connected users.
        /// </summary>
        public int GetConnectedUserCount() => _connections.Count;

        /// <summary>
        /// Gets the total connection count.
        /// </summary>
        public int GetTotalConnectionCount() => _connections.Values.Sum(bag => bag.Count);

        #region Private Methods

        private async Task<Guid?> AuthenticateAsync(HttpContext context)
        {
            // Get token from query string or header
            var token = context.Request.Query["token"].FirstOrDefault()
                ?? context.Request.Headers["X-Emby-Token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            try
            {
                var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                return session?.UserId;
            }
            catch
            {
                return null;
            }
        }

        private async Task SendInitialStatusAsync(WebSocketConnection connection)
        {
            var friendIds = _socialRepository.GetFriendIds(connection.UserId);
            var statuses = _socialRepository.GetOnlineStatuses(friendIds);

            var friendStatuses = new List<object>();

            foreach (var friendId in friendIds)
            {
                var user = _userManager.GetUserById(friendId);
                var profile = _socialRepository.GetProfile(friendId);
                statuses.TryGetValue(friendId, out var friendStatus);

                // Check privacy
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

            var message = new SocialWebSocketMessage
            {
                Type = "InitialStatus",
                Data = new { friends = friendStatuses }
            };

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await SendMessageAsync(connection, bytes).ConfigureAwait(false);
        }

        private async Task ReceiveLoopAsync(WebSocketConnection connection)
        {
            var buffer = new byte[1024];

            while (connection.Socket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await connection.Socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    // Handle incoming messages (ping/pong, etc.)
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageText = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await HandleClientMessageAsync(connection, messageText).ConfigureAwait(false);
                    }
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }

        private async Task HandleClientMessageAsync(WebSocketConnection connection, string messageText)
        {
            try
            {
                var message = JsonSerializer.Deserialize<SocialWebSocketMessage>(messageText);
                if (message == null) return;

                switch (message.Type)
                {
                    case "Ping":
                        // Respond with pong
                        var pong = new SocialWebSocketMessage { Type = "Pong" };
                        var json = JsonSerializer.Serialize(pong);
                        await SendMessageAsync(connection, Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
                        break;

                    case "RefreshStatus":
                        // Client requests a refresh of all friend statuses
                        await SendInitialStatusAsync(connection).ConfigureAwait(false);
                        break;
                }
            }
            catch (JsonException)
            {
                // Invalid JSON, ignore
            }
        }

        private async Task SendMessageAsync(WebSocketConnection connection, byte[] data)
        {
            if (connection.Socket.State != WebSocketState.Open) return;

            try
            {
                await connection.SendLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await connection.Socket.SendAsync(
                        new ArraySegment<byte>(data),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    connection.SendLock.Release();
                }
            }
            catch (WebSocketException)
            {
                // Connection lost, will be cleaned up
            }
        }

        private void RemoveConnection(Guid userId, WebSocket socket)
        {
            if (_connections.TryGetValue(userId, out var userConnections))
            {
                // Create new bag without the disconnected socket
                var remaining = new ConcurrentBag<WebSocketConnection>(
                    userConnections.Where(c => c.Socket != socket));

                if (remaining.IsEmpty)
                {
                    _connections.TryRemove(userId, out _);
                }
                else
                {
                    _connections[userId] = remaining;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a WebSocket connection.
    /// </summary>
    public class WebSocketConnection
    {
        /// <summary>
        /// Gets or sets the connection ID.
        /// </summary>
        public Guid ConnectionId { get; set; }

        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket.
        /// </summary>
        public WebSocket Socket { get; set; } = null!;

        /// <summary>
        /// Gets or sets when the connection was established.
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// Gets the send lock for thread-safe sending.
        /// </summary>
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }

    /// <summary>
    /// WebSocket message format.
    /// </summary>
    public class SocialWebSocketMessage
    {
        /// <summary>
        /// Gets or sets the message type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message data.
        /// </summary>
        public object? Data { get; set; }
    }
}
