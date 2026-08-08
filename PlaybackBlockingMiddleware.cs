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

        private static readonly Regex AuthTokenRegex = new(
            @"Token=""([^""]+)""",
            RegexOptions.Compiled);

        // Resolving an auth token to a user hits the session store. During playback the same token
        // arrives on every segment request, so the result is memoised briefly. The TTL is short so
        // a revoked session stops being honoured almost immediately.
        private static readonly TimeSpan TokenCacheTtl = TimeSpan.FromSeconds(30);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (Guid UserId, DateTime Expires)> TokenCache = new(StringComparer.Ordinal);

        private static DateTime _lastTokenCachePrune = DateTime.UtcNow;

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
            // This middleware is registered through an IStartupFilter, which places it at the very
            // front of the pipeline - ahead of Jellyfin's own middleware. It therefore sees EVERY
            // request in the server: images, API calls, HLS segments. ToLowerInvariant() here
            // allocated a new string for every one of them; ordinal comparisons allocate nothing.
            var path = context.Request.Path.Value ?? string.Empty;

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
            if (path.Contains("/videos/", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/audio/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // PlaybackInfo requests
            if (path.Contains("/playbackinfo", StringComparison.OrdinalIgnoreCase))
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
        /// <remarks>
        /// This deliberately matches ONLY <c>POST /PlaybackInfo</c>, which the client sends exactly
        /// once when starting playback. It used to also match any GET containing "/stream",
        /// "master.m3u8" or "main.m3u8" - but during HLS playback the client re-fetches the
        /// playlist every few seconds and each segment is its own request, so a single stream
        /// incremented the user's quota hundreds of times per hour and rewrote media_quotas.json
        /// on each one. Quota is a count of playback starts, not of HTTP requests.
        /// </remarks>
        private static bool IsPlaybackStartRequest(string path, string method)
        {
            return method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                && path.Contains("/playbackinfo", StringComparison.OrdinalIgnoreCase);
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

            var now = DateTime.UtcNow;
            if (TokenCache.TryGetValue(token, out var cached) && cached.Expires > now)
            {
                return cached.UserId;
            }

            try
            {
                var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                var userId = session?.UserId ?? Guid.Empty;

                if (userId != Guid.Empty)
                {
                    TokenCache[token] = (userId, now.Add(TokenCacheTtl));
                    PruneTokenCache(now);
                }

                return userId;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Drops expired entries so the token cache cannot grow without bound.
        /// </summary>
        private static void PruneTokenCache(DateTime now)
        {
            if (now - _lastTokenCachePrune < TokenCacheTtl)
            {
                return;
            }

            _lastTokenCachePrune = now;

            foreach (var entry in TokenCache)
            {
                if (entry.Value.Expires <= now)
                {
                    TokenCache.TryRemove(entry.Key, out _);
                }
            }
        }
    }
}
