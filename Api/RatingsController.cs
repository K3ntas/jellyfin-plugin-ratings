using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using MediaBrowser.Common.Configuration;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// Ratings API controller.
    /// </summary>
    [ApiController]
    [Route("Ratings")]
    [Produces(MediaTypeNames.Application.Json)]
    public class RatingsController : ControllerBase
    {
        private readonly RatingsRepository _repository;
        private readonly IUserManager _userManager;
        private readonly ILibraryManager _libraryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IUserDataManager _userDataManager;
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<RatingsController> _logger;
        private readonly SocialWebSocketListener _socialWebSocketListener;
        private readonly ISystemManager _systemManager;

        // Server restart state
        private static CancellationTokenSource? _restartCts;
        private static DateTime? _restartScheduledAt;
        private static string? _restartReason;

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingsController"/> class.
        /// </summary>
        /// <param name="repository">Ratings repository.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="userDataManager">User data manager.</param>
        /// <param name="appPaths">Application paths.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="socialWebSocketListener">Social WebSocket listener.</param>
        /// <param name="systemManager">System manager for server control.</param>
        public RatingsController(
            RatingsRepository repository,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            IUserDataManager userDataManager,
            IApplicationPaths appPaths,
            ILogger<RatingsController> logger,
            SocialWebSocketListener socialWebSocketListener,
            ISystemManager systemManager)
        {
            _repository = repository;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _appPaths = appPaths;
            _logger = logger;
            _socialWebSocketListener = socialWebSocketListener;
            _systemManager = systemManager;
        }

        /// <summary>
        /// Checks if a user is a Jellyfin administrator (server-side check).
        /// </summary>
        private bool IsJellyfinAdmin(Guid userId)
        {
            if (userId == Guid.Empty) return false;
            try
            {
                var user = _userManager.GetUserById(userId);
                return user != null && user.HasPermission(PermissionKind.IsAdministrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the request has admin rights (via session admin OR API key).
        /// API keys have implicit admin rights since only admins can create them.
        /// </summary>
        private bool IsAdminRequest(Guid userId)
        {
            // If authenticated but no userId, it's an API key (implicit admin)
            if (userId == Guid.Empty && User.Identity?.IsAuthenticated == true)
            {
                return true;
            }

            // Otherwise check if user is admin
            return IsJellyfinAdmin(userId);
        }

        // Pre-compiled regex patterns with timeout protection against ReDoS
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
        private static readonly Regex HtmlTagRegex = new(@"<[^>]*?>", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex IncompleteTagRegex = new(@"<[^>]*$", RegexOptions.Compiled, RegexTimeout);
        private static readonly Regex JavaScriptProtocolRegex = new(@"j\s*a\s*v\s*a\s*s\s*c\s*r\s*i\s*p\s*t\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);
        private static readonly Regex EventHandlerRegex = new(@"on\w+\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase, RegexTimeout);

        /// <summary>
        /// Sanitizes user input to prevent XSS attacks.
        /// Strips HTML tags and encodes special characters.
        /// </summary>
        /// <param name="input">The input string to sanitize.</param>
        /// <param name="maxLength">Maximum allowed length (default 500).</param>
        /// <returns>Sanitized string.</returns>
        private static string SanitizeInput(string? input, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            // Limit length FIRST to prevent ReDoS on large inputs
            var sanitized = input.Trim();
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            try
            {
                // Strip all HTML tags (handles malformed tags too)
                sanitized = HtmlTagRegex.Replace(sanitized, string.Empty);
                // Also strip incomplete tags at end
                sanitized = IncompleteTagRegex.Replace(sanitized, string.Empty);

                // Remove javascript: protocol (limit iterations to prevent infinite loop)
                for (int i = 0; i < 5 && sanitized.Contains("javascript", StringComparison.OrdinalIgnoreCase); i++)
                {
                    var previous = sanitized;
                    sanitized = JavaScriptProtocolRegex.Replace(sanitized, string.Empty);
                    if (sanitized == previous) break;
                }

                // Remove event handler attributes
                sanitized = EventHandlerRegex.Replace(sanitized, string.Empty);
            }
            catch (RegexMatchTimeoutException)
            {
                // If regex times out, return empty string for safety
                return string.Empty;
            }

            // Note: NOT HTML encoding here because client renders with escapeHtml() or textContent
            // Adding encoding would cause double-encoding: "L'été" → "L&#39;&#233;t&#233;"

            return sanitized;
        }

        /// <summary>
        /// Validates and sanitizes JSON custom fields.
        /// Ensures valid JSON structure with depth limit to prevent abuse.
        /// </summary>
        /// <param name="json">The JSON string to validate.</param>
        /// <param name="maxLength">Maximum allowed length.</param>
        /// <param name="maxDepth">Maximum nesting depth (default 3).</param>
        /// <returns>Sanitized JSON or empty string if invalid.</returns>
        private static string SanitizeJsonFields(string? json, int maxLength = 5000, int maxDepth = 3)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            // Length check first
            if (json.Length > maxLength)
            {
                return string.Empty;
            }

            try
            {
                // Parse with depth limit using JsonDocument options
                var options = new System.Text.Json.JsonDocumentOptions
                {
                    MaxDepth = maxDepth
                };

                using var doc = System.Text.Json.JsonDocument.Parse(json, options);

                // Re-serialize to ensure clean JSON (removes any malformed content)
                return System.Text.Json.JsonSerializer.Serialize(doc.RootElement);
            }
            catch
            {
                // Invalid JSON - return empty
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets a rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="rating">Rating value (1-10).</param>
        /// <param name="review">Optional review text.</param>
        /// <returns>The created or updated rating.</returns>
        [HttpPost("Items/{itemId}/Rating")]
        [Authorize]
        public async Task<ActionResult<UserRating>> SetRating(
            [FromRoute] [Required] Guid itemId,
            [FromQuery] [Required] [Range(1, 10)] int rating,
            [FromQuery] string? review = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (string.IsNullOrEmpty(authHeader))
                    {
                        _logger.LogError("No authentication header found");
                        return Unauthorized("No authentication header provided");
                    }

                    // Extract token from header
                    var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                    if (!tokenMatch.Success)
                    {
                        return Unauthorized("Invalid authentication header format");
                    }

                    var token = tokenMatch.Groups[1].Value;

                    // Get session by authentication token
                    var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                    if (session == null)
                    {
                        return Unauthorized("Invalid or expired token");
                    }

                    userId = session.UserId;
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Verify the item exists
                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound($"Item {itemId} not found");
                }

                // Check plugin configuration
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableRatings == false)
                {
                    return BadRequest("Ratings are currently disabled");
                }

                // Validate rating range
                if (rating < (config?.MinRating ?? 1) || rating > (config?.MaxRating ?? 10))
                {
                    return BadRequest($"Rating must be between {config?.MinRating ?? 1} and {config?.MaxRating ?? 10}");
                }

                // Extract provider IDs for fallback lookup (handles replaced media files)
                string? tmdbId = null;
                string? imdbId = null;
                string? aniDbId = null;
                if (item.ProviderIds != null)
                {
                    item.ProviderIds.TryGetValue("Tmdb", out tmdbId);
                    item.ProviderIds.TryGetValue("Imdb", out imdbId);
                    item.ProviderIds.TryGetValue("AniDB", out aniDbId);
                }

                // Sanitize review text
                var sanitizedReview = review != null ? SanitizeInput(review, 2000) : null;

                var result = await _repository.SetRatingAsync(userId, itemId, rating, tmdbId, imdbId, aniDbId, sanitizedReview).ConfigureAwait(false);
                _logger.LogDebug("User rated item {ItemId} with {Rating}", itemId, rating);

                // Broadcast profile stats update via WebSocket
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var userRatings = _repository.GetUserRatings(userId);
                        var ratingsCount = userRatings.Count;
                        var averageRating = ratingsCount > 0 ? Math.Round(userRatings.Average(r => r.Rating), 1) : 0;

                        await _socialWebSocketListener.BroadcastProfileStatsUpdateAsync(userId, new
                        {
                            ratingsCount,
                            averageRating
                        }).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to broadcast stats update");
                    }
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting rating for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets rating statistics for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>Rating statistics.</returns>
        [HttpGet("Items/{itemId}/Stats")]
        [Authorize]
        public async Task<ActionResult<RatingStats>> GetRatingStats([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Verify the item exists
                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound($"Item {itemId} not found");
                }

                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        // Extract token from header
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;

                            // Get session by authentication token
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                // Check if this is a collection (BoxSet) - calculate average from child items
                if (item is MediaBrowser.Controller.Entities.Movies.BoxSet boxSet)
                {
                    var childItems = boxSet.GetLinkedChildren();
                    var childRatings = new List<double>();

                    foreach (var child in childItems)
                    {
                        string? childTmdbId = null;
                        string? childImdbId = null;
                        string? childAniDbId = null;
                        if (child.ProviderIds != null)
                        {
                            child.ProviderIds.TryGetValue("Tmdb", out childTmdbId);
                            child.ProviderIds.TryGetValue("Imdb", out childImdbId);
                            child.ProviderIds.TryGetValue("AniDB", out childAniDbId);
                        }

                        var childStats = _repository.GetRatingStats(child.Id, null, childTmdbId, childImdbId, childAniDbId);
                        if (childStats.TotalRatings > 0)
                        {
                            childRatings.Add(childStats.AverageRating);
                        }
                    }

                    var collectionStats = new RatingStats
                    {
                        ItemId = itemId,
                        TotalRatings = childRatings.Count,
                        AverageRating = childRatings.Count > 0 ? Math.Round(childRatings.Average(), 2) : 0
                    };

                    return Ok(collectionStats);
                }

                // Extract provider IDs for fallback lookup (handles replaced media files)
                string? tmdbId = null;
                string? imdbId = null;
                string? aniDbId = null;
                if (item.ProviderIds != null)
                {
                    item.ProviderIds.TryGetValue("Tmdb", out tmdbId);
                    item.ProviderIds.TryGetValue("Imdb", out imdbId);
                    item.ProviderIds.TryGetValue("AniDB", out aniDbId);
                }

                var stats = _repository.GetRatingStats(itemId, userId != Guid.Empty ? userId : null, tmdbId, imdbId, aniDbId);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating stats for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets rating stats for multiple items in a single request.
        /// This is much more efficient than calling GetRatingStats for each item individually.
        /// </summary>
        /// <param name="itemIds">Comma-separated list of item IDs.</param>
        /// <returns>Dictionary of item ID to rating stats.</returns>
        [HttpGet("Items/BatchStats")]
        [Authorize]
        public ActionResult<Dictionary<string, RatingStats>> GetBatchRatingStats([FromQuery] [Required] string itemIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(itemIds))
                {
                    return BadRequest("itemIds is required");
                }

                var ids = itemIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .Where(id => Guid.TryParse(id, out _))
                    .Select(id => Guid.Parse(id))
                    .Distinct()
                    .Take(100) // Limit to 100 items per request
                    .ToList();

                if (ids.Count == 0)
                {
                    return BadRequest("No valid item IDs provided");
                }

                var userId = User.GetUserId();
                var result = new Dictionary<string, RatingStats>();

                // Batch fetch items from library manager
                var items = ids.Select(id => _libraryManager.GetItemById(id))
                    .Where(item => item != null)
                    .ToList();

                foreach (var item in items)
                {
                    if (item == null) continue;

                    string? tmdbId = null;
                    string? imdbId = null;
                    string? aniDbId = null;
                    if (item.ProviderIds != null)
                    {
                        item.ProviderIds.TryGetValue("Tmdb", out tmdbId);
                        item.ProviderIds.TryGetValue("Imdb", out imdbId);
                        item.ProviderIds.TryGetValue("AniDB", out aniDbId);
                    }

                    var stats = _repository.GetRatingStats(item.Id, userId != Guid.Empty ? userId : null, tmdbId, imdbId, aniDbId);
                    result[item.Id.ToString()] = stats;
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting batch rating stats");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the current user's rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>The user's rating or null if not found.</returns>
        [HttpGet("Items/{itemId}/UserRating")]
        [Authorize]
        public ActionResult<UserRating> GetUserRating([FromRoute] [Required] Guid itemId)
        {
            try
            {
                var userId = User.GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var rating = _repository.GetUserRating(userId, itemId);
                if (rating == null)
                {
                    return NotFound("No rating found for this user and item");
                }

                return Ok(rating);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user rating for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all ratings by a specific user.
        /// </summary>
        /// <param name="userId">Optional user ID. If not provided, returns ratings for the authenticated user.</param>
        /// <returns>List of all ratings by the user.</returns>
        [HttpGet("Users/{userId}/Ratings")]
        [Authorize]
        public async Task<ActionResult<List<UserRating>>> GetUserRatings([FromRoute] Guid? userId = null)
        {
            try
            {
                // Try to get user from authentication
                var authUserId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (authUserId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                authUserId = session.UserId;
                            }
                        }
                    }
                }

                if (authUserId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Use provided userId or fall back to authenticated user
                var targetUserId = userId ?? authUserId;

                // IDOR protection: Only allow viewing own ratings or if admin
                if (targetUserId != authUserId && !IsJellyfinAdmin(authUserId))
                {
                    return Forbid("Cannot view another user's ratings");
                }

                var ratings = _repository.GetUserRatings(targetUserId);
                _logger.LogDebug("Retrieved {Count} ratings for user", ratings.Count);

                return Ok(ratings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings for user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all ratings for the currently authenticated user.
        /// </summary>
        /// <returns>List of all ratings by the current user.</returns>
        [HttpGet("MyRatings")]
        [Authorize]
        public async Task<ActionResult<List<UserRating>>> GetMyRatings()
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var ratings = _repository.GetUserRatings(userId);
                _logger.LogInformation("Retrieved {Count} ratings for current user {UserId}", ratings.Count, userId);

                return Ok(ratings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings for current user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets library items sorted by rating with pagination.
        /// Only returns items that have ratings (for local/personal sort).
        /// This is much faster than fetching all library items.
        /// </summary>
        /// <param name="sortBy">Sort field: local, personal, imdb, release, added.</param>
        /// <param name="direction">Sort direction: asc or desc.</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="limit">Items per page (max 200).</param>
        /// <param name="parentId">Optional parent library ID to filter by.</param>
        /// <returns>Paginated list of sorted items.</returns>
        [HttpGet("SortedLibrary")]
        [Authorize]
        public async Task<ActionResult> GetSortedLibrary(
            [FromQuery] string sortBy = "local",
            [FromQuery] string direction = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int limit = 100,
            [FromQuery] string? parentId = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Cap limit to prevent abuse
                limit = Math.Clamp(limit, 1, 200);
                page = Math.Max(1, page);

                // Parse parentId for library filtering
                Guid? parentGuid = null;
                HashSet<Guid>? libraryItemIds = null;
                if (!string.IsNullOrEmpty(parentId) && Guid.TryParse(parentId, out var parsedParentId))
                {
                    parentGuid = parsedParentId;
                    // Get all items from this library to filter ratings
                    var libraryQuery = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        ParentId = parsedParentId,
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo },
                        Recursive = true
                    };
                    var libraryItems = _libraryManager.GetItemList(libraryQuery);
                    libraryItemIds = libraryItems.Select(i => i.Id).ToHashSet();
                }

                List<object> sortedItems;
                int totalCount;

                if (sortBy == "local" || sortBy == "personal")
                {
                    // For rating-based sorts, only fetch items that have ratings
                    Dictionary<Guid, double> itemRatings;

                    if (sortBy == "personal")
                    {
                        // Get user's personal ratings
                        var userRatings = _repository.GetUserRatingsMap(userId);
                        itemRatings = userRatings.ToDictionary(kv => kv.Key, kv => (double)kv.Value);
                    }
                    else
                    {
                        // Get all items with local ratings
                        var allRatings = _repository.GetAllItemRatingStats();
                        itemRatings = allRatings.ToDictionary(kv => kv.Key, kv => kv.Value.AverageRating);
                    }

                    // Filter by library if parentId was provided
                    if (libraryItemIds != null)
                    {
                        itemRatings = itemRatings.Where(kv => libraryItemIds.Contains(kv.Key))
                            .ToDictionary(kv => kv.Key, kv => kv.Value);
                    }

                    if (itemRatings.Count == 0)
                    {
                        return Ok(new { items = new List<object>(), totalCount = 0, page, limit });
                    }

                    // Sort item IDs by rating
                    var sortedIds = direction == "desc"
                        ? itemRatings.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList()
                        : itemRatings.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

                    totalCount = sortedIds.Count;

                    // Paginate
                    var pageIds = sortedIds.Skip((page - 1) * limit).Take(limit).ToArray();

                    // Fetch item details from Jellyfin for this page only
                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        ItemIds = pageIds,
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo }
                    };

                    var items = _libraryManager.GetItemList(query);

                    // Build response maintaining sort order
                    sortedItems = pageIds
                        .Select(id => items.FirstOrDefault(i => i.Id == id))
                        .Where(item => item != null)
                        .Select(item => new
                        {
                            Id = item!.Id.ToString("N"),
                            Name = item.Name,
                            Year = item.ProductionYear,
                            Type = item is MediaBrowser.Controller.Entities.Movies.Movie ? "Movie" : (item is MediaBrowser.Controller.Entities.MusicVideo ? "MusicVideo" : "Series"),
                            ImageUrl = item.ImageInfos?.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary) == true
                                ? $"/Items/{item.Id}/Images/Primary"
                                : null,
                            Rating = itemRatings.TryGetValue(item.Id, out var r) ? r : (double?)null,
                            CommunityRating = item.CommunityRating,
                            PremiereDate = item.PremiereDate,
                            DateCreated = item.DateCreated
                        })
                        .Cast<object>()
                        .ToList();
                }
                else
                {
                    // For non-rating sorts (imdb, release, added), use Jellyfin's native sorting
                    // but only on items that have local ratings (to keep consistent with rating feature)
                    var allRatings = _repository.GetAllItemRatingStats();

                    // Filter by library if parentId was provided
                    if (libraryItemIds != null)
                    {
                        allRatings = allRatings.Where(kv => libraryItemIds.Contains(kv.Key))
                            .ToDictionary(kv => kv.Key, kv => kv.Value);
                    }

                    if (allRatings.Count == 0)
                    {
                        return Ok(new { items = new List<object>(), totalCount = 0, page, limit });
                    }

                    var ratedItemIds = allRatings.Keys.ToArray();

                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        ItemIds = ratedItemIds,
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo }
                    };

                    var items = _libraryManager.GetItemList(query);

                    // Sort by requested field
                    IEnumerable<MediaBrowser.Controller.Entities.BaseItem> sorted = sortBy switch
                    {
                        "imdb" => direction == "desc"
                            ? items.OrderByDescending(i => i.CommunityRating ?? -1)
                            : items.OrderBy(i => i.CommunityRating ?? -1),
                        "release" => direction == "desc"
                            ? items.OrderByDescending(i => i.PremiereDate ?? DateTime.MinValue)
                            : items.OrderBy(i => i.PremiereDate ?? DateTime.MinValue),
                        "added" => direction == "desc"
                            ? items.OrderByDescending(i => i.DateCreated)
                            : items.OrderBy(i => i.DateCreated),
                        _ => items.OrderByDescending(i => allRatings.TryGetValue(i.Id, out var r) ? r.AverageRating : -1)
                    };

                    var sortedList = sorted.ToList();
                    totalCount = sortedList.Count;

                    // Paginate
                    sortedItems = sortedList
                        .Skip((page - 1) * limit)
                        .Take(limit)
                        .Select(item => new
                        {
                            Id = item.Id.ToString("N"),
                            Name = item.Name,
                            Year = item.ProductionYear,
                            Type = item is MediaBrowser.Controller.Entities.Movies.Movie ? "Movie" : (item is MediaBrowser.Controller.Entities.MusicVideo ? "MusicVideo" : "Series"),
                            ImageUrl = item.ImageInfos?.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary) == true
                                ? $"/Items/{item.Id}/Images/Primary"
                                : null,
                            Rating = allRatings.TryGetValue(item.Id, out var r) ? r.AverageRating : (double?)null,
                            CommunityRating = item.CommunityRating,
                            PremiereDate = item.PremiereDate,
                            DateCreated = item.DateCreated
                        })
                        .Cast<object>()
                        .ToList();
                }

                return Ok(new
                {
                    items = sortedItems,
                    totalCount,
                    page,
                    limit,
                    totalPages = (int)Math.Ceiling((double)totalCount / limit)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sorted library");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes the current user's rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Items/{itemId}/Rating")]
        [Authorize]
        public async Task<ActionResult> DeleteRating([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var deleted = await _repository.DeleteRatingAsync(userId, itemId).ConfigureAwait(false);
                if (!deleted)
                {
                    return NotFound("No rating found to delete");
                }

                _logger.LogDebug("User deleted rating for item {ItemId}", itemId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting rating for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all ratings for an item with usernames.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>List of detailed ratings with usernames.</returns>
        [HttpGet("Items/{itemId}/DetailedRatings")]
        [Authorize]
        public async Task<ActionResult<List<UserRatingDetail>>> GetDetailedRatings([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Verify the item exists
                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound($"Item {itemId} not found");
                }

                // Get current user for like status
                var currentUserId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                var ratings = _repository.GetItemRatings(itemId);
                var detailedRatings = ratings.Select(r =>
                {
                    var user = _userManager.GetUserById(r.UserId);
                    var likeCounts = _repository.GetReviewLikeCounts(r.UserId, itemId);
                    var userLike = currentUserId != Guid.Empty
                        ? _repository.GetUserReviewLike(r.UserId, itemId, currentUserId)
                        : null;

                    return new UserRatingDetail
                    {
                        UserId = r.UserId,
                        Username = user?.Username ?? "Unknown User",
                        Rating = r.Rating,
                        CreatedAt = r.CreatedAt,
                        ReviewText = r.ReviewText,
                        HasReview = !string.IsNullOrWhiteSpace(r.ReviewText),
                        LikeCount = likeCounts.LikeCount,
                        DislikeCount = likeCounts.DislikeCount,
                        UserLiked = userLike
                    };
                }).OrderByDescending(r => r.Rating).ThenBy(r => r.Username).ToList();

                return Ok(detailedRatings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed ratings for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Likes or dislikes a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="isLike">True for like, false for dislike.</param>
        /// <returns>Success status.</returns>
        [HttpPost("Reviews/{reviewerUserId}/{itemId}/Like")]
        [Authorize]
        public async Task<ActionResult> LikeReview(
            [FromRoute] [Required] Guid reviewerUserId,
            [FromRoute] [Required] Guid itemId,
            [FromQuery] [Required] bool isLike)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Can't like your own review
                if (userId == reviewerUserId)
                {
                    return BadRequest("Cannot like your own review");
                }

                // Verify review exists
                var rating = _repository.GetUserRating(reviewerUserId, itemId);
                if (rating == null || string.IsNullOrWhiteSpace(rating.ReviewText))
                {
                    return NotFound("Review not found");
                }

                await _repository.SetReviewLikeAsync(reviewerUserId, itemId, userId, isLike).ConfigureAwait(false);

                // Return updated counts
                var counts = _repository.GetReviewLikeCounts(reviewerUserId, itemId);
                var userLike = _repository.GetUserReviewLike(reviewerUserId, itemId, userId);

                return Ok(new
                {
                    LikeCount = counts.LikeCount,
                    DislikeCount = counts.DislikeCount,
                    UserLiked = userLike
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error liking review for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates only the review text for an existing rating.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="review">Review text (empty to clear).</param>
        /// <returns>The updated rating.</returns>
        [HttpPut("Items/{itemId}/Review")]
        [Authorize]
        public async Task<ActionResult<UserRating>> UpdateReview(
            [FromRoute] [Required] Guid itemId,
            [FromQuery] string? review = null)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Sanitize review text
                var sanitizedReview = review != null ? SanitizeInput(review, 2000) : null;

                var result = await _repository.UpdateReviewTextAsync(userId, itemId, sanitizedReview).ConfigureAwait(false);
                if (result == null)
                {
                    return NotFound("Rating not found. Please rate the item first.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Test endpoint to verify plugin is loaded.
        /// </summary>
        /// <returns>Test message.</returns>
        [HttpGet("test")]
        [AllowAnonymous]
        public ActionResult GetTest()
        {
            return Ok(new { message = "Ratings plugin is loaded!" });
        }

        /// <summary>
        /// Gets plugin configuration for client-side use.
        /// </summary>
        /// <returns>Plugin configuration settings.</returns>
        [HttpGet("Config")]
        [AllowAnonymous]
        public async Task<ActionResult> GetConfig()
        {
            try
            {
                var config = Plugin.Instance?.Configuration;

                // Resolve user ID to hide sensitive data from anonymous users
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                return Ok(new
                {
                    // Core settings
                    EnableRatings = config?.EnableRatings ?? true,
                    EnableNetflixView = config?.EnableNetflixView ?? false,
                    EnableRequestButton = config?.EnableRequestButton ?? true,
                    EnableNewMediaNotifications = config?.EnableNewMediaNotifications ?? true,
                    MinRating = config?.MinRating ?? 1,
                    MaxRating = config?.MaxRating ?? 10,

                    // UI toggles
                    DefaultLanguage = config?.DefaultLanguage ?? "en",
                    ShowLanguageSwitch = config?.ShowLanguageSwitch ?? true,
                    ShowHeaderLanguageButton = config?.ShowHeaderLanguageButton ?? true,
                    ShowSearchButton = config?.ShowSearchButton ?? true,
                    SearchExcludeEpisodes = config?.SearchExcludeEpisodes ?? true,
                    ShowNotificationToggle = config?.ShowNotificationToggle ?? true,
                    NotificationsEnabledByDefault = config?.NotificationsEnabledByDefault ?? true,
                    ShowLatestMediaButton = config?.ShowLatestMediaButton ?? true,
                    HideHomeDuplicates = config?.HideHomeDuplicates ?? true,

                    // Media management settings
                    EnableMediaManagement = config?.EnableMediaManagement ?? true,
                    DefaultDeletionDelayDays = config?.DefaultDeletionDelayDays ?? 7,

                    // Request system settings
                    EnableAdminRequests = config?.EnableAdminRequests ?? false,
                    AutoDeleteRejectedDays = config?.AutoDeleteRejectedDays ?? 0,
                    MaxRequestsPerMonth = config?.MaxRequestsPerMonth ?? 0,

                    // Custom fields
                    CustomRequestFields = config?.CustomRequestFields ?? string.Empty,

                    // Request window customization
                    RequestWindowTitle = config?.RequestWindowTitle ?? string.Empty,
                    RequestWindowDescription = config?.RequestWindowDescription ?? string.Empty,
                    RequestSubmitButtonText = config?.RequestSubmitButtonText ?? string.Empty,

                    // Title field
                    RequestTitleLabel = config?.RequestTitleLabel ?? string.Empty,
                    RequestTitlePlaceholder = config?.RequestTitlePlaceholder ?? string.Empty,

                    // Type field
                    RequestTypeEnabled = config?.RequestTypeEnabled ?? true,
                    RequestTypeRequired = config?.RequestTypeRequired ?? false,
                    RequestTypeLabel = config?.RequestTypeLabel ?? string.Empty,

                    // Notes field
                    RequestNotesEnabled = config?.RequestNotesEnabled ?? true,
                    RequestNotesRequired = config?.RequestNotesRequired ?? false,
                    RequestNotesLabel = config?.RequestNotesLabel ?? string.Empty,
                    RequestNotesPlaceholder = config?.RequestNotesPlaceholder ?? string.Empty,

                    // IMDB Code field
                    RequestImdbCodeEnabled = config?.RequestImdbCodeEnabled ?? true,
                    RequestImdbCodeRequired = config?.RequestImdbCodeRequired ?? false,
                    RequestImdbCodeLabel = config?.RequestImdbCodeLabel ?? string.Empty,
                    RequestImdbCodePlaceholder = config?.RequestImdbCodePlaceholder ?? string.Empty,

                    // IMDB Link field
                    RequestImdbLinkEnabled = config?.RequestImdbLinkEnabled ?? true,
                    RequestImdbLinkRequired = config?.RequestImdbLinkRequired ?? false,
                    RequestImdbLinkLabel = config?.RequestImdbLinkLabel ?? string.Empty,
                    RequestImdbLinkPlaceholder = config?.RequestImdbLinkPlaceholder ?? string.Empty,

                    // Badge display profiles
                    BadgeDisplayProfiles = config?.BadgeDisplayProfiles ?? string.Empty,

                    // Sorting options
                    EnableImdbSorting = config?.EnableImdbSorting ?? true,

                    // Star display options
                    StarDisplayMode = config?.StarDisplayMode ?? "10-stars",
                    QuickRatingMode = config?.QuickRatingMode ?? false,

                    // Star widget text options
                    ShowRatingStats = config?.ShowRatingStats ?? true,
                    RatingStatsFormat = config?.RatingStatsFormat ?? "{avg}/10 - {count} rating{s}",
                    ShowYourRating = config?.ShowYourRating ?? true,
                    YourRatingFormat = config?.YourRatingFormat ?? "Your rating: {rating}/10 (click stars to edit)",

                    // Star widget styling
                    StarWidgetBackground = config?.StarWidgetBackground ?? "rgba(0, 0, 0, 0.6)",
                    StarWidgetBorderEnabled = config?.StarWidgetBorderEnabled ?? false,
                    StarWidgetBorderColor = config?.StarWidgetBorderColor ?? "rgba(255, 255, 255, 0.3)",
                    StarWidgetBorderRadius = config?.StarWidgetBorderRadius ?? 6,
                    StarWidgetGlowEffect = config?.StarWidgetGlowEffect ?? false,
                    StarWidgetGlowColor = config?.StarWidgetGlowColor ?? "rgba(255, 215, 0, 0.5)",
                    StarFilledColor = config?.StarFilledColor ?? "#ffd700",
                    StarEmptyColor = config?.StarEmptyColor ?? "#555555",
                    StarHoverColor = config?.StarHoverColor ?? "#ffd700",
                    StarWidgetCustomCSS = config?.StarWidgetCustomCSS ?? string.Empty,

                    // Social features
                    EnableFriendsButton = config?.EnableFriendsButton ?? false,

                    // Chat settings
                    EnableChat = config?.EnableChat ?? false,
                    // HasGifSupport indicates API key is configured (key is never exposed to client)
                    HasGifSupport = !string.IsNullOrEmpty(config?.KlipyApiKey) || !string.IsNullOrEmpty(config?.TenorApiKey),
                    ChatAllowGifs = config?.ChatAllowGifs ?? true,
                    ChatAllowEmojis = config?.ChatAllowEmojis ?? true,
                    ChatMaxMessageLength = config?.ChatMaxMessageLength ?? 500,
                    ChatRateLimitPerMinute = config?.ChatRateLimitPerMinute ?? 10,
                    ChatNotifyPublic = config?.ChatNotifyPublic ?? true,
                    ChatNotifyPrivate = config?.ChatNotifyPrivate ?? true,

                    // Header button group styling
                    HeaderButtonTransparentBg = config?.HeaderButtonTransparentBg ?? false,
                    HeaderButtonGroupBackground = config?.HeaderButtonGroupBackground ?? "rgba(40, 40, 40, 0.95)",
                    HeaderButtonNoBorder = config?.HeaderButtonNoBorder ?? false,
                    HeaderButtonGroupBorderColor = config?.HeaderButtonGroupBorderColor ?? "rgba(255, 255, 255, 0.15)",
                    HeaderButtonGroupBorderRadius = config?.HeaderButtonGroupBorderRadius ?? 25,
                    HeaderButtonColor = config?.HeaderButtonColor ?? "#ffffff",
                    HeaderButtonIconOpacity = config?.HeaderButtonIconOpacity ?? 100,
                    HeaderButtonHoverBackground = config?.HeaderButtonHoverBackground ?? "rgba(255, 255, 255, 0.15)",
                    HeaderButtonGlowEffect = config?.HeaderButtonGlowEffect ?? false,
                    HeaderButtonGlowColor = config?.HeaderButtonGlowColor ?? "rgba(255, 255, 255, 0.3)",
                    HeaderGroupOverallOpacity = config?.HeaderGroupOverallOpacity ?? 100,
                    SearchFieldMatchGroupBg = config?.SearchFieldMatchGroupBg ?? true,
                    SearchFieldBackground = config?.SearchFieldBackground ?? "rgba(40, 40, 40, 0.95)",
                    LanguageTextColor = config?.LanguageTextColor ?? "#ffffff"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting plugin configuration");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets notifications since a specific time.
        /// </summary>
        /// <param name="since">ISO 8601 timestamp to get notifications since.</param>
        /// <returns>List of notifications.</returns>
        [HttpGet("Notifications")]
        [Authorize]
        public async Task<ActionResult<List<Models.NewMediaNotification>>> GetNotifications([FromQuery] string? since = null)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                DateTime sinceTime;
                if (string.IsNullOrEmpty(since))
                {
                    // Return notifications from the last 5 minutes by default
                    sinceTime = DateTime.UtcNow.AddMinutes(-5);
                }
                else if (DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out sinceTime))
                {
                    // Ensure it's UTC
                    if (sinceTime.Kind != DateTimeKind.Utc)
                    {
                        sinceTime = sinceTime.ToUniversalTime();
                    }
                }
                else
                {
                    return BadRequest("Invalid 'since' parameter format. Use ISO 8601 format.");
                }

                var notifications = _repository.GetNotificationsSince(sinceTime);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Sends a test notification (admin only).
        /// </summary>
        /// <param name="message">Optional custom message for the notification.</param>
        /// <returns>The created test notification.</returns>
        [HttpPost("Notifications/Test")]
        [Authorize]
        public async Task<ActionResult<Models.NewMediaNotification>> SendTestNotification([FromQuery] string? message = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can send test notifications");
                }

                // Try to get a random movie, series, or episode from the library
                Models.NewMediaNotification notification;
                try
                {
                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.Episode },
                        Recursive = true,
                        Limit = 100
                    };

                    var items = _libraryManager.GetItemList(query);
                    if (items != null && items.Count > 0)
                    {
                        // Pick a random item
                        var random = new Random();
                        var randomItem = items[random.Next(items.Count)];

                        string? imageUrl = null;
                        if (randomItem.ImageInfos != null && randomItem.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary))
                        {
                            imageUrl = $"/Items/{randomItem.Id}/Images/Primary";
                        }

                        if (randomItem is MediaBrowser.Controller.Entities.Movies.Movie)
                        {
                            notification = new Models.NewMediaNotification
                            {
                                Id = Guid.NewGuid(),
                                ItemId = randomItem.Id,
                                Title = randomItem.Name,
                                MediaType = "Movie",
                                Year = randomItem.ProductionYear,
                                ImageUrl = imageUrl,
                                CreatedAt = DateTime.UtcNow,
                                IsTest = false,
                                Message = null
                            };
                            _logger.LogInformation("Admin {UserId} sent test notification with random Movie: {Title} ({Year})", userId, randomItem.Name, randomItem.ProductionYear);
                        }
                        else if (randomItem is MediaBrowser.Controller.Entities.TV.Episode episode)
                        {
                            // For episodes, prefer series image if episode doesn't have one
                            if (string.IsNullOrEmpty(imageUrl) && episode.Series != null &&
                                episode.Series.ImageInfos != null && episode.Series.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary))
                            {
                                imageUrl = $"/Items/{episode.Series.Id}/Images/Primary";
                            }

                            notification = new Models.NewMediaNotification
                            {
                                Id = Guid.NewGuid(),
                                ItemId = episode.Id,
                                Title = episode.Name,
                                MediaType = "Episode",
                                Year = episode.ProductionYear ?? episode.PremiereDate?.Year,
                                SeriesName = episode.SeriesName,
                                SeasonNumber = episode.ParentIndexNumber,
                                EpisodeNumber = episode.IndexNumber,
                                ImageUrl = imageUrl,
                                CreatedAt = DateTime.UtcNow,
                                IsTest = false,
                                Message = null
                            };
                            _logger.LogInformation("Admin {UserId} sent test notification with random Episode: {SeriesName} S{Season}E{Episode} - {Title}",
                                userId, episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber, episode.Name);
                        }
                        else
                        {
                            // Series
                            notification = new Models.NewMediaNotification
                            {
                                Id = Guid.NewGuid(),
                                ItemId = randomItem.Id,
                                Title = randomItem.Name,
                                MediaType = "Series",
                                Year = randomItem.ProductionYear,
                                ImageUrl = imageUrl,
                                CreatedAt = DateTime.UtcNow,
                                IsTest = false,
                                Message = null
                            };
                            _logger.LogInformation("Admin {UserId} sent test notification with random Series: {Title} ({Year})", userId, randomItem.Name, randomItem.ProductionYear);
                        }
                    }
                    else
                    {
                        // Fallback if no media found
                        notification = new Models.NewMediaNotification
                        {
                            Id = Guid.NewGuid(),
                            ItemId = Guid.Empty,
                            Title = "Test Notification",
                            MediaType = "Test",
                            Year = DateTime.UtcNow.Year,
                            ImageUrl = null,
                            CreatedAt = DateTime.UtcNow,
                            IsTest = true,
                            Message = string.IsNullOrEmpty(message) ? "This is a test notification from the Ratings plugin!" : message
                        };
                        _logger.LogInformation("Admin {UserId} sent test notification (no media in library)", userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get random media for test notification, using fallback");
                    notification = new Models.NewMediaNotification
                    {
                        Id = Guid.NewGuid(),
                        ItemId = Guid.Empty,
                        Title = "Test Notification",
                        MediaType = "Test",
                        Year = DateTime.UtcNow.Year,
                        ImageUrl = null,
                        CreatedAt = DateTime.UtcNow,
                        IsTest = true,
                        Message = string.IsNullOrEmpty(message) ? "This is a test notification from the Ratings plugin!" : message
                    };
                }

                _repository.AddNotification(notification);

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Serves the ratings.js file.
        /// </summary>
        /// <returns>The JavaScript file content.</returns>
        [HttpGet("ratings.js")]
        [AllowAnonymous]
        public ActionResult GetRatingsScript()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "Jellyfin.Plugin.Ratings.Web.ratings.js";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    var allResources = assembly.GetManifestResourceNames();
                    _logger.LogError("Resource {ResourceName} not found in assembly. Available: {Resources}", resourceName, string.Join(", ", allResources));
                    return Content("// ERROR: Resource not available", "application/javascript");
                }

                using var reader = new System.IO.StreamReader(stream);
                var content = reader.ReadToEnd();

                return Content(content, "application/javascript");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve ratings.js");
                return Content("// ERROR: Failed to load ratings.js", "application/javascript");
            }
        }

        /// <summary>
        /// Creates a new media request.
        /// </summary>
        /// <param name="request">The media request data.</param>
        /// <returns>The created request.</returns>
        [HttpPost("Requests")]
        [Authorize]
        public async Task<ActionResult<MediaRequest>> CreateMediaRequest([FromBody] [Required] MediaRequestDto request)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (string.IsNullOrEmpty(authHeader))
                    {
                        _logger.LogError("No authentication header found");
                        return Unauthorized("No authentication header provided");
                    }

                    // Extract token from header
                    var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                    if (!tokenMatch.Success)
                    {
                        _logger.LogError("Could not extract token from header");
                        return Unauthorized("Invalid authentication header format");
                    }

                    var token = tokenMatch.Groups[1].Value;

                    // Get session by authentication token
                    var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                    if (session == null)
                    {
                        _logger.LogError("No active session found for token");
                        return Unauthorized("Invalid or expired token");
                    }

                    userId = session.UserId;
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Check if user is banned from media requests
                var mediaBan = _repository.GetActiveBan(userId, "media_request");
                if (mediaBan != null)
                {
                    var banMsg = mediaBan.ExpiresAt.HasValue
                        ? $"You are banned from submitting media requests until {mediaBan.ExpiresAt.Value:yyyy-MM-dd HH:mm} UTC"
                        : "You are permanently banned from submitting media requests";
                    return BadRequest(banMsg);
                }

                // Check request limit
                var config = Plugin.Instance?.Configuration;
                var maxRequests = config?.MaxRequestsPerMonth ?? 0;
                if (maxRequests > 0)
                {
                    var currentCount = _repository.GetUserRequestCountThisMonth(userId);
                    if (currentCount >= maxRequests)
                    {
                        return BadRequest($"You have reached your monthly request limit of {maxRequests} requests.");
                    }
                }

                // Run auto-cleanup of old rejected requests
                var autoDeleteDays = config?.AutoDeleteRejectedDays ?? 0;
                if (autoDeleteDays > 0)
                {
                    _ = _repository.CleanupOldRejectedRequestsAsync(autoDeleteDays);
                }

                // Validate ImdbLink URL if provided
                if (!string.IsNullOrWhiteSpace(request.ImdbLink))
                {
                    if (!Uri.TryCreate(request.ImdbLink, UriKind.Absolute, out var imdbUri) ||
                        (imdbUri.Scheme != "https" && imdbUri.Scheme != "http"))
                    {
                        return BadRequest("Invalid IMDB link format");
                    }

                    // Only allow IMDB URLs
                    var host = imdbUri.Host.ToLowerInvariant();
                    if (!host.Equals("imdb.com", StringComparison.OrdinalIgnoreCase) &&
                        !host.EndsWith(".imdb.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest("IMDB link must be from imdb.com");
                    }
                }

                var mediaRequest = new MediaRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Username = user.Username,
                    Title = SanitizeInput(request.Title, 500),
                    Type = SanitizeInput(request.Type, 100),
                    Notes = SanitizeInput(request.Notes, 2000),
                    CustomFields = SanitizeJsonFields(request.CustomFields, 5000),
                    ImdbCode = SanitizeInput(request.ImdbCode, 50),
                    ImdbLink = request.ImdbLink, // Already validated as URL above
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _repository.AddMediaRequestAsync(mediaRequest).ConfigureAwait(false);
                _logger.LogInformation("User {UserId} created media request for '{Title}'", userId, request.Title);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating media request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all media requests (admin only).
        /// </summary>
        /// <returns>List of all media requests.</returns>
        [HttpGet("Requests")]
        [Authorize]
        public async Task<ActionResult<List<MediaRequest>>> GetMediaRequests()
        {
            try
            {
                var userId = User.GetUserId();

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can view all requests");
                }
                // API key auth passes through - has implicit admin rights

                var requests = await _repository.GetAllMediaRequestsAsync().ConfigureAwait(false);
                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media requests");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates the status of a media request (admin only).
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="status">The new status (pending, processing, done, rejected).</param>
        /// <param name="mediaLink">Optional media link when marking as done.</param>
        /// <param name="rejectionReason">Optional rejection reason when rejecting.</param>
        /// <returns>The updated request.</returns>
        [HttpPost("Requests/{requestId}/Status")]
        [Authorize]
        public async Task<ActionResult<MediaRequest>> UpdateRequestStatus(
            [FromRoute] [Required] Guid requestId,
            [FromQuery] [Required] string status,
            [FromQuery] string? mediaLink = null,
            [FromQuery] string? rejectionReason = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can update request status");
                }

                // Validate status
                var validStatuses = new[] { "pending", "processing", "done", "rejected", "snoozed" };
                if (!validStatuses.Contains(status.ToLower()))
                {
                    return BadRequest($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }

                var result = await _repository.UpdateMediaRequestStatusAsync(requestId, status.ToLower(), mediaLink, rejectionReason).ConfigureAwait(false);
                if (result == null)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("Admin {UserId} updated request {RequestId} status to '{Status}'", userId, requestId, status);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request status");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a media request. Admins can delete any request, users can only delete their own.
        /// </summary>
        /// <param name="requestId">The request ID to delete.</param>
        /// <returns>Success or failure.</returns>
        [HttpDelete("Requests/{requestId}")]
        [Authorize]
        public async Task<ActionResult> DeleteRequest([FromRoute] [Required] Guid requestId)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Check if user exists
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Get the request to check ownership (for non-admin users)
                var existingRequest = await _repository.GetMediaRequestAsync(requestId).ConfigureAwait(false);
                if (existingRequest == null)
                {
                    return NotFound("Request not found");
                }

                var isOwner = existingRequest.UserId == userId;
                if (!isOwner && !IsJellyfinAdmin(userId))
                {
                    return Forbid("You can only delete your own requests");
                }

                var result = await _repository.DeleteMediaRequestAsync(requestId).ConfigureAwait(false);
                if (!result)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("User {UserId} deleted request {RequestId} (owner: {IsOwner})", userId, requestId, isOwner);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the current user's requests.
        /// </summary>
        /// <returns>List of requests made by the current user.</returns>
        [HttpGet("Requests/My")]
        [Authorize]
        public async Task<ActionResult<List<MediaRequest>>> GetMyRequests()
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var requests = _repository.GetUserRequests(userId);

                // Also return request count info
                var config = Plugin.Instance?.Configuration;
                var maxRequests = config?.MaxRequestsPerMonth ?? 0;
                var currentCount = _repository.GetUserRequestCountThisMonth(userId);

                Response.Headers["X-Request-Count"] = currentCount.ToString();
                Response.Headers["X-Request-Limit"] = maxRequests.ToString();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user's requests");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates a media request. Users can only edit their own pending requests.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="request">The updated request data.</param>
        /// <returns>The updated request.</returns>
        [HttpPut("Requests/{requestId}")]
        [Authorize]
        public async Task<ActionResult<MediaRequest>> UpdateMediaRequest(
            [FromRoute] [Required] Guid requestId,
            [FromBody] [Required] MediaRequestDto request)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Get the existing request
                var existingRequest = await _repository.GetMediaRequestAsync(requestId).ConfigureAwait(false);
                if (existingRequest == null)
                {
                    return NotFound("Request not found");
                }

                // Users can only edit their own requests
                if (existingRequest.UserId != userId)
                {
                    return Forbid("You can only edit your own requests");
                }

                // Users can only edit pending requests
                if (existingRequest.Status != "pending")
                {
                    return BadRequest("You can only edit pending requests");
                }

                // Validate ImdbLink URL if provided
                if (!string.IsNullOrWhiteSpace(request.ImdbLink))
                {
                    if (!Uri.TryCreate(request.ImdbLink, UriKind.Absolute, out var imdbUri) ||
                        (imdbUri.Scheme != "https" && imdbUri.Scheme != "http"))
                    {
                        return BadRequest("Invalid IMDB link format");
                    }

                    // Only allow IMDB URLs
                    var host = imdbUri.Host.ToLowerInvariant();
                    if (!host.Equals("imdb.com", StringComparison.OrdinalIgnoreCase) &&
                        !host.EndsWith(".imdb.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest("IMDB link must be from imdb.com");
                    }
                }

                var result = await _repository.UpdateMediaRequestAsync(
                    requestId,
                    SanitizeInput(request.Title, 500),
                    SanitizeInput(request.Type, 100),
                    SanitizeInput(request.Notes, 2000),
                    SanitizeJsonFields(request.CustomFields, 5000),
                    SanitizeInput(request.ImdbCode, 50),
                    request.ImdbLink).ConfigureAwait(false);

                if (result == null)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("User {UserId} updated request {RequestId}", userId, requestId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Snoozes a media request until a specified date (admin only).
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="snoozedUntil">The date until which to snooze (ISO 8601 format).</param>
        /// <returns>The updated request.</returns>
        [HttpPost("Requests/{requestId}/Snooze")]
        [Authorize]
        public async Task<ActionResult<MediaRequest>> SnoozeRequest(
            [FromRoute] [Required] Guid requestId,
            [FromQuery] [Required] string snoozedUntil)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can snooze requests");
                }

                // Parse the snooze date
                if (!DateTime.TryParse(snoozedUntil, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var snoozeDate))
                {
                    return BadRequest("Invalid date format. Use ISO 8601 format (e.g., 2024-12-31).");
                }

                if (snoozeDate <= DateTime.UtcNow)
                {
                    return BadRequest("Snooze date must be in the future.");
                }

                var result = await _repository.SnoozeMediaRequestAsync(requestId, snoozeDate).ConfigureAwait(false);
                if (result == null)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("Admin {UserId} snoozed request {RequestId} until {SnoozeDate}", userId, requestId, snoozeDate);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error snoozing request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Unsnoozes a media request (admin only).
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The updated request.</returns>
        [HttpPost("Requests/{requestId}/Unsnooze")]
        [Authorize]
        public async Task<ActionResult<MediaRequest>> UnsnoozeRequest([FromRoute] [Required] Guid requestId)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can unsnooze requests");
                }

                var result = await _repository.UnsnoozeMediaRequestAsync(requestId).ConfigureAwait(false);
                if (result == null)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("Admin {UserId} unsnoozed request {RequestId}", userId, requestId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsnoozing request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the current user's request count for this month.
        /// </summary>
        /// <returns>Request count info.</returns>
        [HttpGet("Requests/Count")]
        [Authorize]
        public async Task<ActionResult> GetRequestCount()
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var config = Plugin.Instance?.Configuration;
                var maxRequests = config?.MaxRequestsPerMonth ?? 0;
                var currentCount = _repository.GetUserRequestCountThisMonth(userId);

                return Ok(new
                {
                    CurrentCount = currentCount,
                    MaxRequests = maxRequests,
                    Remaining = maxRequests > 0 ? Math.Max(0, maxRequests - currentCount) : -1,
                    Unlimited = maxRequests == 0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request count");
                return StatusCode(500, "Internal server error");
            }
        }

        // Media Management Endpoints

        /// <summary>
        /// Gets all media items with statistics (admin only).
        /// </summary>
        /// <param name="search">Optional search term for title.</param>
        /// <param name="type">Optional filter by type (Movie, Series).</param>
        /// <param name="sortBy">Sort field (title, year, playCount, watchTime, size, rating, dateAdded).</param>
        /// <param name="sortOrder">Sort order (asc, desc).</param>
        /// <param name="page">Page number (1-based).</param>
        /// <param name="pageSize">Items per page (default 50).</param>
        /// <returns>Paginated list of media items with stats.</returns>
        [HttpGet("Media")]
        [Authorize]
        public async Task<ActionResult<object>> GetMediaItems(
            [FromQuery] string? search = null,
            [FromQuery] string? type = null,
            [FromQuery] string? parentId = null,
            [FromQuery] string sortBy = "dateAdded",
            [FromQuery] string sortOrder = "desc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can access media management");
                }

                // Get user for play count data (null for API key auth)
                var user = userId != Guid.Empty ? _userManager.GetUserById(userId) : null;

                // Cap pageSize to prevent abuse
                pageSize = Math.Clamp(pageSize, 1, 200);

                // Check if media management is enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableMediaManagement != true)
                {
                    return BadRequest("Media management is disabled");
                }

                // Build query for media items
                var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo },
                    Recursive = true
                };

                // Apply type filter
                if (!string.IsNullOrEmpty(type))
                {
                    if (type.Equals("Movie", StringComparison.OrdinalIgnoreCase))
                    {
                        query.IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie };
                    }
                    else if (type.Equals("Series", StringComparison.OrdinalIgnoreCase))
                    {
                        query.IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Series };
                    }
                }

                // Apply parent library filter (for library-specific tabs like Anime)
                if (!string.IsNullOrEmpty(parentId) && Guid.TryParse(parentId, out var parentGuid))
                {
                    query.ParentId = parentGuid;
                    // When filtering by library, include all types from that library
                    query.IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo };
                }

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query.SearchTerm = search;
                }

                var allItems = _libraryManager.GetItemList(query);

                // Get scheduled deletions for badge info
                var scheduledDeletions = _repository.GetAllScheduledDeletions()
                    .ToDictionary(d => d.ItemId);

                // STEP 1: Build basic stats quickly (no expensive episode queries)
                var mediaStats = allItems.Select(item =>
                {
                    var ratingStats = _repository.GetRatingStats(item.Id);

                    // Build image URL
                    string? imageUrl = null;
                    if (item.ImageInfos != null && item.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary))
                    {
                        imageUrl = $"/Items/{item.Id}/Images/Primary";
                    }

                    // Get scheduled deletion if any
                    scheduledDeletions.TryGetValue(item.Id, out var deletion);

                    return new MediaItemStats
                    {
                        ItemId = item.Id,
                        Title = item.Name,
                        Year = item.ProductionYear,
                        Type = item is MediaBrowser.Controller.Entities.Movies.Movie ? "Movie" : "Series",
                        ImageUrl = imageUrl ?? string.Empty,
                        PlayCount = 0, // Will be calculated for current page only
                        TotalWatchTimeMinutes = (long)(item.RunTimeTicks.HasValue ? TimeSpan.FromTicks(item.RunTimeTicks.Value).TotalMinutes : 0),
                        FileSizeBytes = 0, // Will be calculated for current page only
                        AverageRating = ratingStats.TotalRatings > 0 ? ratingStats.AverageRating : null,
                        RatingCount = ratingStats.TotalRatings,
                        DateAdded = item.DateCreated,
                        ScheduledDeletion = deletion
                    };
                }).ToList();

                // When sorting by playcount or size, calculate those stats for ALL items first
                var sortField = sortBy.ToLower();
                if (sortField == "playcount" || sortField == "size")
                {
                    foreach (var stat in mediaStats)
                    {
                        var item = allItems.FirstOrDefault(i => i.Id == stat.ItemId);
                        if (item == null) continue;

                        if (sortField == "size" && item is MediaBrowser.Controller.Entities.Movies.Movie sizeMovie)
                        {
                            try
                            {
                                var mediaStreams = sizeMovie.GetMediaSources(false);
                                if (mediaStreams != null && mediaStreams.Count > 0)
                                {
                                    stat.FileSizeBytes = mediaStreams[0].Size ?? 0;
                                }
                            }
                            catch { }
                        }

                        if (sortField == "playcount" && user != null)
                        {
                            if (item is MediaBrowser.Controller.Entities.Movies.Movie playMovie)
                            {
                                try
                                {
                                    var userData = _userDataManager.GetUserData(user, item);
                                    if (userData != null)
                                    {
                                        stat.PlayCount = userData.PlayCount;
                                    }
                                }
                                catch { }
                            }
                            else if (item is MediaBrowser.Controller.Entities.TV.Series playSeries)
                            {
                                try
                                {
                                    var episodeQuery = new MediaBrowser.Controller.Entities.InternalItemsQuery
                                    {
                                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Episode },
                                        AncestorIds = new[] { playSeries.Id },
                                        Recursive = true
                                    };
                                    var episodes = _libraryManager.GetItemList(episodeQuery);
                                    foreach (var episode in episodes)
                                    {
                                        var epUserData = _userDataManager.GetUserData(user, episode);
                                        if (epUserData != null)
                                        {
                                            stat.PlayCount += epUserData.PlayCount;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                // Apply sorting
                mediaStats = sortField switch
                {
                    "title" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.Title).ToList()
                        : mediaStats.OrderByDescending(m => m.Title).ToList(),
                    "year" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.Year ?? 0).ToList()
                        : mediaStats.OrderByDescending(m => m.Year ?? 0).ToList(),
                    "playcount" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.PlayCount).ToList()
                        : mediaStats.OrderByDescending(m => m.PlayCount).ToList(),
                    "watchtime" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.TotalWatchTimeMinutes).ToList()
                        : mediaStats.OrderByDescending(m => m.TotalWatchTimeMinutes).ToList(),
                    "size" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.FileSizeBytes).ToList()
                        : mediaStats.OrderByDescending(m => m.FileSizeBytes).ToList(),
                    "rating" => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.AverageRating ?? 0).ToList()
                        : mediaStats.OrderByDescending(m => m.AverageRating ?? 0).ToList(),
                    _ => sortOrder.ToLower() == "asc"
                        ? mediaStats.OrderBy(m => m.DateAdded).ToList()
                        : mediaStats.OrderByDescending(m => m.DateAdded).ToList()
                };

                // Apply pagination
                var totalItems = mediaStats.Count;
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                var pagedItems = mediaStats.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                // STEP 2: Calculate expensive stats only for paginated items
                foreach (var stat in pagedItems)
                {
                    var item = allItems.FirstOrDefault(i => i.Id == stat.ItemId);
                    if (item == null) continue;

                    if (item is MediaBrowser.Controller.Entities.Movies.Movie movie)
                    {
                        // Movie: get file size and play count
                        try
                        {
                            var mediaStreams = movie.GetMediaSources(false);
                            if (mediaStreams != null && mediaStreams.Count > 0)
                            {
                                stat.FileSizeBytes = mediaStreams[0].Size ?? 0;
                            }
                        }
                        catch { }

                        if (user != null)
                        {
                            try
                            {
                                var userData = _userDataManager.GetUserData(user, item);
                                if (userData != null)
                                {
                                    stat.PlayCount = userData.PlayCount;
                                }
                            }
                            catch { }
                        }
                    }
                    else if (item is MediaBrowser.Controller.Entities.TV.Series series)
                    {
                        // Series: sum play counts from all episodes
                        if (user != null)
                        {
                            try
                            {
                                var episodeQuery = new MediaBrowser.Controller.Entities.InternalItemsQuery
                                {
                                    IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Episode },
                                    AncestorIds = new[] { series.Id },
                                    Recursive = true
                                };
                                var episodes = _libraryManager.GetItemList(episodeQuery);

                                foreach (var episode in episodes)
                                {
                                    var epUserData = _userDataManager.GetUserData(user, episode);
                                    if (epUserData != null)
                                    {
                                        stat.PlayCount += epUserData.PlayCount;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }

                return Ok(new
                {
                    Items = pagedItems,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting media items");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Schedules a media item for deletion (admin only).
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="delayDays">Number of days until deletion.</param>
        /// <returns>The scheduled deletion.</returns>
        [HttpPost("Media/{itemId}/ScheduleDeletion")]
        [Authorize]
        public async Task<ActionResult<ScheduledDeletion>> ScheduleDeletion(
            [FromRoute] [Required] Guid itemId,
            [FromQuery] int? delayDays = null,
            [FromQuery] int? delayHours = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can schedule deletions");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Check if media management is enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableMediaManagement != true)
                {
                    return BadRequest("Media management is disabled");
                }

                // Verify item exists
                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound($"Item {itemId} not found");
                }

                // Calculate deletion time - hours takes precedence over days
                DateTime deleteAt;
                string delayDescription;
                if (delayHours.HasValue && delayHours.Value > 0)
                {
                    deleteAt = DateTime.UtcNow.AddHours(delayHours.Value);
                    delayDescription = $"{delayHours.Value} hours";
                }
                else
                {
                    var actualDelayDays = delayDays ?? config?.DefaultDeletionDelayDays ?? 7;
                    if (actualDelayDays < 1)
                    {
                        return BadRequest("Delay must be at least 1 day");
                    }
                    deleteAt = DateTime.UtcNow.AddDays(actualDelayDays);
                    delayDescription = $"{actualDelayDays} days";
                }

                var deletion = new ScheduledDeletion
                {
                    ItemId = itemId,
                    ItemTitle = item.Name,
                    ItemType = item is MediaBrowser.Controller.Entities.Movies.Movie ? "Movie" : "Series",
                    ScheduledByUserId = userId,
                    ScheduledByUsername = user.Username,
                    ScheduledAt = DateTime.UtcNow,
                    DeleteAt = deleteAt
                };

                var result = await _repository.ScheduleDeletionAsync(deletion).ConfigureAwait(false);
                _logger.LogInformation("Admin {UserId} scheduled deletion for item {ItemId} ({Title}) in {Delay}",
                    userId, itemId, item.Name, delayDescription);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling deletion for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Cancels a scheduled deletion (admin only).
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Media/{itemId}/ScheduleDeletion")]
        [Authorize]
        public async Task<ActionResult> CancelScheduledDeletion([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can cancel scheduled deletions");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                var cancelled = await _repository.CancelDeletionAsync(itemId).ConfigureAwait(false);
                if (!cancelled)
                {
                    return NotFound("No scheduled deletion found for this item");
                }

                _logger.LogInformation("Admin {UserId} cancelled scheduled deletion for item {ItemId}", userId, itemId);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling scheduled deletion for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all active scheduled deletions (for displaying badges to all users).
        /// </summary>
        /// <returns>List of scheduled deletions with keep request info.</returns>
        [HttpGet("ScheduledDeletions")]
        [Authorize]
        public async Task<ActionResult<List<object>>> GetScheduledDeletions()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Check if media management is enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableMediaManagement != true)
                {
                    return Ok(new List<object>());
                }

                var deletions = _repository.GetAllScheduledDeletions();
                var keepCounts = _repository.GetAllKeepRequestCounts();
                var autoCancelThreshold = config?.AutoCancelDeletionThreshold ?? 0;

                // Check if current user is admin
                var user = _userManager.GetUserById(userId);
                var isAdmin = user?.HasPermission(PermissionKind.IsAdministrator) ?? false;

                // Build response with keep request info
                var result = deletions.Select(d => new
                {
                    d.Id,
                    d.ItemId,
                    d.ItemTitle,
                    d.ItemType,
                    d.ScheduledByUserId,
                    d.ScheduledByUsername,
                    d.ScheduledAt,
                    d.DeleteAt,
                    d.IsCancelled,
                    d.CancelledAt,
                    KeepRequestCount = keepCounts.TryGetValue(d.ItemId, out var count) ? count : 0,
                    AutoCancelThreshold = autoCancelThreshold,
                    UserHasRequested = _repository.HasUserRequestedKeep(d.ItemId, userId),
                    UserCanRequestToday = !_repository.HasUserRequestedKeepToday(d.ItemId, userId),
                    IsAdmin = isAdmin
                }).ToList<object>();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled deletions");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Submits a request to keep a scheduled-for-deletion item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Result of the keep request.</returns>
        [HttpPost("KeepRequest/{itemId}")]
        [Authorize]
        public async Task<ActionResult<object>> SubmitKeepRequest([FromRoute] [Required] Guid itemId)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Check if media management is enabled
                var config = Plugin.Instance?.Configuration;
                if (config?.EnableMediaManagement != true)
                {
                    return BadRequest("Media management is not enabled");
                }

                // Check if item has a scheduled deletion
                var deletion = _repository.GetScheduledDeletion(itemId);
                if (deletion == null)
                {
                    return NotFound("No scheduled deletion found for this item");
                }

                // Check if user already requested today
                if (_repository.HasUserRequestedKeepToday(itemId, userId))
                {
                    return BadRequest("You have already requested to keep this item today");
                }

                // Get username
                var user = _userManager.GetUserById(userId);
                var username = user?.Username ?? "Unknown";

                // Create keep request
                var keepRequest = new KeepRequest
                {
                    ItemId = itemId,
                    UserId = userId,
                    Username = username
                };

                var result = await _repository.AddKeepRequestAsync(keepRequest).ConfigureAwait(false);
                if (result == null)
                {
                    return BadRequest("Failed to add keep request");
                }

                _logger.LogInformation("User {UserId} ({Username}) requested to keep item {ItemId}", userId, username, itemId);

                // Check if auto-cancel threshold is reached
                var keepCount = _repository.GetKeepRequestCount(itemId);
                var threshold = config?.AutoCancelDeletionThreshold ?? 0;

                if (threshold > 0 && keepCount >= threshold)
                {
                    // Auto-cancel the deletion
                    await _repository.CancelDeletionAsync(itemId).ConfigureAwait(false);
                    _logger.LogInformation("Auto-cancelled deletion for item {ItemId} due to {Count} keep requests (threshold: {Threshold})",
                        itemId, keepCount, threshold);

                    return Ok(new
                    {
                        Success = true,
                        Message = "Request submitted. Deletion has been automatically cancelled!",
                        AutoCancelled = true,
                        KeepRequestCount = keepCount
                    });
                }

                return Ok(new
                {
                    Success = true,
                    Message = "Request submitted successfully",
                    AutoCancelled = false,
                    KeepRequestCount = keepCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting keep request for item {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        // Deletion Request Endpoints

        /// <summary>
        /// Creates a new deletion request for a fulfilled media request.
        /// </summary>
        /// <param name="request">The deletion request data.</param>
        /// <returns>The created deletion request.</returns>
        [HttpPost("DeletionRequests")]
        [Authorize]
        public async Task<ActionResult<DeletionRequest>> CreateDeletionRequest([FromBody] [Required] DeletionRequestDto request)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (string.IsNullOrEmpty(authHeader))
                    {
                        return Unauthorized("No authentication header provided");
                    }

                    var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                    if (!tokenMatch.Success)
                    {
                        return Unauthorized("Invalid authentication header format");
                    }

                    var token = tokenMatch.Groups[1].Value;
                    var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                    if (session == null)
                    {
                        return Unauthorized("Invalid or expired token");
                    }

                    userId = session.UserId;
                }

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Check if user is banned from deletion requests
                var deletionBan = _repository.GetActiveBan(userId, "deletion_request");
                if (deletionBan != null)
                {
                    var banMsg = deletionBan.ExpiresAt.HasValue
                        ? $"You are banned from submitting deletion requests until {deletionBan.ExpiresAt.Value:yyyy-MM-dd HH:mm} UTC"
                        : "You are permanently banned from submitting deletion requests";
                    return BadRequest(banMsg);
                }

                // Validate the original media request exists
                var mediaRequest = await _repository.GetMediaRequestAsync(request.MediaRequestId).ConfigureAwait(false);
                if (mediaRequest == null)
                {
                    return NotFound("Original media request not found");
                }

                // Validate deletion type
                var deletionType = string.IsNullOrEmpty(request.DeletionType) ? "media" : request.DeletionType.ToLower();
                if (deletionType != "request" && deletionType != "media")
                {
                    return BadRequest("DeletionType must be 'request' or 'media'");
                }

                // For "media" type, the request must be "done"
                if (deletionType == "media" && mediaRequest.Status != "done")
                {
                    return BadRequest("Can only request media deletion for fulfilled (done) requests");
                }

                // For "request" type, the request must NOT be done/rejected
                if (deletionType == "request" && (mediaRequest.Status == "done" || mediaRequest.Status == "rejected"))
                {
                    return BadRequest("Cannot request deletion of a completed or rejected request");
                }

                // For media deletions, validate the ItemId exists in the library
                if (deletionType == "media" && request.ItemId != Guid.Empty)
                {
                    var libraryItem = _libraryManager.GetItemById(request.ItemId);
                    if (libraryItem == null)
                    {
                        return BadRequest("The specified media item does not exist in the library");
                    }
                }

                // Check for duplicate pending deletion request
                if (_repository.HasPendingDeletionRequest(request.MediaRequestId))
                {
                    return BadRequest("A pending deletion request already exists for this media request");
                }

                // Limit to 3 total deletion requests per media request
                if (_repository.GetDeletionRequestCountForMediaRequest(request.MediaRequestId) >= 3)
                {
                    return BadRequest("Maximum of 3 deletion requests per media item has been reached");
                }

                var deletionRequest = new DeletionRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Username = user.Username,
                    MediaRequestId = request.MediaRequestId,
                    ItemId = request.ItemId,
                    Title = request.Title,
                    Type = request.Type,
                    MediaLink = request.MediaLink,
                    DeletionType = deletionType,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _repository.AddDeletionRequestAsync(deletionRequest).ConfigureAwait(false);
                _logger.LogInformation("User {UserId} created deletion request for '{Title}' (MediaRequest: {MediaRequestId})",
                    userId, request.Title, request.MediaRequestId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating deletion request");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all deletion requests.
        /// </summary>
        /// <returns>List of all deletion requests.</returns>
        [HttpGet("DeletionRequests")]
        [Authorize]
        public async Task<ActionResult<List<DeletionRequest>>> GetDeletionRequests()
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can view all deletion requests");
                }

                var requests = _repository.GetAllDeletionRequests();
                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting deletion requests");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Admin action on a deletion request (approve or reject).
        /// </summary>
        /// <param name="requestId">The deletion request ID.</param>
        /// <param name="action">The action to take (approve or reject).</param>
        /// <param name="delayDays">Optional delay in days for deletion scheduling.</param>
        /// <param name="delayHours">Optional delay in hours for deletion scheduling.</param>
        /// <param name="rejectionReason">Optional rejection reason when rejecting.</param>
        /// <returns>The updated deletion request.</returns>
        [HttpPost("DeletionRequests/{requestId}/Action")]
        [Authorize]
        public async Task<ActionResult<DeletionRequest>> ActionDeletionRequest(
            [FromRoute] [Required] Guid requestId,
            [FromQuery] [Required] string action,
            [FromQuery] int? delayDays = null,
            [FromQuery] int? delayHours = null,
            [FromQuery] string? rejectionReason = null)
        {
            try
            {
                // Try to get user from authentication
                var userId = User.GetUserId();

                // If standard auth didn't work, try to get from session token
                if (userId == Guid.Empty)
                {
                    var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                                  ?? Request.Headers["Authorization"].FirstOrDefault();

                    if (!string.IsNullOrEmpty(authHeader))
                    {
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                            if (session != null)
                            {
                                userId = session.UserId;
                            }
                        }
                    }
                }

                if (!IsAdminRequest(userId))
                {
                    return Forbid("Only administrators can action deletion requests");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Get the deletion request
                var deletionRequest = _repository.GetDeletionRequestById(requestId);
                if (deletionRequest == null)
                {
                    return NotFound("Deletion request not found");
                }

                if (deletionRequest.Status != "pending")
                {
                    return BadRequest("This deletion request has already been resolved");
                }

                var actionLower = action.ToLower();
                if (actionLower != "approve" && actionLower != "reject")
                {
                    return BadRequest("Action must be 'approve' or 'reject'");
                }

                if (actionLower == "approve")
                {
                    if (deletionRequest.DeletionType == "request")
                    {
                        // Delete the media request itself
                        var deleted = await _repository.DeleteMediaRequestAsync(deletionRequest.MediaRequestId).ConfigureAwait(false);
                        if (!deleted)
                        {
                            _logger.LogWarning("Media request {MediaRequestId} not found when approving deletion request", deletionRequest.MediaRequestId);
                        }

                        var result = await _repository.UpdateDeletionRequestStatusAsync(requestId, "approved", user.Username).ConfigureAwait(false);
                        _logger.LogInformation("Admin {UserId} approved deletion of request {MediaRequestId} via deletion request {RequestId}",
                            userId, deletionRequest.MediaRequestId, requestId);

                        return Ok(result);
                    }
                    else
                    {
                        // Schedule the media deletion using the existing ScheduleDeletion system
                        DateTime deleteAt;
                        if (delayDays.HasValue && delayDays.Value > 0)
                        {
                            deleteAt = DateTime.UtcNow.AddDays(delayDays.Value);
                        }
                        else if (delayHours.HasValue && delayHours.Value > 0)
                        {
                            deleteAt = DateTime.UtcNow.AddHours(delayHours.Value);
                        }
                        else
                        {
                            // Default: 1 hour (near-immediate)
                            deleteAt = DateTime.UtcNow.AddHours(1);
                        }

                        // Try to get item title from library
                        var itemTitle = deletionRequest.Title;
                        var itemType = deletionRequest.Type;
                        var item = _libraryManager.GetItemById(deletionRequest.ItemId);
                        if (item != null)
                        {
                            itemTitle = item.Name;
                            itemType = item is MediaBrowser.Controller.Entities.Movies.Movie ? "Movie" : "Series";
                        }

                        var deletion = new ScheduledDeletion
                        {
                            ItemId = deletionRequest.ItemId,
                            ItemTitle = itemTitle,
                            ItemType = itemType,
                            ScheduledByUserId = userId,
                            ScheduledByUsername = user.Username,
                            ScheduledAt = DateTime.UtcNow,
                            DeleteAt = deleteAt
                        };

                        await _repository.ScheduleDeletionAsync(deletion).ConfigureAwait(false);

                        // Update deletion request status
                        var result = await _repository.UpdateDeletionRequestStatusAsync(requestId, "approved", user.Username).ConfigureAwait(false);
                        _logger.LogInformation("Admin {UserId} approved media deletion request {RequestId} for item {ItemId}, scheduled at {DeleteAt}",
                            userId, requestId, deletionRequest.ItemId, deleteAt);

                        return Ok(result);
                    }
                }
                else
                {
                    // Reject
                    var result = await _repository.UpdateDeletionRequestStatusAsync(requestId, "rejected", user.Username, rejectionReason).ConfigureAwait(false);
                    _logger.LogInformation("Admin {UserId} rejected deletion request {RequestId}", userId, requestId);

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deletion request action");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a user ban.
        /// </summary>
        /// <param name="userId">The user ID to ban.</param>
        /// <param name="banType">The ban type (media_request or deletion_request).</param>
        /// <param name="duration">Duration: 1d, 1w, 1m, or permanent.</param>
        /// <returns>The created ban.</returns>
        [HttpPost("Bans")]
        [Authorize]
        public async Task<ActionResult<UserBan>> CreateBan(
            [FromQuery] [Required] Guid userId,
            [FromQuery] [Required] string banType,
            [FromQuery] [Required] string duration)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(adminId))
                {
                    return Forbid("Only administrators can create bans");
                }

                var admin = _userManager.GetUserById(adminId);
                if (admin == null)
                {
                    return Unauthorized("User not found");
                }

                var banTypeLower = banType.ToLower();
                if (banTypeLower != "media_request" && banTypeLower != "deletion_request")
                {
                    return BadRequest("banType must be 'media_request' or 'deletion_request'");
                }

                // Check if already banned
                var existingBan = _repository.GetActiveBan(userId, banTypeLower);
                if (existingBan != null)
                {
                    return BadRequest("User is already banned for this type");
                }

                var targetUser = _userManager.GetUserById(userId);
                var username = targetUser?.Username ?? "Unknown";

                DateTime? expiresAt = null;
                switch (duration.ToLower())
                {
                    case "1d":
                        expiresAt = DateTime.UtcNow.AddDays(1);
                        break;
                    case "1w":
                        expiresAt = DateTime.UtcNow.AddDays(7);
                        break;
                    case "1m":
                        expiresAt = DateTime.UtcNow.AddDays(30);
                        break;
                    case "permanent":
                        expiresAt = null;
                        break;
                    default:
                        return BadRequest("duration must be '1d', '1w', '1m', or 'permanent'");
                }

                var ban = new UserBan
                {
                    UserId = userId,
                    Username = username,
                    BanType = banTypeLower,
                    ExpiresAt = expiresAt,
                    BannedByUsername = admin.Username
                };

                var result = await _repository.AddUserBanAsync(ban).ConfigureAwait(false);
                _logger.LogInformation("Admin {AdminId} banned user {UserId} from {BanType} for {Duration}",
                    adminId, userId, banTypeLower, duration);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ban");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets active bans by type.
        /// </summary>
        /// <param name="banType">The ban type.</param>
        /// <returns>List of active bans.</returns>
        [HttpGet("Bans")]
        [Authorize]
        public async Task<ActionResult<List<UserBan>>> GetBans([FromQuery] [Required] string banType)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(adminId))
                {
                    return Forbid("Only administrators can view bans");
                }

                var bans = _repository.GetActiveBans(banType.ToLower());
                return Ok(bans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bans");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Checks if the current user is banned for a specific type.
        /// </summary>
        /// <param name="banType">The ban type.</param>
        /// <returns>Ban info or null.</returns>
        [HttpGet("Bans/Check")]
        [Authorize]
        public async Task<ActionResult> CheckBan([FromQuery] [Required] string banType)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var ban = _repository.GetActiveBan(userId, banType.ToLower());
                if (ban != null)
                {
                    return Ok(new { banned = true, expiresAt = ban.ExpiresAt, bannedBy = ban.BannedByUsername });
                }

                return Ok(new { banned = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking ban");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Lifts a user ban.
        /// </summary>
        /// <param name="banId">The ban ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Bans/{banId}")]
        [Authorize]
        public async Task<ActionResult> LiftBan([FromRoute] [Required] Guid banId)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(adminId))
                {
                    return Forbid("Only administrators can lift bans");
                }

                var lifted = await _repository.LiftBanAsync(banId).ConfigureAwait(false);
                if (!lifted)
                {
                    return NotFound("Ban not found");
                }

                _logger.LogInformation("Admin {AdminId} lifted ban {BanId}", adminId, banId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error lifting ban");
                return StatusCode(500, "Internal server error");
            }
        }

        #region Backup & Restore

        /// <summary>
        /// Export all plugin data as a single JSON file for backup.
        /// </summary>
        /// <returns>JSON backup file.</returns>
        [HttpGet("Backup/Export")]
        [Authorize(Policy = "RequiresElevation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ExportBackup()
        {
            try
            {
                var dataPath = Path.Combine(_appPaths.DataPath, "ratings");
                var backupData = new Dictionary<string, object?>
                {
                    { "exportDate", DateTime.UtcNow.ToString("o") },
                    { "pluginVersion", Plugin.Instance?.Version.ToString() ?? "unknown" }
                };

                // List of data files to backup
                var dataFiles = new[]
                {
                    "ratings.json",
                    "media_requests.json",
                    "scheduled_deletions.json",
                    "deletion_requests.json",
                    "user_bans.json",
                    "chat_messages.json",
                    "chat_users.json",
                    "chat_moderators.json",
                    "chat_bans.json",
                    "private_messages.json"
                };

                foreach (var fileName in dataFiles)
                {
                    var filePath = Path.Combine(dataPath, fileName);
                    var key = Path.GetFileNameWithoutExtension(fileName);

                    if (System.IO.File.Exists(filePath))
                    {
                        var content = await System.IO.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                        try
                        {
                            backupData[key] = System.Text.Json.JsonSerializer.Deserialize<object>(content);
                        }
                        catch
                        {
                            backupData[key] = null;
                        }
                    }
                    else
                    {
                        backupData[key] = null;
                    }
                }

                // Update last backup date in config
                var config = Plugin.Instance?.Configuration;
                if (config != null)
                {
                    config.LastBackupDate = DateTime.UtcNow.ToString("o");
                    Plugin.Instance?.SaveConfiguration();
                }

                var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var json = System.Text.Json.JsonSerializer.Serialize(backupData, jsonOptions);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var fileName2 = $"ratings_backup_{DateTime.UtcNow:yyyy-MM-dd_HHmmss}.json";

                return File(bytes, "application/json", fileName2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting backup");
                return StatusCode(500, "Failed to export backup");
            }
        }

        /// <summary>
        /// Import plugin data from a backup file.
        /// </summary>
        /// <param name="backupJson">The backup JSON content.</param>
        /// <returns>Import result.</returns>
        [HttpPost("Backup/Import")]
        [Authorize(Policy = "RequiresElevation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> ImportBackup([FromBody] System.Text.Json.JsonElement backupJson)
        {
            try
            {
                var dataPath = Path.Combine(_appPaths.DataPath, "ratings");

                // Ensure directory exists
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                // Map of backup keys to file names
                var keyToFile = new Dictionary<string, string>
                {
                    { "ratings", "ratings.json" },
                    { "media_requests", "media_requests.json" },
                    { "scheduled_deletions", "scheduled_deletions.json" },
                    { "deletion_requests", "deletion_requests.json" },
                    { "user_bans", "user_bans.json" },
                    { "chat_messages", "chat_messages.json" },
                    { "chat_users", "chat_users.json" },
                    { "chat_moderators", "chat_moderators.json" },
                    { "chat_bans", "chat_bans.json" },
                    { "private_messages", "private_messages.json" }
                };

                var importedCount = 0;

                foreach (var kvp in keyToFile)
                {
                    if (backupJson.TryGetProperty(kvp.Key, out var data) && data.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        var filePath = Path.Combine(dataPath, kvp.Value);
                        var json = data.GetRawText();

                        // Sanitize chat messages on import to prevent XSS
                        if (kvp.Key == "chat_messages" || kvp.Key == "private_messages")
                        {
                            json = SanitizeChatMessagesJson(json);
                        }

                        await System.IO.File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
                        importedCount++;
                    }
                }

                // Reload data in repository
                await _repository.ReloadAllDataAsync().ConfigureAwait(false);

                _logger.LogInformation("Backup imported successfully. {Count} data files restored.", importedCount);
                return Ok(new { success = true, message = $"Imported {importedCount} data files. Please restart Jellyfin for full effect." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing backup");
                return StatusCode(500, "Failed to import backup");
            }
        }

        /// <summary>
        /// Sanitizes chat message content in backup JSON to prevent XSS.
        /// </summary>
        private string SanitizeChatMessagesJson(string json)
        {
            try
            {
                var messages = System.Text.Json.JsonSerializer.Deserialize<List<ChatMessage>>(json);
                if (messages == null) return json;

                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                    {
                        msg.Content = SanitizeInput(msg.Content, 500);
                    }
                    // Also sanitize username to be safe
                    if (!string.IsNullOrEmpty(msg.UserName))
                    {
                        msg.UserName = SanitizeInput(msg.UserName, 100);
                    }
                }

                return System.Text.Json.JsonSerializer.Serialize(messages);
            }
            catch
            {
                // Return original if parsing fails - will still be validated on load
                return json;
            }
        }

        /// <summary>
        /// Get backup status (last backup date).
        /// </summary>
        /// <returns>Backup status.</returns>
        [HttpGet("Backup/Status")]
        [Authorize(Policy = "RequiresElevation")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult GetBackupStatus()
        {
            var config = Plugin.Instance?.Configuration;
            var lastBackup = config?.LastBackupDate;

            DateTime? lastBackupDate = null;
            int? daysSinceBackup = null;

            if (!string.IsNullOrEmpty(lastBackup) && DateTime.TryParse(lastBackup, out var parsed))
            {
                lastBackupDate = parsed;
                daysSinceBackup = (int)(DateTime.UtcNow - parsed).TotalDays;
            }

            return Ok(new
            {
                lastBackupDate,
                daysSinceBackup,
                neverBackedUp = lastBackupDate == null
            });
        }

        #endregion

        /// <summary>
        /// Helper to get authenticated user ID from headers.
        /// </summary>
        private async Task<Guid> GetAuthenticatedUserIdAsync()
        {
            var userId = User.GetUserId();
            if (userId != Guid.Empty)
            {
                return userId;
            }

            var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                          ?? Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader))
            {
                var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                if (tokenMatch.Success)
                {
                    var token = tokenMatch.Groups[1].Value;
                    var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                    if (session != null)
                    {
                        return session.UserId;
                    }
                }
            }

            return Guid.Empty;
        }

        #region Admin - Disk Usage

        /// <summary>
        /// Gets disk usage information for all physical drives.
        /// </summary>
        [HttpGet("Admin/DiskUsage")]
        [Authorize]
        public async Task<ActionResult> GetDiskUsage()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                var disks = new List<object>();

                // On Linux, read /proc/mounts to get physical device info
                if (System.IO.File.Exists("/proc/mounts"))
                {
                    var mountInfo = GetLinuxPhysicalDisks();
                    disks.AddRange(mountInfo);
                }
                else
                {
                    // Windows: use DriveInfo directly (each drive letter = separate disk)
                    var allDrives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                        .Select(d => new
                        {
                            DriveLetter = d.Name.TrimEnd('\\'),
                            DriveName = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                            TotalSizeGB = Math.Round(d.TotalSize / 1073741824.0, 2),
                            UsedSizeGB = Math.Round((d.TotalSize - d.AvailableFreeSpace) / 1073741824.0, 2),
                            FreeSizeGB = Math.Round(d.AvailableFreeSpace / 1073741824.0, 2),
                            UsedPercent = Math.Round((d.TotalSize - d.AvailableFreeSpace) * 100.0 / d.TotalSize, 1),
                            DriveType = d.DriveType.ToString(),
                            DriveFormat = d.DriveFormat,
                            MountPoints = new List<string> { d.Name.TrimEnd('\\') }
                        })
                        .ToList();
                    disks.AddRange(allDrives.Cast<object>());
                }

                var totalStorage = disks.Sum(d => (double)d.GetType().GetProperty("TotalSizeGB")!.GetValue(d)!);
                var totalUsed = disks.Sum(d => (double)d.GetType().GetProperty("UsedSizeGB")!.GetValue(d)!);
                var totalFree = disks.Sum(d => (double)d.GetType().GetProperty("FreeSizeGB")!.GetValue(d)!);

                return Ok(new
                {
                    Disks = disks,
                    TotalStorageGB = Math.Round(totalStorage, 2),
                    TotalUsedGB = Math.Round(totalUsed, 2),
                    TotalFreeGB = Math.Round(totalFree, 2)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting disk usage");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets unique filesystems on Linux, handling Docker/LVM environments.
        /// Uses device names from /proc/mounts to correctly identify separate physical disks.
        /// </summary>
        private List<object> GetLinuxPhysicalDisks()
        {
            var result = new List<object>();

            try
            {
                // Build mount point -> device mapping from /proc/mounts
                var mountToDevice = new Dictionary<string, string>();
                if (System.IO.File.Exists("/proc/mounts"))
                {
                    var mountLines = System.IO.File.ReadAllLines("/proc/mounts");
                    foreach (var line in mountLines)
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2) continue;

                        var device = parts[0];
                        var mountPoint = parts[1].TrimEnd('/');
                        if (string.IsNullOrEmpty(mountPoint)) mountPoint = "/";

                        // Only track /dev/ devices
                        if (!device.StartsWith("/dev/")) continue;

                        mountToDevice[mountPoint] = device;
                    }
                }

                // Get all drives and group by device
                var allDrives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .ToList();

                // Filter meaningful drives and find their device
                var driveDevicePairs = new List<(DriveInfo Drive, string Device, string MountPoint)>();
                foreach (var drive in allDrives)
                {
                    var mountPoint = drive.Name.TrimEnd('/');
                    if (string.IsNullOrEmpty(mountPoint)) mountPoint = "/";

                    // Skip system paths
                    if (mountPoint.StartsWith("/proc")) continue;
                    if (mountPoint.StartsWith("/sys")) continue;
                    if (mountPoint.StartsWith("/run")) continue;
                    if (mountPoint.StartsWith("/dev/")) continue;
                    if (mountPoint.StartsWith("/etc/")) continue;

                    // Find device for this mount
                    var device = mountToDevice.GetValueOrDefault(mountPoint, "unknown");
                    driveDevicePairs.Add((drive, device, mountPoint));
                }

                // Group by device
                var grouped = driveDevicePairs.GroupBy(p => p.Device).ToList();

                int diskNumber = 1;
                int mediaNumber = 1;

                foreach (var group in grouped)
                {
                    var device = group.Key;
                    var representative = group.First().Drive;
                    var mountPoints = group
                        .Select(p => p.MountPoint)
                        .Where(m => !string.IsNullOrEmpty(m))
                        .Distinct()
                        .OrderBy(m => m.Length)
                        .ToList();

                    if (mountPoints.Count == 0) continue;

                    // Determine a nice name based on mount points
                    string driveName;
                    var primaryMount = mountPoints.FirstOrDefault() ?? "/";
                    if (primaryMount == "/" || primaryMount == "")
                    {
                        driveName = "System";
                    }
                    else if (primaryMount.Contains("media") || primaryMount.Contains("cache") || primaryMount.Contains("config"))
                    {
                        driveName = $"Disk {diskNumber++}";
                    }
                    else
                    {
                        driveName = $"Disk {diskNumber++}";
                    }

                    result.Add(new
                    {
                        DriveLetter = primaryMount,
                        DriveName = driveName,
                        TotalSizeGB = Math.Round(representative.TotalSize / 1073741824.0, 2),
                        UsedSizeGB = Math.Round((representative.TotalSize - representative.AvailableFreeSpace) / 1073741824.0, 2),
                        FreeSizeGB = Math.Round(representative.AvailableFreeSpace / 1073741824.0, 2),
                        UsedPercent = Math.Round((representative.TotalSize - representative.AvailableFreeSpace) * 100.0 / representative.TotalSize, 1),
                        DriveType = representative.DriveType.ToString(),
                        DriveFormat = representative.DriveFormat ?? "Unknown",
                        MountPoints = mountPoints,
                        Device = device
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting Linux disk info");
            }

            return result.OrderByDescending(d => (double)d.GetType().GetProperty("TotalSizeGB")!.GetValue(d)!).ToList();
        }

        #endregion

        #region Admin - Duplicate Finder

        /// <summary>
        /// Finds duplicate media items - Movies/Series by IMDB ID, Music by title.
        /// Episodes are excluded (they share IMDB ID with parent series).
        /// </summary>
        [HttpGet("Admin/Duplicates")]
        [Authorize]
        public async Task<ActionResult> GetDuplicates()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                var duplicateGroups = new List<object>();

                // 1. Check Movies and Series by IMDB ID (NOT Episodes - they share parent's IMDB)
                var videoItems = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series },
                    Recursive = true
                });

                var videoDuplicates = videoItems
                    .Where(i => i.ProviderIds?.ContainsKey("Imdb") == true && !string.IsNullOrEmpty(i.ProviderIds["Imdb"]))
                    .GroupBy(i => i.ProviderIds["Imdb"])
                    .Where(g => g.Count() > 1)
                    .Select(g => BuildDuplicateGroup(g.Key, g.First().Name, g.First().ProductionYear, g.ToList(), "Video"))
                    .ToList();

                duplicateGroups.AddRange(videoDuplicates);

                // 2. Check Music by normalized title (artist + title)
                var musicItems = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Audio },
                    Recursive = true
                });

                var musicDuplicates = musicItems
                    .Where(i => !string.IsNullOrEmpty(i.Name))
                    .GroupBy(i =>
                    {
                        // Group by normalized: artist + title (lowercase, trimmed)
                        var artist = (i as MediaBrowser.Controller.Entities.Audio.Audio)?.Artists?.FirstOrDefault() ?? "";
                        var title = i.Name?.Trim().ToLowerInvariant() ?? "";
                        return $"{artist.Trim().ToLowerInvariant()}|{title}";
                    })
                    .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key.Split('|').LastOrDefault()))
                    .Select(g =>
                    {
                        var first = g.First();
                        var artist = (first as MediaBrowser.Controller.Entities.Audio.Audio)?.Artists?.FirstOrDefault() ?? "";
                        var displayTitle = string.IsNullOrEmpty(artist) ? first.Name : $"{artist} - {first.Name}";
                        return BuildDuplicateGroup(g.Key, displayTitle, first.ProductionYear, g.ToList(), "Music");
                    })
                    .ToList();

                duplicateGroups.AddRange(musicDuplicates);

                // Sort all by size descending
                var sortedDuplicates = duplicateGroups
                    .OrderByDescending(d => ((dynamic)d).TotalSizeGB)
                    .ToList();

                var potentialSavings = sortedDuplicates.Sum(d => (double)((dynamic)d).TotalSizeGB - ((IEnumerable<dynamic>)((dynamic)d).Items).Max(i => (double)i.SizeGB));

                return Ok(new
                {
                    Duplicates = sortedDuplicates,
                    TotalDuplicateGroups = sortedDuplicates.Count,
                    TotalDuplicateItems = sortedDuplicates.Sum(d => (int)((dynamic)d).ItemCount),
                    PotentialSavingsGB = Math.Round(potentialSavings, 2)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding duplicates");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Helper to build duplicate group object.
        /// </summary>
        private object BuildDuplicateGroup(string groupKey, string title, int? year, List<MediaBrowser.Controller.Entities.BaseItem> items, string mediaType)
        {
            var itemDetails = items.Select(i =>
            {
                double sizeGB = 0;
                string quality = "Unknown";
                try
                {
                    if (!string.IsNullOrEmpty(i.Path) && System.IO.File.Exists(i.Path))
                    {
                        var fileInfo = new FileInfo(i.Path);
                        sizeGB = Math.Round(fileInfo.Length / 1073741824.0, 2);
                    }

                    // Try to determine quality from video stream (for video items)
                    if (mediaType == "Video")
                    {
                        var mediaStreams = i.GetMediaStreams();
                        var videoStream = mediaStreams?.FirstOrDefault(s => s.Type == MediaBrowser.Model.Entities.MediaStreamType.Video);
                        if (videoStream != null && videoStream.Height.HasValue)
                        {
                            var height = videoStream.Height.Value;
                            quality = height >= 2160 ? "4K" : height >= 1080 ? "1080p" : height >= 720 ? "720p" : height >= 480 ? "480p" : "SD";
                        }
                    }
                    else if (mediaType == "Music")
                    {
                        // For music, show bitrate as quality
                        var audioStream = i.GetMediaStreams()?.FirstOrDefault(s => s.Type == MediaBrowser.Model.Entities.MediaStreamType.Audio);
                        if (audioStream?.BitRate != null)
                        {
                            quality = $"{audioStream.BitRate / 1000}kbps";
                        }
                    }
                }
                catch { }

                return new
                {
                    ItemId = i.Id,
                    Name = i.Name,
                    Path = i.Path,
                    SizeGB = sizeGB,
                    DateAdded = i.DateCreated,
                    Quality = quality,
                    Container = System.IO.Path.GetExtension(i.Path)?.TrimStart('.') ?? ""
                };
            }).OrderByDescending(x => x.SizeGB).ToList();

            return new
            {
                ImdbId = groupKey,
                Title = title,
                Year = year,
                MediaType = mediaType,
                Items = itemDetails,
                TotalSizeGB = Math.Round(itemDetails.Sum(x => x.SizeGB), 2),
                ItemCount = itemDetails.Count
            };
        }

        /// <summary>
        /// Deletes a duplicate item.
        /// </summary>
        [HttpDelete("Admin/Duplicates/{itemId}")]
        [Authorize]
        public async Task<ActionResult> DeleteDuplicate(
            [FromRoute] [Required] Guid itemId,
            [FromQuery] bool deleteFile = false)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound("Item not found");
                }

                var filePath = item.Path;
                double freedSpace = 0;

                if (deleteFile && !string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    freedSpace = Math.Round(fileInfo.Length / 1073741824.0, 2);
                }

                // Delete from library
                _libraryManager.DeleteItem(item, new MediaBrowser.Controller.Library.DeleteOptions
                {
                    DeleteFileLocation = deleteFile
                });

                _logger.LogInformation("Admin deleted duplicate item {ItemId}, deleteFile={DeleteFile}", itemId, deleteFile);

                return Ok(new
                {
                    Success = true,
                    ItemId = itemId,
                    DeletedFile = deleteFile,
                    FreedSpaceGB = freedSpace
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting duplicate {ItemId}", itemId);
                return StatusCode(500, "Internal server error");
            }
        }

        #endregion

        #region Admin - Server Restart

        /// <summary>
        /// Schedules a server restart with countdown notification.
        /// </summary>
        [HttpPost("Admin/ScheduleRestart")]
        [Authorize]
        public async Task<ActionResult> ScheduleRestart([FromBody] RestartRequest request)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                if (_restartCts != null)
                {
                    return BadRequest("Restart already scheduled. Cancel it first.");
                }

                var delayMinutes = request?.DelayMinutes ?? 2;
                if (delayMinutes < 1 || delayMinutes > 60)
                {
                    return BadRequest("Delay must be between 1 and 60 minutes");
                }

                var delaySeconds = delayMinutes * 60;
                _restartScheduledAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                _restartReason = request?.Reason ?? "Server maintenance";
                _restartCts = new CancellationTokenSource();

                _logger.LogInformation("Server restart scheduled in {Minutes} minutes by admin", delayMinutes);

                // Start countdown broadcast task
                _ = BroadcastRestartCountdownAsync(delaySeconds, _restartCts.Token);

                return Ok(new
                {
                    Success = true,
                    RestartAt = _restartScheduledAt,
                    DelaySeconds = delaySeconds,
                    Message = $"Server restart scheduled in {delayMinutes} minute{(delayMinutes != 1 ? "s" : "")}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling restart");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Cancels a scheduled server restart.
        /// </summary>
        [HttpDelete("Admin/ScheduleRestart")]
        [Authorize]
        public async Task<ActionResult> CancelRestart()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                if (_restartCts == null)
                {
                    return BadRequest("No restart scheduled");
                }

                _restartCts.Cancel();
                _restartCts = null;
                _restartScheduledAt = null;
                _restartReason = null;

                _logger.LogInformation("Scheduled server restart cancelled by admin");

                // Notify all clients
                await _socialWebSocketListener.BroadcastToAllAsync(new
                {
                    MessageType = "ServerRestartCancelled",
                    Data = new { Message = "Server restart has been cancelled" }
                }).ConfigureAwait(false);

                return Ok(new { Success = true, Message = "Scheduled restart cancelled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling restart");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the current restart status.
        /// </summary>
        [HttpGet("Admin/RestartStatus")]
        [Authorize]
        public async Task<ActionResult> GetRestartStatus()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid("Admin access required");
                }

                if (_restartScheduledAt == null)
                {
                    return Ok(new { IsScheduled = false });
                }

                var remaining = (_restartScheduledAt.Value - DateTime.UtcNow).TotalSeconds;
                return Ok(new
                {
                    IsScheduled = true,
                    RestartAt = _restartScheduledAt,
                    SecondsRemaining = Math.Max(0, (int)remaining),
                    Reason = _restartReason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting restart status");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task BroadcastRestartCountdownAsync(int totalSeconds, CancellationToken ct)
        {
            try
            {
                for (int remaining = totalSeconds; remaining >= 0; remaining--)
                {
                    if (ct.IsCancellationRequested) break;

                    var phase = remaining > 30 ? "warning" : remaining > 10 ? "critical" : "imminent";
                    var formatted = $"{remaining / 60}:{(remaining % 60):D2}";

                    // Broadcast to ALL connected clients
                    await _socialWebSocketListener.BroadcastToAllAsync(new
                    {
                        MessageType = "ServerRestartCountdown",
                        Data = new
                        {
                            SecondsRemaining = remaining,
                            FormattedTime = formatted,
                            Reason = _restartReason,
                            Phase = phase
                        }
                    }).ConfigureAwait(false);

                    if (remaining > 0)
                    {
                        try
                        {
                            await Task.Delay(1000, ct).ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Restarting server now");
                    _restartCts = null;
                    _restartScheduledAt = null;
                    _restartReason = null;

                    // Actually restart Jellyfin
                    _systemManager.Restart();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in restart countdown");
            }
            finally
            {
                _restartCts = null;
                _restartScheduledAt = null;
                _restartReason = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Request model for scheduling server restart.
    /// </summary>
    public class RestartRequest
    {
        /// <summary>
        /// Delay in minutes before restart.
        /// </summary>
        public int DelayMinutes { get; set; } = 2;

        /// <summary>
        /// Optional reason for restart.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Extension methods for user claims.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Gets the user ID from claims.
        /// </summary>
        /// <param name="principal">Claims principal.</param>
        /// <returns>User ID.</returns>
        public static Guid GetUserId(this System.Security.Claims.ClaimsPrincipal principal)
        {
            var userId = principal.FindFirst("Jellyfin.UserId")?.Value;
            return Guid.TryParse(userId, out var id) ? id : Guid.Empty;
        }
    }
}
