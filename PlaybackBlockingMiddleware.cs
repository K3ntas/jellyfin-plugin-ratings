using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Middleware that blocks media playback for banned users or users who exceeded their quota.
    /// </summary>
    public class PlaybackBlockingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<PlaybackBlockingMiddleware> _logger;
        private readonly RatingsRepository _repository;
        private readonly ISessionManager _sessionManager;

        // Pre-compiled regex patterns for performance
        private static readonly Regex PlaybackInfoRegex = new(
            @"/items/[a-f0-9-]+/playbackinfo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VideoStreamRegex = new(
            @"/videos/[a-f0-9-]+/(stream|master\.m3u8)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AuthTokenRegex = new(
            @"Token=""([^""]+)""",
            RegexOptions.Compiled);

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackBlockingMiddleware"/> class.
        /// </summary>
        public PlaybackBlockingMiddleware(
            RequestDelegate next,
            ILogger<PlaybackBlockingMiddleware> logger,
            RatingsRepository repository,
            ISessionManager sessionManager)
        {
            _next = next;
            _logger = logger;
            _repository = repository;
            _sessionManager = sessionManager;
        }

        /// <summary>
        /// Processes the request and blocks media playback if the user is banned or quota exceeded.
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            // Only check playback-related requests
            if (!IsPlaybackRequest(path))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Get user ID from authentication
            var userId = await GetUserIdFromContextAsync(context).ConfigureAwait(false);
            if (userId == Guid.Empty)
            {
                // No user identified, let request through (will fail auth elsewhere)
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Check media ban
            var mediaBan = _repository.GetActiveChatBan(userId, "media");
            if (mediaBan != null)
            {
                _logger.LogWarning("Blocking media playback for banned user {UserId}", userId);
                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    message = "You are banned from watching media",
                    expiresAt = mediaBan.ExpiresAt,
                    reason = mediaBan.Reason,
                    isPermanent = mediaBan.IsPermanent
                })).ConfigureAwait(false);
                return;
            }

            // Check quota for actual playback start requests (not just info requests)
            if (IsPlaybackStartRequest(path, context.Request.Method))
            {
                if (_repository.IsMediaQuotaExceeded(userId))
                {
                    _logger.LogWarning("Blocking media playback for user {UserId} - quota exceeded", userId);
                    context.Response.StatusCode = 429;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        message = "Your media quota has been exceeded"
                    })).ConfigureAwait(false);
                    return;
                }

                // Increment usage counter
                await _repository.IncrementMediaUsageAsync(userId).ConfigureAwait(false);
            }

            await _next(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if the request is a playback-related request.
        /// </summary>
        private static bool IsPlaybackRequest(string path)
        {
            // Video/Audio streaming endpoints
            if (path.Contains("/videos/") || path.Contains("/audio/"))
            {
                return true;
            }

            // PlaybackInfo requests
            if (path.Contains("/playbackinfo"))
            {
                return true;
            }

            // Items play endpoints
            if (PlaybackInfoRegex.IsMatch(path))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if this is an actual playback start request (vs info/metadata).
        /// </summary>
        private static bool IsPlaybackStartRequest(string path, string method)
        {
            // POST to PlaybackInfo or actual stream requests count as playback
            if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Contains("/playbackinfo"))
            {
                return true;
            }

            // GET requests for actual video/audio streams
            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                if (path.Contains("/stream") || path.Contains("/master.m3u8") || path.Contains("/main.m3u8"))
                {
                    return true;
                }

                // Direct video file requests
                if (VideoStreamRegex.IsMatch(path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the user ID from the request context.
        /// </summary>
        private async Task<Guid> GetUserIdFromContextAsync(HttpContext context)
        {
            // Try to get from claims first
            var userIdClaim = context.User?.Claims?.FirstOrDefault(c => c.Type == "Jellyfin-UserId");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var claimUserId))
            {
                return claimUserId;
            }

            // Try from authorization header
            var authHeader = context.Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                          ?? context.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader))
            {
                return Guid.Empty;
            }

            var tokenMatch = AuthTokenRegex.Match(authHeader);
            if (!tokenMatch.Success)
            {
                return Guid.Empty;
            }

            var token = tokenMatch.Groups[1].Value;
            try
            {
                var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                return session?.UserId ?? Guid.Empty;
            }
            catch
            {
                return Guid.Empty;
            }
        }
    }
}
