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
        private readonly MediaBrowser.Controller.IO.IPathManager? _pathManager;

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
            ISystemManager systemManager,
            MediaBrowser.Controller.IO.IPathManager? pathManager = null)
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
            _pathManager = pathManager;
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
                // Shared helper, not the hand-rolled fallback that used to be here: that one read
                // only X-Emby-Authorization/Authorization and pulled Token="..." out of it, so a
                // caller sending a plain X-Emby-Token header was rejected. Same fault as issue #72.
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
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

                // Remember what was rated, so the entry still shows a title and a poster if the
                // item is later removed from the library (issue #72).
                var snapshot = new RatingSnapshot
                {
                    Title = item.Name,
                    Year = item.ProductionYear,
                    MediaType = item is MediaBrowser.Controller.Entities.TV.Series ? "Series" : "Movie",
                    IsExternal = false
                };

                var result = await _repository.SetRatingAsync(userId, itemId, rating, tmdbId, imdbId, aniDbId, sanitizedReview, snapshot).ConfigureAwait(false);
                _logger.LogDebug("User rated item {ItemId} with {Rating}", itemId, rating);

                // Jellyfin's own image URL dies with the item, so fetch a poster that will outlive
                // it. Deliberately off the request path - rating must stay instant.
                QueueTmdbPosterBackfill(result);

                // Mirror into Jellyfin's native fields so external apps can read it via the API.
                WriteNativeRating(userId, item, rating);

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
        public async Task<ActionResult<Dictionary<string, RatingStats>>> GetBatchRatingStats([FromQuery] [Required] string itemIds)
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

                // Resolved through the shared helper so every way a client can present a token
                // works here, not just the ones that populate the claim (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                // Batch fetch items from library manager and extract provider IDs
                var itemsWithProviders = new List<(Guid ItemId, string? TmdbId, string? ImdbId, string? AniDbId)>();
                foreach (var id in ids)
                {
                    var item = _libraryManager.GetItemById(id);
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

                    itemsWithProviders.Add((item.Id, tmdbId, imdbId, aniDbId));
                }

                // Use batch method - single lock acquisition for all items
                var result = _repository.GetBatchRatingStats(itemsWithProviders, userId != Guid.Empty ? userId : null);

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
        public async Task<ActionResult<UserRating>> GetUserRating([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Resolved through the shared helper so every way a client can present a token
                // works here, not just the ones that populate the claim (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
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
                // Shared helper: the hand-rolled fallback that used to be here missed at least
                // one of the ways a client can present a token (issue #72).
                var authUserId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (authUserId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Use provided userId or fall back to authenticated user
                var targetUserId = userId ?? authUserId;

                // Allow viewing other users' ratings for profile pages
                // Privacy settings are handled at the profile API level

                var ratings = _repository.GetUserRatings(targetUserId);
                _logger.LogDebug("Retrieved {Count} ratings for user", ratings.Count);

                return Ok(EnrichRatings(ratings, authUserId));
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var ratings = _repository.GetUserRatings(userId);
                _logger.LogInformation("Retrieved {Count} ratings for current user {UserId}", ratings.Count, userId);

                return Ok(EnrichRatings(ratings, userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting ratings for current user");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Enriches raw ratings with the resolved Jellyfin item name, year, type and library presence
        /// so profile pages can show real media names (instead of "Unknown") and link to the item.
        /// All original UserRating fields are preserved for backward compatibility.
        /// </summary>
        /// <param name="ratings">The raw ratings.</param>
        /// <returns>Enriched rating objects.</returns>
        private List<object> EnrichRatings(IEnumerable<UserRating> ratings, Guid viewerId = default)
        {
            var list = ratings as IList<UserRating> ?? ratings.ToList();

            // Resolve ALL items in a single library query instead of one lookup per rating
            // (the per-item loop made profile loads very slow for users with many ratings).
            var itemMap = new Dictionary<Guid, MediaBrowser.Controller.Entities.BaseItem>();
            try
            {
                var ids = list.Select(r => r.ItemId).Where(id => id != Guid.Empty).Distinct().ToArray();
                if (ids.Length > 0)
                {
                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery { ItemIds = ids };
                    foreach (var it in _libraryManager.GetItemList(query))
                    {
                        itemMap[it.Id] = it;
                    }
                }
            }
            catch
            {
                // fall through with whatever resolved
            }

            // Like/dislike counts for every rating, not only ones carrying review text - a plain
            // star rating can be agreed or disagreed with too. Collected in a single pass, since
            // the per-item lookup walks the whole like collection each time.
            Dictionary<(Guid ReviewerUserId, Guid ItemId), (int LikeCount, int DislikeCount)> likeMap;
            try
            {
                likeMap = _repository.GetReviewLikeCountsFor(list.Select(r => r.UserId).Distinct());
            }
            catch
            {
                likeMap = new Dictionary<(Guid, Guid), (int, int)>();
            }

            // The viewer's own votes, so the buttons come back highlighted after a reload rather
            // than only until the page is refreshed.
            Dictionary<(Guid ReviewerUserId, Guid ItemId), bool> myVotes;
            try
            {
                myVotes = _repository.GetUserReviewLikes(viewerId);
            }
            catch
            {
                myVotes = new Dictionary<(Guid, Guid), bool>();
            }

            var result = new List<object>(list.Count);
            foreach (var r in list)
            {
                itemMap.TryGetValue(r.ItemId, out var item);

                likeMap.TryGetValue((r.UserId, r.ItemId), out var lc);
                var likeCount = lc.LikeCount;
                var dislikeCount = lc.DislikeCount;

                // Fall back to the snapshot taken when the rating was made. Without this a film
                // removed from the library left a row of stars with no title and no poster,
                // because everything shown came from the (now missing) library item.
                var name = item?.Name ?? r.Title;
                var year = item?.ProductionYear ?? r.Year;
                var type = item?.GetType().Name ?? r.MediaType;

                // A live library item always wins for the poster - it is local and always current.
                // r.PosterUrl is the remembered one, used once the item is gone.
                var poster = item != null
                    ? "/Items/" + item.Id.ToString("N") + "/Images/Primary"
                    : r.PosterUrl;

                result.Add(new
                {
                    r.Id,
                    r.UserId,
                    r.ItemId,
                    r.TmdbId,
                    r.ImdbId,
                    r.AniDbId,
                    r.Rating,
                    r.ReviewText,
                    r.CreatedAt,
                    r.UpdatedAt,
                    ItemName = name,
                    Year = year,
                    Type = type,
                    InLibrary = item != null,
                    IsExternal = r.IsExternal,
                    PosterUrl = poster,
                    LikeCount = likeCount,
                    DislikeCount = dislikeCount,
                    UserLiked = myVotes.TryGetValue((r.UserId, r.ItemId), out var mine) ? (bool?)mine : null
                });
            }

            return result;
        }

        // A punctuation-insensitive name index, rebuilt at most every few minutes.
        // "The X-Files", "x files" and "xfiles" all reduce to "thexfiles"/"xfiles", so a query
        // matches however the user types the separators - Jellyfin's own SearchTerm does not.
        private static List<(Guid Id, string Normalized)>? _searchIndex;
        private static DateTime _searchIndexBuiltUtc = DateTime.MinValue;
        private static readonly object _searchIndexLock = new();
        private static readonly TimeSpan _searchIndexTtl = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Reduces a title to letters and digits, lower-cased, so punctuation and spacing stop
        /// mattering: "The X-Files" and "xfiles" both become comparable.
        /// </summary>
        /// <param name="value">Title or query.</param>
        /// <returns>The normalized form.</returns>
        /// <summary>
        /// Returns a link only if it parses as an absolute http(s) URL, in its parsed form.
        ///
        /// Storing the string as typed is not safe even after a host check: Uri.TryCreate accepts
        /// embedded quotes and spaces and still reports the expected host, and these links are
        /// rendered into an href. AbsoluteUri percent-encodes anything that could break out.
        /// </summary>
        /// <param name="value">The submitted link.</param>
        /// <returns>The normalized URL, or empty.</returns>
        private static string NormalizeExternalLink(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return string.Empty;
            }

            return uri.AbsoluteUri;
        }

        private static string NormalizeForSearch(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the ids of movies and series whose name matches the query once punctuation and
        /// spacing are ignored. Used as a fallback when Jellyfin's own search finds nothing.
        /// </summary>
        /// <param name="query">The raw user query.</param>
        /// <param name="limit">Maximum ids to return.</param>
        /// <returns>Matching item ids, best (prefix) matches first.</returns>
        private List<Guid> FindByNormalizedName(string query, int limit)
        {
            var needle = NormalizeForSearch(query);
            if (needle.Length == 0)
            {
                return new List<Guid>();
            }

            List<(Guid Id, string Normalized)> index;
            lock (_searchIndexLock)
            {
                if (_searchIndex == null || DateTime.UtcNow - _searchIndexBuiltUtc > _searchIndexTtl)
                {
                    var built = new List<(Guid, string)>();
                    var all = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        IncludeItemTypes = new[]
                        {
                            Jellyfin.Data.Enums.BaseItemKind.Movie,
                            Jellyfin.Data.Enums.BaseItemKind.Series,
                        },
                        Recursive = true,
                    });

                    foreach (var it in all)
                    {
                        var n = NormalizeForSearch(it.Name);
                        if (n.Length > 0)
                        {
                            built.Add((it.Id, n));
                        }
                    }

                    _searchIndex = built;
                    _searchIndexBuiltUtc = DateTime.UtcNow;
                }

                index = _searchIndex;
            }

            // Titles that start with the query are the better answer, so they come first.
            return index
                .Where(e => e.Normalized.Contains(needle, StringComparison.Ordinal))
                .OrderByDescending(e => e.Normalized.StartsWith(needle, StringComparison.Ordinal))
                .ThenBy(e => e.Normalized.Length)
                .Take(limit)
                .Select(e => e.Id)
                .ToList();
        }

        /// <summary>
        /// Searches the library ignoring punctuation and spacing, so "xfiles", "x files" and
        /// "X-Files" all find the same show.
        /// </summary>
        /// <param name="query">Search text.</param>
        /// <param name="limit">Maximum results (1-50).</param>
        /// <returns>Matching items with the fields the pickers need.</returns>
        [HttpGet("Search")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> SearchLibrary(
            [FromQuery] string? query = null,
            [FromQuery] [Range(1, 50)] int limit = 10)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                // Ask the index for more than we need: the permission-scoped fetch below drops
                // anything this account cannot see, and trimming afterwards keeps a restricted
                // user from getting a short list just because the extras were filtered out.
                var ids = FindByNormalizedName(query ?? string.Empty, limit * 4);
                if (ids.Count == 0)
                {
                    return Ok(new { items = Array.Empty<object>() });
                }

                // Scoped to the caller. The index behind FindByNormalizedName is built once for the
                // whole server, so without this a user could find titles in libraries Jellyfin does
                // not let them browse. Jellyfin applies the account's library permissions when the
                // query carries the user, so inaccessible ids simply do not come back.
                var searchUser = _userManager.GetUserById(userId);
                var items = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    ItemIds = ids.ToArray(),
                    User = searchUser,
                });

                // Preserve the ranking from the index rather than the library's own ordering.
                var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
                var result = items
                    .OrderBy(i => order.TryGetValue(i.Id, out var pos) ? pos : int.MaxValue)
                    .Take(limit)
                    .Select(i => new
                    {
                        Id = i.Id.ToString("N"),
                        i.Name,
                        i.ProductionYear,
                        Type = i is MediaBrowser.Controller.Entities.TV.Series ? "Series" : "Movie",
                        i.Overview,
                    })
                    .ToList();

                return Ok(new { items = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching library");
                return StatusCode(500, "Internal server error");
            }
        }

        private static readonly System.Net.Http.HttpClient _tmdbHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        // Ratings whose poster has already been looked up (or attempted) this process, so a user
        // re-rating the same title does not re-hit TMDB.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _posterBackfillSeen = new();

        // Library items Jellyfin holds no artwork for, mapped to a TMDB poster we resolved.
        // In-memory only: it is a display nicety, and a restart simply looks them up again.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> _externalPosterCache = new();

        // Items already attempted this process, successful or not, so a title TMDB does not
        // know is not re-requested on every page view.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _externalPosterSeen = new();

        // Matches an IMDB id left in an item's name by the filename, e.g. "Undertone [tt35892608]".
        // Unidentified items usually have no provider ids, so the name is the only source.
        // (A [GeneratedRegex] partial method would need the controller to be partial.)
        private static readonly Regex _imdbIdInName = new(
            @"\b(tt\d{7,10})\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(200));

        /// <summary>
        /// Builds a stable ItemId for a TMDB title that is not on the server.
        /// </summary>
        /// <remarks>
        /// Everything in the repository is keyed on ItemId, so external ratings need one. Deriving
        /// it deterministically from the TMDB id means the same title always lands on the same key,
        /// and because the rating also stores its TmdbId, the existing provider-id fallback will
        /// migrate it onto the real library item automatically if the title is ever added to the
        /// server - the user keeps their rating with no special handling anywhere.
        /// </remarks>
        /// <param name="tmdbId">TMDB numeric id.</param>
        /// <param name="mediaType">"Movie" or "Series".</param>
        /// <returns>A deterministic id for this external title.</returns>
        internal static Guid GetExternalItemId(string tmdbId, string mediaType)
        {
            var seed = "ratings-plugin:tmdb:" + (mediaType ?? "Movie").ToLowerInvariant() + ":" + tmdbId;
            var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
            return new Guid(bytes);
        }

        /// <summary>
        /// Looks up a TMDB poster for a rating in the background and stores it on the rating.
        /// </summary>
        /// <remarks>
        /// Only runs when a TMDB token is configured, the rating has a TMDB id, and no poster has
        /// been stored yet. Failure is silent: a missing poster is a cosmetic loss, and this must
        /// never affect whether the rating itself was saved.
        /// </remarks>
        /// <param name="rating">The rating just written.</param>
        /// <summary>
        /// Looks up a TMDB poster for a library item Jellyfin holds no artwork for.
        ///
        /// An item Jellyfin never identified has no images and often no provider ids either -
        /// its name still carries the raw "[tt1234567]" from the filename. So the IMDB id is
        /// taken from the provider ids when present and scraped from the name otherwise, then
        /// resolved through TMDB's /find endpoint.
        ///
        /// Runs off the request thread and caches the result, so the list response is never
        /// held up by network calls; the poster appears on the next load. Failures are cached
        /// as a miss too, so a title TMDB does not know is not retried on every page view.
        /// </summary>
        /// <param name="item">The library item lacking a primary image.</param>
        private void QueueExternalPosterLookup(MediaBrowser.Controller.Entities.BaseItem? item)
        {
            if (item == null || Plugin.Instance?.Configuration?.EnableExternalPosterFallback != true)
            {
                return;
            }

            var token = Plugin.Instance?.Configuration?.TmdbApiToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            // One attempt per item per server lifetime.
            if (!_externalPosterSeen.TryAdd(item.Id, 0))
            {
                return;
            }

            var itemId = item.Id;
            var isSeries = item is MediaBrowser.Controller.Entities.TV.Series;
            string? tmdbId = null;
            string? imdbId = null;
            if (item.ProviderIds != null)
            {
                item.ProviderIds.TryGetValue("Tmdb", out tmdbId);
                item.ProviderIds.TryGetValue("Imdb", out imdbId);
            }

            if (string.IsNullOrWhiteSpace(imdbId) && !string.IsNullOrWhiteSpace(item.Name))
            {
                var m = _imdbIdInName.Match(item.Name);
                if (m.Success)
                {
                    imdbId = m.Groups[1].Value;
                }
            }

            if (string.IsNullOrWhiteSpace(tmdbId) && string.IsNullOrWhiteSpace(imdbId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    string? posterPath = null;

                    if (!string.IsNullOrWhiteSpace(tmdbId))
                    {
                        var kind = isSeries ? "tv" : "movie";
                        posterPath = await FetchTmdbPosterPathAsync(
                            "https://api.themoviedb.org/3/" + kind + "/" + Uri.EscapeDataString(tmdbId),
                            token!,
                            null).ConfigureAwait(false);
                    }

                    if (posterPath == null && !string.IsNullOrWhiteSpace(imdbId))
                    {
                        // /find returns results grouped by kind, so pick the matching bucket.
                        posterPath = await FetchTmdbPosterPathAsync(
                            "https://api.themoviedb.org/3/find/" + Uri.EscapeDataString(imdbId)
                                + "?external_source=imdb_id",
                            token!,
                            isSeries ? "tv_results" : "movie_results").ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(posterPath))
                    {
                        _externalPosterCache[itemId] = "https://image.tmdb.org/t/p/w185" + posterPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "External poster lookup failed for item {ItemId}", itemId);
                }
            });
        }

        /// <summary>
        /// Requests a TMDB endpoint and pulls poster_path out, optionally from a results array.
        /// </summary>
        /// <param name="url">The TMDB URL.</param>
        /// <param name="token">The TMDB API token.</param>
        /// <param name="resultsProperty">Results array to look inside, or null for a direct object.</param>
        /// <returns>The poster path, or null.</returns>
        private async Task<string?> FetchTmdbPosterPathAsync(string url, string token, string? resultsProperty)
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            request.Headers.TryAddWithoutValidation("accept", "application/json");

            using var resp = await _tmdbHttp.SendAsync(request).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (resultsProperty != null)
            {
                if (!root.TryGetProperty(resultsProperty, out var arr)
                    || arr.ValueKind != System.Text.Json.JsonValueKind.Array
                    || arr.GetArrayLength() == 0)
                {
                    return null;
                }

                root = arr[0];
            }

            if (root.TryGetProperty("poster_path", out var pp)
                && pp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return pp.GetString();
            }

            return null;
        }

        private void QueueTmdbPosterBackfill(UserRating? rating)
        {
            if (rating == null
                || !string.IsNullOrWhiteSpace(rating.PosterUrl)
                || string.IsNullOrWhiteSpace(rating.TmdbId))
            {
                return;
            }

            var token = Plugin.Instance?.Configuration?.TmdbApiToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            if (!_posterBackfillSeen.TryAdd(rating.Id, 0))
            {
                return;
            }

            var ratingId = rating.Id;
            var tmdbId = rating.TmdbId;
            var isSeries = string.Equals(rating.MediaType, "Series", StringComparison.OrdinalIgnoreCase);

            _ = Task.Run(async () =>
            {
                try
                {
                    var kind = isSeries ? "tv" : "movie";
                    var url = "https://api.themoviedb.org/3/" + kind + "/" + Uri.EscapeDataString(tmdbId!);

                    using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                    request.Headers.TryAddWithoutValidation("accept", "application/json");

                    using var resp = await _tmdbHttp.SendAsync(request).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("poster_path", out var pp)
                        && pp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var path = pp.GetString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            _repository.SetRatingPoster(ratingId, "https://image.tmdb.org/t/p/w185" + path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "TMDB poster backfill failed for rating {RatingId}", ratingId);
                }
            });
        }

        /// <summary>
        /// Rates a TMDB title that is not on the server.
        /// </summary>
        /// <remarks>
        /// Ratings previously required a Jellyfin item, so titles added to a profile row from the
        /// TMDB catalog could not be rated at all (issue #72). The rating is filed under a
        /// deterministic id derived from the TMDB id and carries its own title, year and poster, so
        /// it displays properly with nothing else to look up - and is picked up automatically by
        /// the real item if the title is later added to the library.
        /// </remarks>
        /// <param name="request">External rating request.</param>
        /// <returns>The stored rating.</returns>
        [HttpPost("External/Rating")]
        [Authorize]
        public async Task<ActionResult<UserRating>> SetExternalRating([FromBody] [Required] ExternalRatingDto request)
        {
            try
            {
                // Must go through the shared helper, not User.GetUserId() alone: the claim is only
                // populated for some of the ways a client can present a token, so a caller sending
                // a bare X-Emby-Token got 401 "User not authenticated" here while the same token
                // worked on every other rating endpoint (reported in issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var config = Plugin.Instance?.Configuration;
                if (config?.EnableRatings == false)
                {
                    return BadRequest("Ratings are currently disabled");
                }

                if (request == null || string.IsNullOrWhiteSpace(request.TmdbId))
                {
                    return BadRequest("tmdbId is required");
                }

                if (request.Rating < (config?.MinRating ?? 1) || request.Rating > (config?.MaxRating ?? 10))
                {
                    return BadRequest($"Rating must be between {config?.MinRating ?? 1} and {config?.MaxRating ?? 10}");
                }

                var mediaType = string.Equals(request.MediaType, "Series", StringComparison.OrdinalIgnoreCase)
                    ? "Series"
                    : "Movie";

                // If this title IS on the server after all, rate the real item instead - otherwise
                // the user would end up with two separate ratings for the same film.
                var existingItem = FindLibraryItemByTmdbId(request.TmdbId, mediaType);
                if (existingItem != null)
                {
                    return await SetRating(existingItem.Id, request.Rating, request.Review).ConfigureAwait(false);
                }

                var itemId = GetExternalItemId(request.TmdbId, mediaType);
                var snapshot = new RatingSnapshot
                {
                    Title = SanitizeInput(request.Title ?? string.Empty, 300),
                    Year = request.Year,
                    MediaType = mediaType,
                    PosterUrl = IsSafePosterUrl(request.PosterUrl) ? request.PosterUrl : null,
                    IsExternal = true
                };

                var sanitizedReview = request.Review != null ? SanitizeInput(request.Review, 2000) : null;

                var result = await _repository.SetRatingAsync(
                    userId, itemId, request.Rating, request.TmdbId, null, null, sanitizedReview, snapshot).ConfigureAwait(false);

                _logger.LogInformation(
                    "User rated external TMDB title '{Title}' (tmdb {TmdbId}) with {Rating}",
                    snapshot.Title, request.TmdbId, request.Rating);

                // No poster from the client (or an unsafe one) - fetch it from TMDB.
                QueueTmdbPosterBackfill(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting external rating");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Only TMDB's own image host is accepted, so a client cannot store an arbitrary URL that
        /// would later be rendered in someone else's profile.
        /// </summary>
        /// <param name="url">Candidate poster URL.</param>
        /// <returns>True when the URL is a TMDB image URL.</returns>
        private static bool IsSafePosterUrl(string? url)
        {
            return !string.IsNullOrWhiteSpace(url)
                && url.StartsWith("https://image.tmdb.org/t/p/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Finds a library item carrying the given TMDB id, so an external rating collapses onto
        /// the real item when the title is actually on the server.
        /// </summary>
        /// <param name="tmdbId">TMDB id.</param>
        /// <param name="mediaType">"Movie" or "Series".</param>
        /// <returns>The item, or null.</returns>
        private MediaBrowser.Controller.Entities.BaseItem? FindLibraryItemByTmdbId(string tmdbId, string mediaType)
        {
            try
            {
                var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                {
                    IncludeItemTypes = mediaType == "Series"
                        ? new[] { Jellyfin.Data.Enums.BaseItemKind.Series }
                        : new[] { Jellyfin.Data.Enums.BaseItemKind.Movie },
                    Recursive = true,
                    HasAnyProviderId = new Dictionary<string, string> { { "Tmdb", tmdbId } },
                    Limit = 1
                };

                return _libraryManager.GetItemList(query).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not check whether TMDB id {TmdbId} is in the library", tmdbId);
                return null;
            }
        }

        /// <summary>
        /// Searches the external TMDB catalog for the profile "Add a film" box. Uses the
        /// server-side configured TMDB token (never exposed to the browser). Returns an empty
        /// "configured: false" payload when no token is set.
        /// </summary>
        /// <param name="q">Search query (title).</param>
        /// <returns>Normalized external results.</returns>
        [HttpGet("ExternalSearch")]
        [Authorize]
        public async Task<ActionResult> ExternalSearch([FromQuery] string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return Ok(new { configured = true, results = new List<object>() });
                }

                var token = Plugin.Instance?.Configuration?.TmdbApiToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Ok(new { configured = false, results = new List<object>() });
                }

                // Language was hardcoded to en-US, so a Spanish user searching "La mejor oferta" got
                // back "The Best Offer". TMDB falls back to the original title when it has no
                // translation, so an unusual setting degrades rather than returning nothing.
                var language = Plugin.Instance?.Configuration?.TmdbLanguage;
                if (string.IsNullOrWhiteSpace(language))
                {
                    language = "en-US";
                }

                var url = "https://api.themoviedb.org/3/search/multi?include_adult=false&language="
                    + Uri.EscapeDataString(language)
                    + "&page=1&query=" + Uri.EscapeDataString(q);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                request.Headers.TryAddWithoutValidation("accept", "application/json");

                using var resp = await _tmdbHttp.SendAsync(request).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    return Ok(new { configured = true, results = new List<object>(), error = (int)resp.StatusCode });
                }

                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var results = new List<object>();
                if (doc.RootElement.TryGetProperty("results", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var mediaType = el.TryGetProperty("media_type", out var mt) ? mt.GetString() : null;
                        if (mediaType != "movie" && mediaType != "tv")
                        {
                            continue;
                        }

                        var title = el.TryGetProperty("title", out var t1) ? t1.GetString()
                                  : (el.TryGetProperty("name", out var t2) ? t2.GetString() : null);
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            continue;
                        }

                        var date = el.TryGetProperty("release_date", out var d1) ? d1.GetString()
                                 : (el.TryGetProperty("first_air_date", out var d2) ? d2.GetString() : null);
                        int? year = null;
                        if (!string.IsNullOrEmpty(date) && date.Length >= 4 && int.TryParse(date.Substring(0, 4), out var y))
                        {
                            year = y;
                        }

                        string? poster = null;
                        if (el.TryGetProperty("poster_path", out var pp) && pp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var pv = pp.GetString();
                            if (!string.IsNullOrEmpty(pv))
                            {
                                poster = "https://image.tmdb.org/t/p/w185" + pv;
                            }
                        }

                        double rating = 0;
                        if (el.TryGetProperty("vote_average", out var va) && va.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            rating = Math.Round(va.GetDouble(), 1);
                        }

                        int tmdbId = el.TryGetProperty("id", out var idv) && idv.ValueKind == System.Text.Json.JsonValueKind.Number ? idv.GetInt32() : 0;

                        string? overview = el.TryGetProperty("overview", out var ov) && ov.ValueKind == System.Text.Json.JsonValueKind.String
                            ? ov.GetString()
                            : null;

                        results.Add(new
                        {
                            tmdbId,
                            mediaType = mediaType == "tv" ? "Series" : "Movie",
                            title,
                            year,
                            poster,
                            rating,
                            overview
                        });

                        if (results.Count >= 8)
                        {
                            break;
                        }
                    }
                }

                return Ok(new { configured = true, results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External (TMDB) search failed");
                return StatusCode(500, "External search failed");
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
                // Resolve the authenticated user (handles X-Emby-Token, X-Emby-Authorization, Authorization)
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Resolve the user so library queries can expand GROUPED views. When several
                // folders are merged into one ("Group automatically"), the parentId in the URL is a
                // virtual UserView, not a real folder - Jellyfin can only resolve it into its
                // underlying folders when the query carries the user. Without User, a grouped view
                // returns zero items and the sorted grid shows no cards.
                var sortUser = _userManager.GetUserById(userId);

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
                        User = sortUser,
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
                    // For non-rating sorts (imdb, release, added), show ALL items from library
                    // Get rating stats for display purposes only (not filtering)
                    var allRatings = _repository.GetAllItemRatingStats();

                    // Query ALL items from the library (not just rated ones)
                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series, Jellyfin.Data.Enums.BaseItemKind.MusicVideo },
                        Recursive = true
                    };

                    // Filter by library if parentId was provided. User is required so a GROUPED
                    // view (merged folders) expands into its underlying folders instead of
                    // resolving to nothing.
                    // The user goes on the query ALWAYS, not only when a library was picked: it is
                    // what makes Jellyfin apply this account's library permissions, so without it
                    // the unfiltered view listed titles from libraries the account cannot browse.
                    // (It is also what expands a grouped view into its underlying folders.)
                    query.User = sortUser;
                    if (parentGuid.HasValue)
                    {
                        query.ParentId = parentGuid.Value;
                    }

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

                _logger.LogInformation(
                    "SortedLibrary sortBy={SortBy} dir={Direction} user={UserId} page={Page} totalCount={TotalCount} returnedOnPage={Returned}",
                    sortBy, direction, userId, page, totalCount, sortedItems.Count);

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
                // Shared helper - the old fallback here read only X-Emby-Authorization, so the
                // Remove button (which sends a plain X-Emby-Token) got 401 and the UI could only
                // report "Could not remove that rating". Same fault as issue #72.
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var deleted = await _repository.DeleteRatingAsync(userId, itemId).ConfigureAwait(false);
                if (!deleted)
                {
                    return NotFound("No rating found to delete");
                }

                // Clear/recompute the native rating fields to match.
                var deletedItem = _libraryManager.GetItemById(itemId);
                if (deletedItem != null)
                {
                    ClearNativeRating(userId, deletedItem);
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
        /// Mirrors a just-saved rating into Jellyfin's native fields so external tools (e.g. via the
        /// Jellyfin API) can read it. Best-effort - never throws back into the rating flow.
        /// </summary>
        private void WriteNativeRating(Guid userId, MediaBrowser.Controller.Entities.BaseItem item, int rating)
        {
            var config = Plugin.Instance?.Configuration;

            // A) Native per-user rating (UserData.Rating). Non-destructive; survives metadata refreshes.
            if (config?.WriteRatingsToJellyfin != false)
            {
                try
                {
                    var user = _userManager.GetUserById(userId);
                    if (user != null)
                    {
                        var userData = _userDataManager.GetUserData(user, item);
                        if (userData != null)
                        {
                            userData.Rating = rating;
                            _userDataManager.SaveUserData(user, item, userData, MediaBrowser.Model.Entities.UserDataSaveReason.UpdateUserRating, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to write native per-user rating for {ItemId}", item.Id);
                }
            }

            // B) Item CommunityRating = our average (opt-in; overwrites the external score and is
            //    reverted when Jellyfin next refreshes the item's metadata).
            if (config?.WriteAverageToCommunityRating == true)
            {
                try
                {
                    var stats = _repository.GetRatingStats(item.Id);
                    if (stats.TotalRatings > 0)
                    {
                        item.CommunityRating = (float)Math.Round(stats.AverageRating, 1);
                        _ = _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to write CommunityRating for {ItemId}", item.Id);
                }
            }
        }

        /// <summary>
        /// Clears/recomputes the native rating fields after a user removes their rating. Best-effort.
        /// </summary>
        private void ClearNativeRating(Guid userId, MediaBrowser.Controller.Entities.BaseItem item)
        {
            var config = Plugin.Instance?.Configuration;

            if (config?.WriteRatingsToJellyfin != false)
            {
                try
                {
                    var user = _userManager.GetUserById(userId);
                    if (user != null)
                    {
                        var userData = _userDataManager.GetUserData(user, item);
                        if (userData != null)
                        {
                            userData.Rating = null;
                            _userDataManager.SaveUserData(user, item, userData, MediaBrowser.Model.Entities.UserDataSaveReason.UpdateUserRating, CancellationToken.None);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to clear native per-user rating for {ItemId}", item.Id);
                }
            }

            if (config?.WriteAverageToCommunityRating == true)
            {
                try
                {
                    var stats = _repository.GetRatingStats(item.Id);
                    if (stats.TotalRatings > 0)
                    {
                        item.CommunityRating = (float)Math.Round(stats.AverageRating, 1);
                        _ = _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to recompute CommunityRating for {ItemId}", item.Id);
                }
            }
        }

        /// <summary>
        /// One-time backfill: writes every existing stored rating into Jellyfin's native fields
        /// (per-user rating, plus CommunityRating averages when that option is enabled), so existing
        /// ratings become readable through the Jellyfin API. Admin only.
        /// </summary>
        /// <returns>A summary of what was written.</returns>
        [HttpPost("Admin/BackfillNativeRatings")]
        [Authorize]
        public async Task<ActionResult> BackfillNativeRatings()
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(adminId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
                }

                var config = Plugin.Instance?.Configuration;
                var writeUser = config?.WriteRatingsToJellyfin != false;
                var writeCommunity = config?.WriteAverageToCommunityRating == true;

                var allRatings = _repository.GetAllRatings();
                int userRatingsWritten = 0, errors = 0;
                var distinctItems = new HashSet<Guid>();

                foreach (var r in allRatings)
                {
                    try
                    {
                        var item = _libraryManager.GetItemById(r.ItemId);
                        if (item == null)
                        {
                            continue;
                        }

                        distinctItems.Add(r.ItemId);

                        if (writeUser)
                        {
                            var user = _userManager.GetUserById(r.UserId);
                            if (user != null)
                            {
                                var userData = _userDataManager.GetUserData(user, item);
                                if (userData != null)
                                {
                                    userData.Rating = r.Rating;
                                    _userDataManager.SaveUserData(user, item, userData, MediaBrowser.Model.Entities.UserDataSaveReason.UpdateUserRating, CancellationToken.None);
                                    userRatingsWritten++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        _logger.LogDebug(ex, "Backfill: failed for item {ItemId}", r.ItemId);
                    }
                }

                int communityWritten = 0;
                if (writeCommunity)
                {
                    foreach (var itemId in distinctItems)
                    {
                        try
                        {
                            var item = _libraryManager.GetItemById(itemId);
                            if (item == null)
                            {
                                continue;
                            }

                            var stats = _repository.GetRatingStats(itemId);
                            if (stats.TotalRatings > 0)
                            {
                                item.CommunityRating = (float)Math.Round(stats.AverageRating, 1);
                                await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
                                communityWritten++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            _logger.LogDebug(ex, "Backfill: CommunityRating failed for {ItemId}", itemId);
                        }
                    }
                }

                _logger.LogInformation(
                    "Backfill native ratings complete: userRatings={UR} community={CR} items={Items} errors={E}",
                    userRatingsWritten, communityWritten, distinctItems.Count, errors);

                return Ok(new
                {
                    totalRatings = allRatings.Count,
                    items = distinctItems.Count,
                    userRatingsWritten,
                    communityWritten,
                    errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during native ratings backfill");
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
                    var commentCount = _repository.GetReviewCommentCount(r.UserId, itemId);

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
                        UserLiked = userLike,
                        CommentCount = commentCount
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
                // A plain star rating can be agreed or disagreed with as well, so only the rating
                // itself has to exist - review text is no longer required.
                var rating = _repository.GetUserRating(reviewerUserId, itemId);
                if (rating == null)
                {
                    return NotFound("Rating not found");
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
        /// Gets comments for a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID.</param>
        /// <returns>List of comments.</returns>
        [HttpGet("Reviews/{reviewerUserId}/{itemId}/Comments")]
        [Authorize]
        public ActionResult<List<object>> GetReviewComments(
            [FromRoute] [Required] Guid reviewerUserId,
            [FromRoute] [Required] Guid itemId)
        {
            try
            {
                var comments = _repository.GetReviewComments(reviewerUserId, itemId);
                var result = comments.Select(c =>
                {
                    var user = _userManager.GetUserById(c.CommenterId);
                    return new
                    {
                        Id = c.Id,
                        CommenterId = c.CommenterId,
                        CommenterName = user?.Username ?? "Unknown",
                        Text = c.Text,
                        CreatedAt = c.CreatedAt
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review comments");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Adds a comment to a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="text">Comment text.</param>
        /// <returns>The created comment.</returns>
        [HttpPost("Reviews/{reviewerUserId}/{itemId}/Comments")]
        [Authorize]
        public async Task<ActionResult> AddReviewComment(
            [FromRoute] [Required] Guid reviewerUserId,
            [FromRoute] [Required] Guid itemId,
            [FromQuery] [Required] string text)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Sanitize comment text
                var sanitizedText = SanitizeInput(text, 500);
                if (string.IsNullOrWhiteSpace(sanitizedText))
                {
                    return BadRequest("Comment text is required");
                }

                // Verify review exists
                var rating = _repository.GetUserRating(reviewerUserId, itemId);
                if (rating == null || string.IsNullOrWhiteSpace(rating.ReviewText))
                {
                    return NotFound("Review not found");
                }

                var comment = new Models.ReviewComment
                {
                    ReviewerUserId = reviewerUserId,
                    ItemId = itemId,
                    CommenterId = userId,
                    Text = sanitizedText
                };

                await _repository.AddReviewCommentAsync(comment).ConfigureAwait(false);

                var user = _userManager.GetUserById(userId);
                return Ok(new
                {
                    Id = comment.Id,
                    CommenterId = comment.CommenterId,
                    CommenterName = user?.Username ?? "Unknown",
                    Text = comment.Text,
                    CreatedAt = comment.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding review comment");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a review comment.
        /// </summary>
        /// <param name="commentId">Comment ID to delete.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Reviews/Comments/{commentId}")]
        [Authorize]
        public async Task<ActionResult> DeleteReviewComment(
            [FromRoute] [Required] Guid commentId)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var deleted = await _repository.DeleteReviewCommentAsync(commentId, userId).ConfigureAwait(false);
                if (!deleted)
                {
                    return NotFound("Comment not found or you don't have permission to delete it");
                }

                return Ok(new { Success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review comment");
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
                    ShowCardRatingOverlay = config?.ShowCardRatingOverlay ?? true,
                    EnableNetflixView = config?.EnableNetflixView ?? false,
                    EnableRequestButton = config?.EnableRequestButton ?? true,
                    EnableNewMediaNotifications = config?.EnableNewMediaNotifications ?? true,
                    MinRating = config?.MinRating ?? 1,
                    MaxRating = config?.MaxRating ?? 10,

                    // Support queues - the client hides the profile tab and the report button
                    // entirely when these are off, rather than letting them 404 on submit.
                    EnableQualityRequests = config?.EnableQualityRequests ?? true,
                    EnableBugReports = config?.EnableBugReports ?? true,
                    BugReportMaxAttachments = config?.BugReportMaxAttachments ?? 3,
                    BugReportMaxAttachmentMb = config?.BugReportMaxAttachmentMb ?? 2,

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
                    LanguageTextColor = config?.LanguageTextColor ?? "#ffffff",

                    // Review card styling
                    ReviewCardBackground = config?.ReviewCardBackground ?? "rgba(30, 30, 30, 0.95)",
                    ReviewCardNoBorder = config?.ReviewCardNoBorder ?? false,
                    ReviewCardBorderColor = config?.ReviewCardBorderColor ?? "rgba(255, 255, 255, 0.1)",
                    ReviewCardBorderRadius = config?.ReviewCardBorderRadius ?? 12,
                    ReviewCardUsernameColor = config?.ReviewCardUsernameColor ?? "#ffffff",
                    ReviewCardTimestampColor = config?.ReviewCardTimestampColor ?? "#888888",
                    ReviewCardTextColor = config?.ReviewCardTextColor ?? "#cccccc",
                    ReviewCardRatingColor = config?.ReviewCardRatingColor ?? "#ffd700",
                    ReviewCardActionBtnColor = config?.ReviewCardActionBtnColor ?? "#888888",
                    ReviewCardActionBtnHoverColor = config?.ReviewCardActionBtnHoverColor ?? "#ffffff",
                    ReviewCardLikedColor = config?.ReviewCardLikedColor ?? "#4CAF50",
                    ReviewCardDislikedColor = config?.ReviewCardDislikedColor ?? "#f44336",
                    ReviewCardHoverBackground = config?.ReviewCardHoverBackground ?? "rgba(255, 255, 255, 0.05)",
                    ReviewCardOverallOpacity = config?.ReviewCardOverallOpacity ?? 100,
                    ShowReviewProfileTooltip = config?.ShowReviewProfileTooltip ?? true
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
        /// <summary>
        /// Items the plugin has actually announced as new, newest first.
        /// </summary>
        /// <remarks>
        /// The Latest Media list used to be built purely from a Jellyfin query sorted by
        /// DateCreated, which is taken from the file rather than from when the item reached the
        /// library. Media copied in with its original timestamps therefore sorted to wherever that
        /// old date fell - often past the end of the list - so the plugin would announce something
        /// as new that never appeared in Latest Media. That is the reported bug.
        ///
        /// This returns what the plugin itself recorded when it announced each item, stamped with
        /// the time it was seen, so the notification and the list cannot disagree. Items that have
        /// since been removed from the library are dropped.
        /// </remarks>
        /// <param name="limit">Maximum items to return.</param>
        /// <param name="days">How far back to look.</param>
        /// <returns>Recently announced media.</returns>
        [HttpGet("LatestMedia")]
        [Authorize]
        public async Task<ActionResult<object>> GetLatestAnnouncedMedia([FromQuery] int limit = 30, [FromQuery] int days = 30)
        {
            try
            {
                limit = Math.Clamp(limit, 1, 100);
                days = Math.Clamp(days, 1, 365);

                var since = DateTime.UtcNow.AddDays(-days);

                // One record per item - a series that gained several episodes is announced more
                // than once, and the list wants the newest entry for it.
                var newest = _repository.GetNotificationsSince(since)
                    .Where(n => !n.IsTest && n.ItemId != Guid.Empty)
                    .GroupBy(n => n.ItemId)
                    .Select(g => g.OrderByDescending(n => n.CreatedAt).First())
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(limit * 2)
                    .ToList();

                if (newest.Count == 0)
                {
                    return Ok(new { items = Array.Empty<object>() });
                }

                // Resolve them in one query so anything already deleted is filtered out and the
                // caller gets a current image tag.
                var itemMap = new Dictionary<Guid, MediaBrowser.Controller.Entities.BaseItem>();
                try
                {
                    var ids = newest.Select(n => n.ItemId).Distinct().ToArray();
                    foreach (var item in _libraryManager.GetItemList(
                        // Scoped to the caller so announcements cannot reveal titles from
                        // libraries this account is not permitted to browse.
                        new MediaBrowser.Controller.Entities.InternalItemsQuery
                        {
                            ItemIds = ids,
                            User = _userManager.GetUserById(
                                await GetAuthenticatedUserIdAsync().ConfigureAwait(false)),
                        }))
                    {
                        itemMap[item.Id] = item;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not resolve announced items");
                }

                var results = new List<object>();
                foreach (var n in newest)
                {
                    if (!itemMap.TryGetValue(n.ItemId, out var item))
                    {
                        // Announced but no longer in the library - do not offer a dead link.
                        continue;
                    }

                    // An episode announcement points at the episode; the list shows series, so
                    // surface the series it belongs to.
                    var displayItem = item is MediaBrowser.Controller.Entities.TV.Episode ep && ep.SeriesId != Guid.Empty
                        ? (itemMap.TryGetValue(ep.SeriesId, out var s) ? s : item)
                        : item;

                    results.Add(new
                    {
                        itemId = displayItem.Id,
                        name = displayItem.Name,
                        type = displayItem.GetType().Name,
                        year = displayItem.ProductionYear,
                        seriesName = n.SeriesName,

                        // When the PLUGIN saw it, which is the honest "added" time.
                        addedAt = n.CreatedAt,

                        // What Jellyfin thinks, kept so a client can see the discrepancy.
                        dateCreated = item.DateCreated
                    });

                    if (results.Count >= limit)
                    {
                        break;
                    }
                }

                return Ok(new { items = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building latest announced media");
                return StatusCode(500, "Internal server error");
            }
        }

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can send test notifications");
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

        // Static assets (ratings.js, ratings.css, i18n/<lang>.js) are read out of the assembly
        // once per process and cached as UTF-8 BYTES. They used to be cached as strings and
        // returned through Content(), which re-encoded the whole payload to bytes on every single
        // request - about a megabyte of allocation per cache miss for the script alone.
        //
        // Each injected URL carries ?v=<pluginVersion>. When it matches we serve immutable, so the
        // browser keeps the asset for a year with no re-download and no revalidation round-trip; a
        // plugin update changes the URL and the new copy is fetched automatically.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte[]> _assetCache = new(StringComparer.Ordinal);

        private static readonly string AssetVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1";

        /// <summary>
        /// Loads an embedded resource into the byte cache, returning an empty array if absent.
        /// </summary>
        /// <param name="cacheKey">Cache key.</param>
        /// <param name="resourceName">Fully qualified embedded resource name.</param>
        /// <returns>The resource bytes, or an empty array.</returns>
        private static byte[] LoadAsset(string cacheKey, string resourceName)
        {
            return _assetCache.GetOrAdd(cacheKey, _ =>
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return Array.Empty<byte>();
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            });
        }

        /// <summary>
        /// Picks the best pre-compressed encoding the client accepts.
        /// </summary>
        /// <remarks>
        /// Deliberately simple: it looks for the coding as a whole token and ignores q-values, but
        /// it never selects an encoding the client did not list, and "identity" is always a legal
        /// answer. Real browsers all send "gzip, deflate, br".
        /// </remarks>
        /// <param name="acceptEncoding">Raw Accept-Encoding header value.</param>
        /// <returns>"br", "gzip" or null for identity.</returns>
        private static string? NegotiateEncoding(string? acceptEncoding)
        {
            if (string.IsNullOrEmpty(acceptEncoding))
            {
                return null;
            }

            var accepted = acceptEncoding.Split(',');
            var wantsBrotli = false;
            var wantsGzip = false;

            foreach (var raw in accepted)
            {
                var token = raw;

                // Strip any q-value, then trim.
                var semi = token.IndexOf(';', StringComparison.Ordinal);
                if (semi >= 0)
                {
                    // "br;q=0" means explicitly refused.
                    if (token.AsSpan(semi).IndexOf("q=0".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0
                        && token.AsSpan(semi).IndexOf("q=0.".AsSpan(), StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    token = token.Substring(0, semi);
                }

                token = token.Trim();

                if (token.Equals("br", StringComparison.OrdinalIgnoreCase))
                {
                    wantsBrotli = true;
                }
                else if (token.Equals("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    wantsGzip = true;
                }
            }

            if (wantsBrotli)
            {
                return "br";
            }

            return wantsGzip ? "gzip" : null;
        }

        /// <summary>
        /// Serves an embedded web asset with ETag / immutable caching and pre-compressed encodings.
        /// </summary>
        /// <remarks>
        /// The .br/.gz variants are produced at build time (see the CompressAssets target). Serving
        /// our own Content-Encoding matters because Jellyfin registers response compression with
        /// ResponseCompressionOptions.EnableForHttps left at its default of false, so clients
        /// reaching the server over direct HTTPS otherwise receive these assets completely
        /// uncompressed - and a plugin cannot change that setting. Setting Content-Encoding
        /// ourselves also stops the framework's compression middleware from re-compressing.
        /// If the pre-compressed resources are missing (built without Node) this transparently
        /// falls back to the raw bytes.
        /// </remarks>
        /// <param name="resourceName">Fully qualified embedded resource name.</param>
        /// <param name="contentType">MIME type to serve it as.</param>
        /// <param name="cacheKey">Stable key for the in-process byte cache.</param>
        /// <returns>The asset, or 304 / 404.</returns>
        private ActionResult ServeEmbeddedAsset(string resourceName, string contentType, string cacheKey)
        {
            var bytes = LoadAsset(cacheKey, resourceName);
            if (bytes.Length == 0)
            {
                _logger.LogError("Embedded resource {ResourceName} not found in assembly", resourceName);
                return NotFound();
            }

            // Caches keyed on this URL must not mix encodings.
            Response.Headers["Vary"] = "Accept-Encoding";

            var encoding = NegotiateEncoding(Request.Headers.AcceptEncoding.ToString());
            if (encoding != null)
            {
                var suffix = encoding == "br" ? ".br" : ".gz";
                var encoded = LoadAsset(cacheKey + suffix, resourceName + suffix);
                if (encoded.Length > 0)
                {
                    bytes = encoded;
                    Response.Headers["Content-Encoding"] = encoding;
                }
                else
                {
                    encoding = null;
                }
            }

            // The ETag has to include the encoding, otherwise a cache holding the brotli copy could
            // answer an identity request with it (and vice versa).
            var etag = "\"" + cacheKey + "-" + AssetVersion + (encoding == null ? string.Empty : "-" + encoding) + "\"";
            Response.Headers["ETag"] = etag;

            var requestedVersion = Request.Query["v"].ToString();
            Response.Headers["Cache-Control"] =
                !string.IsNullOrEmpty(requestedVersion) && requestedVersion == AssetVersion
                    ? "public, max-age=31536000, immutable"
                    : "no-cache";

            var ifNoneMatch = Request.Headers["If-None-Match"].ToString();
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
            {
                return StatusCode(304);
            }

            return File(bytes, contentType);
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
                return ServeEmbeddedAsset("Jellyfin.Plugin.Ratings.Web.ratings.js", "application/javascript", "ratings.js");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve ratings.js");
                return Content("// ERROR: Failed to load ratings.js", "application/javascript");
            }
        }

        /// <summary>
        /// Serves the plugin stylesheet.
        /// </summary>
        /// <remarks>
        /// This CSS used to be a ~374 KB template literal inside ratings.js, which esbuild could
        /// not minify and which the browser had to parse as a JS string before it could build the
        /// CSSOM. As a real stylesheet it is minified at build time and the preload scanner can
        /// fetch it in parallel with the script.
        /// </remarks>
        /// <returns>The stylesheet.</returns>
        [HttpGet("ratings.css")]
        [AllowAnonymous]
        public ActionResult GetRatingsStylesheet()
        {
            try
            {
                return ServeEmbeddedAsset("Jellyfin.Plugin.Ratings.Web.ratings.css", "text/css", "ratings.css");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve ratings.css");
                return Content("/* ERROR: Failed to load ratings.css */", "text/css");
            }
        }

        /// <summary>
        /// Serves an on-demand language pack.
        /// </summary>
        /// <remarks>
        /// All 16 languages used to be bundled into ratings.js (~232 KB) so that every user
        /// downloaded 15 languages they would never see. Only English is inline now.
        /// </remarks>
        /// <param name="lang">Two-letter language code.</param>
        /// <returns>The language pack script.</returns>
        [HttpGet("i18n/{lang}.js")]
        [AllowAnonymous]
        public ActionResult GetLanguagePack([FromRoute] [Required] string lang)
        {
            try
            {
                // Whitelist - this value goes into a resource name, so it must never be
                // attacker-controlled free text.
                if (!SupportedLanguagePacks.Contains(lang, StringComparer.Ordinal))
                {
                    return NotFound();
                }

                return ServeEmbeddedAsset(
                    "Jellyfin.Plugin.Ratings.Web.i18n." + lang + ".js",
                    "application/javascript",
                    "i18n." + lang);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve language pack {Lang}", lang);
                return NotFound();
            }
        }

        // English is bundled inline in ratings.js and is deliberately not served here.
        private static readonly string[] SupportedLanguagePacks =
        {
            "es", "zh", "pt", "ru", "ja", "de", "fr", "ko", "it", "tr", "pl", "nl", "ar", "hi", "lt"
        };

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
                // Resolved through the shared helper so every way a client can present a token
                // works here, not just the ones that populate the claim (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                // If standard auth didn't work, try to get from session token
                // The helper above already covers every token form, so the fallback that used to
                // follow here was dead code.

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
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
                var normalizedImdbLink = string.Empty;
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

                    // Keep the PARSED form. Uri.TryCreate accepts embedded quotes and spaces and
                    // still reports the allow-listed host, so passing the check does not make the
                    // original text safe - and it is later rendered into an href an administrator
                    // sees. AbsoluteUri percent-encodes those characters.
                    normalizedImdbLink = imdbUri.AbsoluteUri;
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
                    ImdbLink = normalizedImdbLink,
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
                // Resolved through the shared helper so every way a client can present a token
                // works here, not just the ones that populate the claim (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can view all requests");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can update request status");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
                    return StatusCode(StatusCodes.Status403Forbidden, "You can only delete your own requests");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
                    return StatusCode(StatusCodes.Status403Forbidden, "You can only edit your own requests");
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
                    NormalizeExternalLink(request.ImdbLink)).ConfigureAwait(false);

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can snooze requests");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can unsnooze requests");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can access media management");
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

                // Apply search filter. Jellyfin's SearchTerm is used first because it is indexed
                // and understands more than the name alone; if it finds nothing, fall back to a
                // punctuation-insensitive match so "xfiles" and "x files" still find "The X-Files".
                if (!string.IsNullOrEmpty(search))
                {
                    query.SearchTerm = search;

                    var probe = _libraryManager.GetItemsResult(new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        IncludeItemTypes = query.IncludeItemTypes,
                        ParentId = query.ParentId,
                        Recursive = true,
                        SearchTerm = search,
                        Limit = 1,
                    });

                    if (probe.TotalRecordCount == 0)
                    {
                        var ids = FindByNormalizedName(search, 500);
                        if (ids.Count > 0)
                        {
                            query.SearchTerm = null;
                            query.ItemIds = ids.ToArray();
                        }
                    }
                }

                var sortField = sortBy.ToLowerInvariant();
                var ascending = sortOrder.Equals("asc", StringComparison.OrdinalIgnoreCase);

                // Title / year / dateAdded can be sorted and paged by Jellyfin's own query layer.
                // For those we ask the database for just this page instead of materialising the
                // entire library and paging in memory - on a large library that was tens of
                // thousands of items (plus a repository lock each) to render 50 rows.
                // rating / playcount / size are computed by this plugin and are not expressible in
                // InternalItemsQuery, so they still need the whole set before they can be ordered.
                var nativeSort = sortField switch
                {
                    "title" => (Jellyfin.Data.Enums.ItemSortBy?)Jellyfin.Data.Enums.ItemSortBy.SortName,
                    "year" => Jellyfin.Data.Enums.ItemSortBy.ProductionYear,
                    "dateadded" => Jellyfin.Data.Enums.ItemSortBy.DateCreated,
                    _ => null
                };

                IReadOnlyList<MediaBrowser.Controller.Entities.BaseItem> allItems;
                int totalItems;
                var pagedInQuery = false;

                if (nativeSort.HasValue)
                {
                    query.OrderBy = new[]
                    {
                        (nativeSort.Value, ascending ? Jellyfin.Database.Implementations.Enums.SortOrder.Ascending : Jellyfin.Database.Implementations.Enums.SortOrder.Descending)
                    };
                    query.StartIndex = (page - 1) * pageSize;
                    query.Limit = pageSize;
                    query.EnableTotalRecordCount = true;

                    var result = _libraryManager.GetItemsResult(query);
                    allItems = result.Items;
                    totalItems = result.TotalRecordCount;
                    pagedInQuery = true;
                }
                else
                {
                    allItems = _libraryManager.GetItemList(query);
                    totalItems = allItems.Count;
                }

                // Get scheduled deletions for badge info
                var scheduledDeletions = _repository.GetAllScheduledDeletions()
                    .ToDictionary(d => d.ItemId);

                // Rating stats for the whole working set in ONE lock acquisition rather than one
                // per item.
                var ratingStatsById = _repository.GetBatchRatingStats(
                    allItems.Select(i => (i.Id, (string?)null, (string?)null, (string?)null)).ToList());

                // STEP 1: Build basic stats quickly (no expensive episode queries)
                var mediaStats = allItems.Select(item =>
                {
                    ratingStatsById.TryGetValue(item.Id.ToString("N"), out var ratingStats);

                    // Build image URL
                    string? imageUrl = null;
                    if (item.ImageInfos != null && item.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary))
                    {
                        imageUrl = $"/Items/{item.Id}/Images/Primary";
                    }
                    else if (_externalPosterCache.TryGetValue(item.Id, out var externalPoster))
                    {
                        // Resolved earlier from TMDB - an absolute URL, which the client
                        // detects and uses as-is instead of prefixing the server address.
                        imageUrl = externalPoster;
                    }
                    else
                    {
                        // Items Jellyfin has not identified have no artwork at all, so the row
                        // drew a grey box. Look one up in the background; it lands in the cache
                        // for the next page load rather than blocking this response.
                        QueueExternalPosterLookup(item);
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
                        AverageRating = ratingStats != null && ratingStats.TotalRatings > 0 ? ratingStats.AverageRating : null,
                        RatingCount = ratingStats?.TotalRatings ?? 0,
                        DateAdded = item.DateCreated,
                        ScheduledDeletion = deletion
                    };
                }).ToList();

                // When sorting by playcount or size, calculate those stats for ALL items first
                if (sortField == "playcount" || sortField == "size")
                {
                    // Index by id - this used to be allItems.FirstOrDefault(...) inside the loop,
                    // which made the pass O(n^2) over the entire library.
                    var itemsById = new Dictionary<Guid, MediaBrowser.Controller.Entities.BaseItem>();
                    foreach (var i in allItems)
                    {
                        itemsById[i.Id] = i;
                    }

                    foreach (var stat in mediaStats)
                    {
                        if (!itemsById.TryGetValue(stat.ItemId, out var item))
                        {
                            continue;
                        }

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

                // Apply sorting. When the query layer already sorted and paged for us, mediaStats
                // is this page in the right order and re-sorting it would be wrong (it would
                // reorder within the page only).
                if (!pagedInQuery)
                {
                    mediaStats = sortField switch
                    {
                        "title" => ascending
                            ? mediaStats.OrderBy(m => m.Title).ToList()
                            : mediaStats.OrderByDescending(m => m.Title).ToList(),
                        "year" => ascending
                            ? mediaStats.OrderBy(m => m.Year ?? 0).ToList()
                            : mediaStats.OrderByDescending(m => m.Year ?? 0).ToList(),
                        "playcount" => ascending
                            ? mediaStats.OrderBy(m => m.PlayCount).ToList()
                            : mediaStats.OrderByDescending(m => m.PlayCount).ToList(),
                        "watchtime" => ascending
                            ? mediaStats.OrderBy(m => m.TotalWatchTimeMinutes).ToList()
                            : mediaStats.OrderByDescending(m => m.TotalWatchTimeMinutes).ToList(),
                        "size" => ascending
                            ? mediaStats.OrderBy(m => m.FileSizeBytes).ToList()
                            : mediaStats.OrderByDescending(m => m.FileSizeBytes).ToList(),
                        "rating" => ascending
                            ? mediaStats.OrderBy(m => m.AverageRating ?? 0).ToList()
                            : mediaStats.OrderByDescending(m => m.AverageRating ?? 0).ToList(),
                        _ => ascending
                            ? mediaStats.OrderBy(m => m.DateAdded).ToList()
                            : mediaStats.OrderByDescending(m => m.DateAdded).ToList()
                    };
                }

                // Apply pagination (already applied by the query on the native-sort path)
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                var pagedItems = pagedInQuery
                    ? mediaStats
                    : mediaStats.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                // Index once for the enrichment pass below - this was another
                // allItems.FirstOrDefault(...) inside a loop.
                var pagedItemsById = new Dictionary<Guid, MediaBrowser.Controller.Entities.BaseItem>();
                foreach (var i in allItems)
                {
                    pagedItemsById[i.Id] = i;
                }

                // STEP 2: Calculate expensive stats only for paginated items
                foreach (var stat in pagedItems)
                {
                    if (!pagedItemsById.TryGetValue(stat.ItemId, out var item))
                    {
                        continue;
                    }

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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can schedule deletions");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can cancel scheduled deletions");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

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
                    // These reach an administrator's screen, so they get the same treatment the
                    // media-request path already applies. MediaLink additionally goes into an
                    // href, so only a parsed absolute http(s) URL is kept - anything else is
                    // dropped rather than stored as typed.
                    Title = SanitizeInput(request.Title, 500),
                    Type = SanitizeInput(request.Type, 100),
                    MediaLink = NormalizeExternalLink(request.MediaLink),
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can view all deletion requests");
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
                // Shared helper: the hand-rolled fallback that used to be here read only
                // X-Emby-Authorization/Authorization, so a client sending a plain X-Emby-Token
                // was rejected with 401 while the same token worked elsewhere (issue #72).
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);

                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can action deletion requests");
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can create bans");
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can view bans");
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Only administrators can lift bans");
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
                // Writes are coalesced, so up to a debounce window's worth of changes may still be
                // in memory. This reads the files straight off disk, so flush first or the backup
                // silently omits the newest data. (Only ratings-repository files are exported.)
                await _repository.FlushPendingWritesAsync().ConfigureAwait(false);

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
                // Drain any debounced write BEFORE overwriting the files. A pending write still
                // holds a pre-import snapshot, and if it fired after the import it would put the
                // old data straight back on disk.
                await _repository.FlushPendingWritesAsync().ConfigureAwait(false);

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

            // Try X-Emby-Token header first (simple token), then X-Emby-Authorization/Authorization with Token="..." format
            var token = Request.Headers["X-Emby-Token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                              ?? Request.Headers["Authorization"].FirstOrDefault();

                if (!string.IsNullOrEmpty(authHeader))
                {
                    var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                    if (tokenMatch.Success)
                    {
                        token = tokenMatch.Groups[1].Value;
                    }
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                if (session != null)
                {
                    return session.UserId;
                }
            }

            return Guid.Empty;
        }

        #region Admin - Orphaned trickplay data

        /// <summary>
        /// Resolves the folder Jellyfin keeps trickplay tiles in, by asking Jellyfin rather than
        /// assembling a path ourselves: the per-item directory's parent IS the root.
        /// </summary>
        /// <returns>The trickplay root, or null if it cannot be determined.</returns>
        private string? GetTrickplayRoot()
        {
            if (_pathManager == null)
            {
                return null;
            }

            var probe = _libraryManager.GetItemList(new MediaBrowser.Controller.Entities.InternalItemsQuery
            {
                IncludeItemTypes = new[]
                {
                    Jellyfin.Data.Enums.BaseItemKind.Movie,
                    Jellyfin.Data.Enums.BaseItemKind.Episode,
                },
                Recursive = true,
                Limit = 1,
            }).FirstOrDefault();

            if (probe == null)
            {
                return null;
            }

            var itemDir = _pathManager.GetTrickplayDirectory(probe, false);
            if (string.IsNullOrEmpty(itemDir))
            {
                return null;
            }

            var parent = Path.GetDirectoryName(itemDir);
            if (string.IsNullOrEmpty(parent))
            {
                return null;
            }

            // Jellyfin shards this directory by the first two characters of the item id, so the
            // item's parent is a shard like ".../trickplay/1b", not the root. Climbing one more
            // level only when the parent really looks like a shard keeps this correct for both
            // layouts, rather than assuming either.
            var parentName = Path.GetFileName(parent);
            if (parentName.Length == 2 && parentName.All(Uri.IsHexDigit))
            {
                return Path.GetDirectoryName(parent) ?? parent;
            }

            return parent;
        }

        /// <summary>
        /// Total size of a directory tree, ignoring anything unreadable.
        /// </summary>
        /// <param name="path">Directory to measure.</param>
        /// <returns>Size in bytes.</returns>
        private static long DirectorySize(string path)
        {
            long total = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // skip files that vanish or cannot be read
                    }
                }
            }
            catch
            {
                // unreadable directory: report what we have
            }

            return total;
        }

        /// <summary>
        /// Finds trickplay folders whose media is no longer in the library.
        ///
        /// Jellyfin names each folder after the item id, so a folder whose id no longer resolves
        /// belongs to media that has been removed - the tiles were left behind. Folders whose name
        /// is not an item id are ignored entirely rather than guessed at.
        /// </summary>
        /// <returns>The orphaned folders with their sizes.</returns>
        private List<(Guid Id, string Path, long Size)> ScanOrphanedTrickplay()
        {
            return ScanOrphanedTrickplay(out _, out _);
        }

        /// <summary>
        /// As <see cref="ScanOrphanedTrickplay()"/>, also reporting what was examined so the UI can
        /// show evidence the scan ran rather than an unverifiable "nothing found".
        /// </summary>
        /// <param name="scanned">Folders examined.</param>
        /// <param name="skipped">Folders whose name is not an item id, so not ours to judge.</param>
        /// <returns>The orphaned folders with their sizes.</returns>
        private List<(Guid Id, string Path, long Size)> ScanOrphanedTrickplay(out int scanned, out int skipped)
        {
            var found = new List<(Guid, string, long)>();
            scanned = 0;
            skipped = 0;

            var root = GetTrickplayRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return found;
            }

            // Item folders sit one level down inside a two-character shard ("1b/<itemid>"), so both
            // levels are walked. A flat layout still works: a directory named like an item id is
            // treated as an item wherever it is found.
            foreach (var entry in Directory.EnumerateDirectories(root))
            {
                var entryName = Path.GetFileName(entry);

                if (Guid.TryParse(entryName, out var flatId))
                {
                    scanned++;
                    if (_libraryManager.GetItemById(flatId) == null)
                    {
                        found.Add((flatId, entry, DirectorySize(entry)));
                    }

                    continue;
                }

                if (entryName.Length != 2 || !entryName.All(Uri.IsHexDigit))
                {
                    skipped++;
                    continue;
                }

                foreach (var dir in Directory.EnumerateDirectories(entry))
                {
                    scanned++;
                    if (!Guid.TryParse(Path.GetFileName(dir), out var itemId))
                    {
                        skipped++;
                        continue;
                    }

                    if (_libraryManager.GetItemById(itemId) != null)
                    {
                        continue;
                    }

                    found.Add((itemId, dir, DirectorySize(dir)));
                }
            }

            return found;
        }

        /// <summary>
        /// Finds "&lt;video&gt;.trickplay" folders sitting beside media that no longer exists.
        ///
        /// With "save trickplay images next to media" the tiles never reach the central directory -
        /// they live in a sibling folder named after the video file. Deleting the video leaves that
        /// folder behind, and nothing in the central scan can see it.
        ///
        /// The test needs no library lookup: the folder is orphaned when the video file it is named
        /// after is gone. That stays correct even for media Jellyfin never indexed.
        /// </summary>
        /// <param name="scanned">Sibling folders examined.</param>
        /// <returns>The orphaned folders with their sizes.</returns>
        private List<(string Path, long Size)> ScanOrphanedSiblingTrickplay(out int scanned)
        {
            var found = new List<(string, long)>();
            scanned = 0;

            List<string> roots;
            try
            {
                roots = _libraryManager.GetVirtualFolders()
                    .SelectMany(v => v.Locations ?? Array.Empty<string>())
                    .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not enumerate library folders for sibling trickplay scan");
                return found;
            }

            foreach (var root in roots)
            {
                IEnumerable<string> dirs;
                try
                {
                    dirs = Directory.EnumerateDirectories(root, "*.trickplay", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not walk library folder {Root}", root);
                    continue;
                }

                foreach (var dir in dirs)
                {
                    scanned++;

                    try
                    {
                        var parent = Path.GetDirectoryName(dir);
                        if (parent == null)
                        {
                            continue;
                        }

                        // "Movie.1080p.trickplay" belongs to "Movie.1080p.<ext>". Compared by name
                        // rather than a glob, so brackets in a title cannot break the match.
                        var baseName = Path.GetFileName(dir);
                        baseName = baseName.Substring(0, baseName.Length - ".trickplay".Length);

                        var mediaStillThere = Directory.EnumerateFiles(parent).Any(f =>
                            string.Equals(Path.GetFileNameWithoutExtension(f), baseName, StringComparison.OrdinalIgnoreCase));

                        if (!mediaStillThere)
                        {
                            found.Add((dir, DirectorySize(dir)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Skipping sibling trickplay folder {Dir}", dir);
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Lists trickplay folders left behind by media that is no longer in the library.
        /// </summary>
        /// <returns>The orphaned folders and how much space they take.</returns>
        [HttpGet("Admin/OrphanedTrickplay")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GetOrphanedTrickplay()
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid();
                }

                var root = GetTrickplayRoot();
                if (string.IsNullOrEmpty(root))
                {
                    return Ok(new
                    {
                        supported = false,
                        reason = "Trickplay location could not be determined on this server.",
                        items = Array.Empty<object>(),
                        totalBytes = 0L,
                    });
                }

                var orphans = ScanOrphanedTrickplay(out var scanned, out var skipped);
                var siblings = ScanOrphanedSiblingTrickplay(out var siblingScanned);

                var items = orphans.Select(o => new
                {
                    itemId = o.Id.ToString("N"),
                    name = Path.GetFileName(o.Path),
                    sizeBytes = o.Size,
                    nextToMedia = false,
                })
                .Concat(siblings.Select(s => new
                {
                    itemId = string.Empty,
                    name = Path.GetFileName(s.Path),
                    sizeBytes = s.Size,
                    nextToMedia = true,
                }))
                .ToList();

                return Ok(new
                {
                    supported = true,
                    root,
                    // Reported so "nothing found" can be told apart from "nothing ran".
                    scanned,
                    skipped,
                    // Libraries set to "save trickplay next to media" keep nothing in the central
                    // directory, so these are counted separately - otherwise a server using that
                    // setting looks like it has no trickplay at all.
                    siblingScanned,
                    siblingCount = siblings.Count,
                    items,
                    totalBytes = orphans.Sum(o => o.Size) + siblings.Sum(s => s.Size),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning for orphaned trickplay data");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes trickplay folders left behind by removed media.
        ///
        /// The caller sends ids, never paths: the server re-scans and deletes only folders it has
        /// itself just identified as orphaned, and each one is confirmed to sit directly under the
        /// trickplay root before removal. A request cannot therefore point the delete anywhere
        /// else on disk.
        /// </summary>
        /// <param name="request">Optional ids to remove; all orphans when omitted.</param>
        /// <returns>How many folders were removed and how much space that freed.</returns>
        [HttpPost("Admin/OrphanedTrickplay/Delete")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> DeleteOrphanedTrickplay([FromBody] OrphanedTrickplayDeleteDto? request = null)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return Forbid();
                }

                var root = GetTrickplayRoot();
                if (string.IsNullOrEmpty(root))
                {
                    return BadRequest("Trickplay location could not be determined on this server.");
                }

                var rootFull = Path.GetFullPath(root);
                var orphans = ScanOrphanedTrickplay();

                HashSet<Guid>? wanted = null;
                if (request?.ItemIds != null && request.ItemIds.Count > 0)
                {
                    wanted = new HashSet<Guid>();
                    foreach (var raw in request.ItemIds)
                    {
                        if (Guid.TryParse(raw, out var parsed))
                        {
                            wanted.Add(parsed);
                        }
                    }
                }

                long freed = 0;
                var removed = 0;

                foreach (var orphan in orphans)
                {
                    if (wanted != null && !wanted.Contains(orphan.Id))
                    {
                        continue;
                    }

                    // Belt and braces: the path came from our own scan, but confirm it really sits
                    // inside the trickplay root before deleting anything. Item folders are one or
                    // two levels down depending on whether the layout is sharded, so this checks
                    // containment rather than a direct-child relationship - with the separator
                    // appended, so a sibling directory whose name merely starts with the root
                    // ("/config/data/trickplay-old") cannot pass.
                    var full = Path.GetFullPath(orphan.Path);
                    var rootPrefix = rootFull.EndsWith(Path.DirectorySeparatorChar)
                        ? rootFull
                        : rootFull + Path.DirectorySeparatorChar;

                    if (!full.StartsWith(rootPrefix, StringComparison.Ordinal)
                        || full.Contains("..", StringComparison.Ordinal))
                    {
                        _logger.LogWarning("Skipping trickplay path outside the root: {Path}", full);
                        continue;
                    }

                    try
                    {
                        Directory.Delete(full, true);
                        freed += orphan.Size;
                        removed++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete orphaned trickplay folder {Path}", full);
                    }
                }

                // Sibling folders live beside the media, not under the trickplay root, so they get
                // their own containment rule: the path must come from this same fresh scan AND
                // still end in ".trickplay". As with the central ones, the client never supplies
                // a path - it can only ask for all of them.
                if (wanted == null)
                {
                    foreach (var sibling in ScanOrphanedSiblingTrickplay(out _))
                    {
                        var siblingFull = Path.GetFullPath(sibling.Path);
                        if (!siblingFull.EndsWith(".trickplay", StringComparison.OrdinalIgnoreCase)
                            || siblingFull.Contains("..", StringComparison.Ordinal))
                        {
                            _logger.LogWarning("Skipping unexpected sibling trickplay path: {Path}", siblingFull);
                            continue;
                        }

                        try
                        {
                            Directory.Delete(siblingFull, true);
                            freed += sibling.Size;
                            removed++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not delete sibling trickplay folder {Path}", siblingFull);
                        }
                    }
                }

                _logger.LogInformation(
                    "Admin removed {Count} orphaned trickplay folder(s), freeing {Bytes} bytes",
                    removed,
                    freed);

                return Ok(new { removed, freedBytes = freed });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting orphaned trickplay data");
                return StatusCode(500, "Internal server error");
            }
        }

        #endregion

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
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
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
        /// Items that share a series-level IMDB id (episodes, including shows kept in a Movies-type
        /// library where each episode is imported as a separate Movie) are sub-grouped by their
        /// SxxExx marker, so genuinely different episodes are not reported as duplicates of each other.
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
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
                    // A series-level IMDB id (e.g. on a "The Simpsons [tt0096697]" folder) is applied to
                    // EVERY episode file, and when a show sits in a Movies-type library each episode is
                    // imported as a separate Movie - so a raw IMDB grouping reports hundreds of distinct
                    // episodes as "duplicates". Sub-group by the episode marker (SxxExx) parsed from the
                    // file name so only genuine same-content copies are reported: different episodes get
                    // different keys (not duplicates), while real movie copies (no SxxExx) stay grouped.
                    .SelectMany(g => g
                        .GroupBy(i => GetEpisodeKey(i.Path))
                        .Where(sub => sub.Count() > 1)
                        .Select(sub => BuildDuplicateGroup(g.Key, sub.First().Name, sub.First().ProductionYear, sub.ToList(), "Video")))
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

        // Matches a season/episode marker like S31E08, s31.e08, S31 E08 in a file name.
        private static readonly System.Text.RegularExpressions.Regex _episodeMarkerRegex =
            new System.Text.RegularExpressions.Regex(
                @"[Ss](\d{1,2})[ ._-]*[Ee](\d{1,3})",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// Returns a normalized per-episode key (e.g. "S31E8") parsed from a file path, or an empty
        /// string for movies/series with no episode marker. Used so that different episodes sharing a
        /// series-level IMDB id are not reported as duplicates of each other, while real same-content
        /// copies (same episode, or movies with no marker) still group together.
        /// </summary>
        private static string GetEpisodeKey(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var fileName = System.IO.Path.GetFileName(path);
            var match = _episodeMarkerRegex.Match(fileName);
            if (!match.Success)
            {
                return string.Empty;
            }

            return "S" + int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                 + "E" + int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
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
            [FromQuery] bool deleteFile = false,
            [FromQuery] bool deleteFiles = false)
        {
            try
            {
                var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(userId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
                }

                // Accept both "deleteFile" and "deleteFiles" so an older cached frontend cannot
                // silently leave the file on disk (which caused deleted duplicates to reappear
                // after the next library scan).
                deleteFile = deleteFile || deleteFiles;

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

                // Trickplay tiles and extracted subtitles/attachments live outside the media
                // folder, so DeleteItem leaves them behind (issue #70). Clean them up first, while
                // the item's media sources still exist.
                if (deleteFile)
                {
                    MediaDeletionHelper.DeleteExtractedData(_pathManager, item, _logger);
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
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
                    return StatusCode(StatusCodes.Status403Forbidden, "Admin access required");
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

        #region Dashboard Stats

        /// <summary>
        /// Get overall rating statistics for the dashboard.
        /// </summary>
        /// <returns>Overall statistics.</returns>
        [HttpGet("Stats")]
        [Authorize]
        public ActionResult GetOverallStats()
        {
            try
            {
                var allRatings = _repository.GetAllItemRatingStats();
                var totalRatings = allRatings.Values.Sum(r => r.RatingCount);
                var totalUsers = _repository.GetAllUserIds().Count;
                var totalReviews = _repository.GetReviewCount();

                double avgRating = 0;
                if (allRatings.Count > 0)
                {
                    avgRating = allRatings.Values.Average(r => r.AverageRating);
                }

                return Ok(new
                {
                    TotalRatings = totalRatings,
                    TotalUsers = totalUsers,
                    TotalReviews = totalReviews,
                    AverageRating = Math.Round(avgRating, 1),
                    TotalItems = allRatings.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting overall stats");
                return Ok(new { TotalRatings = 0, TotalUsers = 0, TotalReviews = 0, AverageRating = 0.0, TotalItems = 0 });
            }
        }

        /// <summary>
        /// Get unified recent activity feed (ratings, reviews, requests, comments).
        /// </summary>
        /// <param name="limit">Maximum number of items to return.</param>
        /// <returns>Recent activity list.</returns>
        [HttpGet("RecentActivity")]
        [Authorize]
        public ActionResult GetRecentActivity([FromQuery] int limit = 20)
        {
            try
            {
                limit = Math.Clamp(limit, 1, 100);
                var activities = new List<ActivityItem>();

                // Get recent ratings (includes reviews)
                var recentRatings = _repository.GetRecentRatings(limit);
                foreach (var rating in recentRatings)
                {
                    var item = _libraryManager.GetItemById(rating.ItemId);
                    var user = _userManager.GetUserById(rating.UserId);
                    var hasReview = !string.IsNullOrWhiteSpace(rating.ReviewText);

                    activities.Add(new ActivityItem
                    {
                        Type = hasReview ? "review" : "rating",
                        UserId = rating.UserId.ToString("N"),
                        UserName = user?.Username ?? "Unknown",
                        ItemId = rating.ItemId.ToString("N"),
                        ItemName = item?.Name ?? "Unknown",
                        Rating = rating.Rating,
                        ReviewPreview = hasReview ? (rating.ReviewText!.Length > 80 ? rating.ReviewText.Substring(0, 80) + "..." : rating.ReviewText) : null,
                        Timestamp = rating.UpdatedAt
                    });
                }

                // Get recent media requests
                var recentRequests = _repository.GetRecentMediaRequests(limit);
                foreach (var request in recentRequests)
                {
                    var user = _userManager.GetUserById(request.UserId);

                    activities.Add(new ActivityItem
                    {
                        Type = "request",
                        UserId = request.UserId.ToString("N"),
                        UserName = user?.Username ?? "Unknown",
                        ItemName = request.Title,
                        RequestType = request.Type,
                        RequestStatus = request.Status,
                        Timestamp = request.CreatedAt
                    });
                }

                // Get recent review comments
                var recentComments = _repository.GetRecentReviewComments(limit);
                foreach (var comment in recentComments)
                {
                    var commenter = _userManager.GetUserById(comment.CommenterId);
                    var reviewer = _userManager.GetUserById(comment.ReviewerUserId);
                    var item = _libraryManager.GetItemById(comment.ItemId);

                    activities.Add(new ActivityItem
                    {
                        Type = "comment",
                        UserId = comment.CommenterId.ToString("N"),
                        UserName = commenter?.Username ?? "Unknown",
                        ItemId = comment.ItemId.ToString("N"),
                        ItemName = item?.Name ?? "Unknown",
                        TargetUserName = reviewer?.Username ?? "Unknown",
                        CommentPreview = comment.Text.Length > 80 ? comment.Text.Substring(0, 80) + "..." : comment.Text,
                        Timestamp = comment.CreatedAt
                    });
                }

                // Sort all activities by timestamp and take limit
                var result = activities
                    .OrderByDescending(a => a.Timestamp)
                    .Take(limit)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activity");
                return Ok(new List<object>());
            }
        }

        private class ActivityItem
        {
            public string Type { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string? ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public int? Rating { get; set; }
            public string? ReviewPreview { get; set; }
            public string? RequestType { get; set; }
            public string? RequestStatus { get; set; }
            public string? TargetUserName { get; set; }
            public string? CommentPreview { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Get top rated items.
        /// </summary>
        /// <param name="limit">Maximum number of items to return.</param>
        /// <returns>Top rated items list.</returns>
        [HttpGet("TopRated")]
        [Authorize]
        public ActionResult GetTopRatedItems([FromQuery] int limit = 10)
        {
            try
            {
                limit = Math.Clamp(limit, 1, 50);
                var allRatings = _repository.GetAllItemRatingStats();

                var topItems = allRatings
                    .Where(r => r.Value.RatingCount >= 1)
                    .OrderByDescending(r => r.Value.AverageRating)
                    .ThenByDescending(r => r.Value.RatingCount)
                    .Take(limit)
                    .ToList();

                var result = new List<object>();
                foreach (var item in topItems)
                {
                    var mediaItem = _libraryManager.GetItemById(item.Key);
                    if (mediaItem == null) continue;

                    var hasImage = mediaItem.ImageInfos?.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary) == true;

                    result.Add(new
                    {
                        Id = item.Key.ToString("N"),
                        Name = mediaItem.Name,
                        Year = mediaItem.ProductionYear,
                        AverageRating = item.Value.AverageRating,
                        TotalRatings = item.Value.RatingCount,
                        ImageUrl = hasImage ? $"/Items/{item.Key}/Images/Primary" : null
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top rated items");
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Get most active users by rating count.
        /// </summary>
        /// <param name="limit">Maximum number of users to return.</param>
        /// <returns>Most active users list.</returns>
        [HttpGet("MostActiveUsers")]
        [Authorize]
        public ActionResult GetMostActiveUsers([FromQuery] int limit = 5)
        {
            try
            {
                limit = Math.Clamp(limit, 1, 20);
                var recentRatings = _repository.GetRecentRatings(500);

                var userStats = recentRatings
                    .GroupBy(r => r.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        RatingCount = g.Count(),
                        ReviewCount = g.Count(r => !string.IsNullOrWhiteSpace(r.ReviewText)),
                        AverageRating = g.Average(r => r.Rating),
                        LastActive = g.Max(r => r.UpdatedAt)
                    })
                    .OrderByDescending(u => u.RatingCount)
                    .Take(limit)
                    .ToList();

                var result = userStats.Select(u =>
                {
                    var user = _userManager.GetUserById(u.UserId);
                    return new
                    {
                        UserId = u.UserId.ToString("N"),
                        UserName = user?.Username ?? "Unknown",
                        RatingCount = u.RatingCount,
                        ReviewCount = u.ReviewCount,
                        AverageRating = Math.Round(u.AverageRating, 1),
                        LastActive = u.LastActive
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting most active users");
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Get rating distribution (count of each rating value).
        /// </summary>
        /// <returns>Rating distribution data.</returns>
        [HttpGet("RatingDistribution")]
        [Authorize]
        public ActionResult GetRatingDistribution()
        {
            try
            {
                var recentRatings = _repository.GetRecentRatings(1000);

                var distribution = Enumerable.Range(1, 10)
                    .Select(rating => new
                    {
                        Rating = rating,
                        Count = recentRatings.Count(r => r.Rating == rating)
                    })
                    .ToList();

                return Ok(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating distribution");
                return Ok(Enumerable.Range(1, 10).Select(r => new { Rating = r, Count = 0 }));
            }
        }

        /// <summary>
        /// Get recent media requests for dashboard.
        /// </summary>
        /// <param name="limit">Maximum number of requests to return.</param>
        /// <returns>Recent requests list.</returns>
        [HttpGet("RecentRequests")]
        [Authorize]
        public async Task<ActionResult> GetRecentRequests([FromQuery] int limit = 5)
        {
            try
            {
                // Returns every user's request title and requesting username, and backs the admin
                // dashboard - so it needs the same gate as GET Requests, which is admin-only.
                var callerId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (!IsAdminRequest(callerId))
                {
                    return Forbid();
                }

                limit = Math.Clamp(limit, 1, 20);
                var requests = _repository.GetRecentMediaRequests(limit);

                var result = requests.Select(r =>
                {
                    var user = _userManager.GetUserById(r.UserId);
                    return new
                    {
                        Id = r.Id.ToString("N"),
                        Title = r.Title,
                        Type = r.Type,
                        Status = r.Status,
                        UserName = user?.Username ?? "Unknown",
                        CreatedAt = r.CreatedAt
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent requests");
                return Ok(new List<object>());
            }
        }

        /// <summary>
        /// Get items filtered by specific rating value.
        /// </summary>
        /// <param name="rating">Rating value to filter by (1-10).</param>
        /// <param name="limit">Maximum number of items to return.</param>
        /// <returns>Items with the specified rating.</returns>
        [HttpGet("ItemsByRating")]
        [Authorize]
        public ActionResult GetItemsByRating([FromQuery] int rating, [FromQuery] int limit = 50)
        {
            try
            {
                rating = Math.Clamp(rating, 1, 10);
                limit = Math.Clamp(limit, 1, 100);

                var recentRatings = _repository.GetRecentRatings(1000);
                var itemsWithRating = recentRatings
                    .Where(r => r.Rating == rating)
                    .GroupBy(r => r.ItemId)
                    .Select(g => g.First())
                    .Take(limit)
                    .ToList();

                var result = new List<object>();
                foreach (var r in itemsWithRating)
                {
                    var mediaItem = _libraryManager.GetItemById(r.ItemId);
                    if (mediaItem == null) continue;

                    var hasImage = mediaItem.ImageInfos?.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary) == true;

                    result.Add(new
                    {
                        Id = r.ItemId.ToString("N"),
                        Name = mediaItem.Name,
                        Year = mediaItem.ProductionYear,
                        ImageUrl = hasImage ? $"/Items/{r.ItemId}/Images/Primary" : null
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting items by rating");
                return Ok(new List<object>());
            }
        }

        #endregion

        // Quality requests and bug reports
        //
        // Two support queues that sit alongside media requests. A media request asks for
        // something the server does not have; a quality request points at an item that is already
        // there, and a bug report is about the plugin itself. They are kept separate because they
        // are resolved by different work and close on different conditions.

        /// <summary>
        /// The image formats a screenshot may be in, keyed by the bytes a real file of that type
        /// starts with. The upload's declared content type is not trusted - only the bytes are.
        /// </summary>
        private static readonly (string ContentType, string Extension, byte[] Magic)[] AllowedImageTypes =
        {
            ("image/jpeg", ".jpg", new byte[] { 0xFF, 0xD8, 0xFF }),
            ("image/png", ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ("image/gif", ".gif", new byte[] { 0x47, 0x49, 0x46, 0x38 }),
        };

        /// <summary>
        /// Identifies an image by its leading bytes, so a renamed executable cannot be stored as a
        /// screenshot. WebP is matched separately because its marker is not at offset zero.
        /// </summary>
        /// <param name="bytes">The uploaded bytes.</param>
        /// <returns>The content type and extension, or null when this is not an allowed image.</returns>
        private static (string ContentType, string Extension)? IdentifyImage(byte[] bytes)
        {
            foreach (var (contentType, extension, magic) in AllowedImageTypes)
            {
                if (bytes.Length >= magic.Length && bytes.AsSpan(0, magic.Length).SequenceEqual(magic))
                {
                    return (contentType, extension);
                }
            }

            // RIFF....WEBP
            if (bytes.Length >= 12
                && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
                && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return ("image/webp", ".webp");
            }

            return null;
        }

        /// <summary>
        /// Shapes a quality request for the client, hiding nothing but keeping the payload small.
        /// </summary>
        private static object ShapeQualityRequest(QualityRequest r) => new
        {
            id = r.Id,
            userId = r.UserId,
            username = r.Username,
            itemId = r.ItemId,
            itemName = r.ItemName,
            year = r.Year,
            itemType = r.ItemType,
            comment = r.Comment,
            currentQuality = r.CurrentQuality,
            status = r.Status,
            adminResponse = r.AdminResponse,
            createdAt = r.CreatedAt,
            updatedAt = r.UpdatedAt,
            resolvedAt = r.ResolvedAt,
            resolvedBy = r.ResolvedBy,
        };

        /// <summary>
        /// Shapes a bug report for the client. Attachments are described, never inlined - they are
        /// fetched one at a time through the attachment endpoint.
        /// </summary>
        private static object ShapeBugReport(BugReport r) => new
        {
            id = r.Id,
            userId = r.UserId,
            username = r.Username,
            comment = r.Comment,
            context = r.Context,
            status = r.Status,
            adminResponse = r.AdminResponse,
            createdAt = r.CreatedAt,
            updatedAt = r.UpdatedAt,
            resolvedAt = r.ResolvedAt,
            resolvedBy = r.ResolvedBy,
            attachments = r.Attachments.Select(a => new
            {
                id = a.Id,
                fileName = a.FileName,
                contentType = a.ContentType,
                sizeBytes = a.SizeBytes,
            }).ToList(),
        };

        /// <summary>
        /// Raises a request to improve the quality of an item already in the library.
        /// </summary>
        /// <param name="dto">The item and what is wrong with it.</param>
        /// <returns>The stored request.</returns>
        [HttpPost("QualityRequests")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> CreateQualityRequest([FromBody] [Required] QualityRequestDto dto)
        {
            var config = Plugin.Instance?.Configuration;
            if (config != null && !config.EnableQualityRequests)
            {
                return NotFound(new { error = "Quality requests are disabled" });
            }

            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            var comment = SanitizeInput(dto.Comment, 1000);
            if (string.IsNullOrWhiteSpace(comment))
            {
                return BadRequest(new { error = "A comment is required" });
            }

            var item = dto.ItemId == Guid.Empty ? null : _libraryManager.GetItemById(dto.ItemId);
            if (item == null)
            {
                return BadRequest(new { error = "Item not found" });
            }

            // The point of this queue is items the server already has; without the check a client
            // could file quality complaints about anything at all.
            if (_repository.HasOpenQualityRequest(userId, dto.ItemId))
            {
                return BadRequest(new { error = "You already have an open request for this title" });
            }

            var request = new QualityRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Username = user.Username,
                ItemId = item.Id,
                ItemName = item.Name ?? string.Empty,
                Year = item.ProductionYear,
                ItemType = item.GetType().Name,
                Comment = comment,
                CurrentQuality = SanitizeInput(dto.CurrentQuality, 120),
                Status = "open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _repository.AddQualityRequest(request);
            _logger.LogInformation("Quality request raised by {User} for {Item}", user.Username, item.Name);

            return Ok(ShapeQualityRequest(request));
        }

        /// <summary>
        /// Lists quality requests. Administrators see every request; everyone else sees their own.
        /// </summary>
        /// <returns>The visible quality requests.</returns>
        [HttpGet("QualityRequests")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GetQualityRequests()
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty && !IsAdminRequest(userId))
            {
                return Unauthorized("User not authenticated");
            }

            var isAdmin = IsAdminRequest(userId);
            var list = isAdmin ? _repository.GetAllQualityRequests() : _repository.GetUserQualityRequests(userId);

            return Ok(new
            {
                isAdmin,
                requests = list.Select(ShapeQualityRequest).ToList(),
            });
        }

        /// <summary>
        /// Updates a quality request's status and optionally replies to its author.
        /// </summary>
        /// <param name="id">The request id.</param>
        /// <param name="dto">New status and optional reply.</param>
        /// <returns>The updated request.</returns>
        [HttpPost("QualityRequests/{id}/Status")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> UpdateQualityRequestStatus([FromRoute] Guid id, [FromBody] [Required] SupportStatusDto dto)
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (!IsAdminRequest(userId))
            {
                return Forbid();
            }

            if (!IsValidSupportStatus(dto.Status))
            {
                return BadRequest(new { error = "Unknown status" });
            }

            var actor = userId == Guid.Empty ? "API key" : (_userManager.GetUserById(userId)?.Username ?? string.Empty);
            var response = dto.Response == null ? null : SanitizeInput(dto.Response, 1000);
            var updated = _repository.UpdateQualityRequestStatus(id, dto.Status, response, actor);

            return updated == null ? NotFound(new { error = "Request not found" }) : Ok(ShapeQualityRequest(updated));
        }

        /// <summary>
        /// Deletes a quality request. Administrators may delete any; a user may withdraw their own.
        /// </summary>
        /// <param name="id">The request id.</param>
        /// <returns>Success.</returns>
        [HttpDelete("QualityRequests/{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> DeleteQualityRequest([FromRoute] Guid id)
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            var existing = _repository.GetQualityRequest(id);
            if (existing == null)
            {
                return NotFound(new { error = "Request not found" });
            }

            if (!IsAdminRequest(userId) && existing.UserId != userId)
            {
                return Forbid();
            }

            _repository.DeleteQualityRequest(id);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Raises a bug report, optionally with screenshots.
        /// </summary>
        /// <param name="comment">What went wrong.</param>
        /// <param name="context">Where the user was when it happened.</param>
        /// <returns>The stored report.</returns>
        [HttpPost("BugReports")]
        [Authorize]
        [RequestSizeLimit(20 * 1024 * 1024)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> CreateBugReport([FromForm] string? comment, [FromForm] string? context)
        {
            var config = Plugin.Instance?.Configuration;
            if (config != null && !config.EnableBugReports)
            {
                return NotFound(new { error = "Bug reports are disabled" });
            }

            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty)
            {
                return Unauthorized("User not authenticated");
            }

            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                return Unauthorized("User not found");
            }

            var text = SanitizeInput(comment, 2000);
            if (string.IsNullOrWhiteSpace(text))
            {
                return BadRequest(new { error = "A description is required" });
            }

            var maxFiles = Math.Clamp(config?.BugReportMaxAttachments ?? 3, 0, 10);
            var maxBytes = Math.Clamp(config?.BugReportMaxAttachmentMb ?? 2, 1, 20) * 1024 * 1024;

            var report = new BugReport
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Username = user.Username,
                Comment = text,
                Context = SanitizeInput(context, 300),
                Status = "open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            var files = Request.HasFormContentType ? Request.Form.Files : null;
            if (files != null && files.Count > 0)
            {
                if (files.Count > maxFiles)
                {
                    return BadRequest(new { error = $"At most {maxFiles} screenshots" });
                }

                foreach (var file in files)
                {
                    if (file.Length <= 0)
                    {
                        continue;
                    }

                    if (file.Length > maxBytes)
                    {
                        return BadRequest(new { error = $"Each screenshot must be under {maxBytes / (1024 * 1024)} MB" });
                    }

                    byte[] bytes;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms).ConfigureAwait(false);
                        bytes = ms.ToArray();
                    }

                    var kind = IdentifyImage(bytes);
                    if (kind == null)
                    {
                        return BadRequest(new { error = "Attachments must be JPEG, PNG, GIF or WebP images" });
                    }

                    var attachment = new BugReportAttachment
                    {
                        Id = Guid.NewGuid(),
                        FileName = SanitizeInput(Path.GetFileName(file.FileName), 120),
                        ContentType = kind.Value.ContentType,
                        Extension = kind.Value.Extension,
                        SizeBytes = bytes.LongLength,
                    };

                    await _repository.SaveBugReportAttachmentAsync(report.Id, attachment, bytes).ConfigureAwait(false);
                    report.Attachments.Add(attachment);
                }
            }

            _repository.AddBugReport(report);
            _logger.LogInformation("Bug report raised by {User} with {Count} attachment(s)", user.Username, report.Attachments.Count);

            return Ok(ShapeBugReport(report));
        }

        /// <summary>
        /// Lists bug reports. Administrators see every report; everyone else sees their own.
        /// </summary>
        /// <returns>The visible bug reports.</returns>
        [HttpGet("BugReports")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GetBugReports()
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty && !IsAdminRequest(userId))
            {
                return Unauthorized("User not authenticated");
            }

            var isAdmin = IsAdminRequest(userId);
            var list = isAdmin ? _repository.GetAllBugReports() : _repository.GetUserBugReports(userId);

            return Ok(new
            {
                isAdmin,
                reports = list.Select(ShapeBugReport).ToList(),
            });
        }

        /// <summary>
        /// Serves one screenshot from a bug report.
        /// </summary>
        /// <remarks>
        /// Readable by the reporter and by administrators only. The file is located from the
        /// stored record rather than from anything in the URL, so the ids in the path can only
        /// ever select a file the plugin itself wrote.
        /// </remarks>
        /// <param name="id">The report id.</param>
        /// <param name="attachmentId">The attachment id.</param>
        /// <returns>The image.</returns>
        [HttpGet("BugReports/{id}/Attachments/{attachmentId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetBugReportAttachment([FromRoute] Guid id, [FromRoute] Guid attachmentId)
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            var report = _repository.GetBugReport(id);
            if (report == null)
            {
                return NotFound();
            }

            if (!IsAdminRequest(userId) && report.UserId != userId)
            {
                return Forbid();
            }

            var attachment = report.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            if (attachment == null)
            {
                return NotFound();
            }

            var path = _repository.BugReportAttachmentPath(id, attachment);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, attachment.ContentType);
        }

        /// <summary>
        /// Updates a bug report's status and optionally replies to its author.
        /// </summary>
        /// <param name="id">The report id.</param>
        /// <param name="dto">New status and optional reply.</param>
        /// <returns>The updated report.</returns>
        [HttpPost("BugReports/{id}/Status")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> UpdateBugReportStatus([FromRoute] Guid id, [FromBody] [Required] SupportStatusDto dto)
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (!IsAdminRequest(userId))
            {
                return Forbid();
            }

            if (!IsValidSupportStatus(dto.Status))
            {
                return BadRequest(new { error = "Unknown status" });
            }

            var actor = userId == Guid.Empty ? "API key" : (_userManager.GetUserById(userId)?.Username ?? string.Empty);
            var response = dto.Response == null ? null : SanitizeInput(dto.Response, 1000);
            var updated = _repository.UpdateBugReportStatus(id, dto.Status, response, actor);

            return updated == null ? NotFound(new { error = "Report not found" }) : Ok(ShapeBugReport(updated));
        }

        /// <summary>
        /// Deletes a bug report and its screenshots. Administrators may delete any; a user may
        /// withdraw their own.
        /// </summary>
        /// <param name="id">The report id.</param>
        /// <returns>Success.</returns>
        [HttpDelete("BugReports/{id}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> DeleteBugReport([FromRoute] Guid id)
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            var existing = _repository.GetBugReport(id);
            if (existing == null)
            {
                return NotFound(new { error = "Report not found" });
            }

            if (!IsAdminRequest(userId) && existing.UserId != userId)
            {
                return Forbid();
            }

            _repository.DeleteBugReport(id);
            return Ok(new { success = true });
        }

        /// <summary>
        /// How many support items are waiting, for the header badge and the admin tabs.
        /// </summary>
        /// <returns>Open counts, zero for non-administrators.</returns>
        [HttpGet("Support/Counts")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GetSupportCounts()
        {
            var userId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
            if (!IsAdminRequest(userId))
            {
                // Users get their own answered-but-unseen counts rather than the server's queue.
                return Ok(new
                {
                    isAdmin = false,
                    qualityOpen = _repository.GetUserQualityRequests(userId).Count(r => r.IsOpen),
                    bugsOpen = _repository.GetUserBugReports(userId).Count(r => r.IsOpen),
                });
            }

            return Ok(new
            {
                isAdmin = true,
                qualityOpen = _repository.GetAllQualityRequests().Count(r => r.IsOpen),
                bugsOpen = _repository.GetAllBugReports().Count(r => r.IsOpen),
            });
        }

        /// <summary>
        /// The states a support item may be moved to.
        /// </summary>
        /// <param name="status">Candidate status.</param>
        /// <returns>True when it is one of the four known states.</returns>
        private static bool IsValidSupportStatus(string? status)
            => status == "open" || status == "reviewing" || status == "solved" || status == "rejected";
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
