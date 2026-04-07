using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Net.WebSocketMessages;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// WebSocket listener for server-wide notifications (restart countdown, etc.).
    /// This is a simplified version for main branch without social features.
    /// </summary>
    public class SocialWebSocketListener : IWebSocketListener, IDisposable
    {
        private readonly ILogger<SocialWebSocketListener> _logger;
        private static readonly ConcurrentDictionary<Guid, ConcurrentBag<IWebSocketConnection>> _userConnections = new();
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialWebSocketListener"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public SocialWebSocketListener(ILogger<SocialWebSocketListener> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public Task ProcessWebSocketConnectedAsync(IWebSocketConnection connection, HttpContext httpContext)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            var userId = connection.AuthorizationInfo?.UserId ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                return Task.CompletedTask;
            }

            var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentBag<IWebSocketConnection>());
            connections.Add(connection);
            _logger.LogDebug("WebSocket connected for user {UserId}", userId);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ProcessMessageAsync(WebSocketMessageInfo message)
        {
            // This simplified version doesn't process incoming messages
            return Task.CompletedTask;
        }

        /// <summary>
        /// Broadcasts a message to ALL connected clients.
        /// Used for server-wide notifications like restart countdown.
        /// </summary>
        /// <param name="message">The message to broadcast.</param>
        /// <returns>A task representing the broadcast operation.</returns>
        public async Task BroadcastToAllAsync(object message)
        {
            // Extract MessageType and Data from the message object
            var messageType = message.GetType().GetProperty("MessageType")?.GetValue(message)?.ToString() ?? "ServerNotification";
            var data = message.GetType().GetProperty("Data")?.GetValue(message) ?? message;

            foreach (var kvp in _userConnections)
            {
                var activeConnections = kvp.Value.Where(c => c.State == WebSocketState.Open).ToList();
                foreach (var conn in activeConnections)
                {
                    try
                    {
                        await SendMessageAsync(conn, messageType, data).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to broadcast to all");
                    }
                }
            }
        }

        /// <summary>
        /// Sends a message through the WebSocket connection.
        /// </summary>
        private async Task SendMessageAsync(IWebSocketConnection connection, string messageType, object data)
        {
            if (connection.State != WebSocketState.Open)
            {
                return;
            }

            try
            {
                var payload = new SocialWebSocketPayload
                {
                    SocialType = messageType,
                    SocialData = data
                };

                await connection.SendAsync(
                    new OutboundWebSocketMessage<SocialWebSocketPayload>
                    {
                        MessageType = SessionMessageType.KeepAlive,
                        Data = payload
                    },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error sending WebSocket message");
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">Whether we are disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _userConnections.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Payload wrapper for social WebSocket messages.
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
