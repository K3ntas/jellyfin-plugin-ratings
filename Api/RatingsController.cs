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
        public RatingsController(
            RatingsRepository repository,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            IUserDataManager userDataManager,
            IApplicationPaths appPaths,
            ILogger<RatingsController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _userDataManager = userDataManager;
            _appPaths = appPaths;
            _logger = logger;
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

            // Strip HTML tags
            var sanitized = Regex.Replace(input, @"<[^>]*>", string.Empty);

            // HTML encode special characters to prevent XSS
            sanitized = WebUtility.HtmlEncode(sanitized);

            // Limit length
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized.Trim();
        }

        /// <summary>
        /// Sets a rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="rating">Rating value (1-10).</param>
        /// <returns>The created or updated rating.</returns>
        [HttpPost("Items/{itemId}/Rating")]
        public async Task<ActionResult<UserRating>> SetRating(
            [FromRoute] [Required] Guid itemId,
            [FromQuery] [Required] [Range(1, 10)] int rating)
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

                var result = await _repository.SetRatingAsync(userId, itemId, rating).ConfigureAwait(false);
                _logger.LogInformation("User {UserId} rated item {ItemId} with {Rating}", userId, itemId, rating);

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

                var stats = _repository.GetRatingStats(itemId, userId != Guid.Empty ? userId : null);

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rating stats for item {ItemId}", itemId);
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

                var ratings = _repository.GetUserRatings(targetUserId);
                _logger.LogInformation("Retrieved {Count} ratings for user {UserId}", ratings.Count, targetUserId);

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
        /// Deletes the current user's rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Items/{itemId}/Rating")]
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

                _logger.LogInformation("User {UserId} deleted rating for item {ItemId}", userId, itemId);
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
        public ActionResult<List<UserRatingDetail>> GetDetailedRatings([FromRoute] [Required] Guid itemId)
        {
            try
            {
                // Verify the item exists
                var item = _libraryManager.GetItemById(itemId);
                if (item == null)
                {
                    return NotFound($"Item {itemId} not found");
                }

                var ratings = _repository.GetItemRatings(itemId);
                var detailedRatings = ratings.Select(r =>
                {
                    var user = _userManager.GetUserById(r.UserId);
                    return new UserRatingDetail
                    {
                        UserId = r.UserId,
                        Username = user?.Username ?? "Unknown User",
                        Rating = r.Rating,
                        CreatedAt = r.CreatedAt
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
                    ShowSearchButton = config?.ShowSearchButton ?? true,
                    ShowNotificationToggle = config?.ShowNotificationToggle ?? true,
                    ShowLatestMediaButton = config?.ShowLatestMediaButton ?? true,

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

                    // Chat settings
                    EnableChat = config?.EnableChat ?? false,
                    // HasGifSupport indicates API key is configured (key is never exposed to client)
                    HasGifSupport = !string.IsNullOrEmpty(config?.KlipyApiKey) || !string.IsNullOrEmpty(config?.TenorApiKey),
                    ChatAllowGifs = config?.ChatAllowGifs ?? true,
                    ChatAllowEmojis = config?.ChatAllowEmojis ?? true,
                    ChatMaxMessageLength = config?.ChatMaxMessageLength ?? 500,
                    ChatRateLimitPerMinute = config?.ChatRateLimitPerMinute ?? 10,
                    ChatNotifyPublic = config?.ChatNotifyPublic ?? true,
                    ChatNotifyPrivate = config?.ChatNotifyPrivate ?? true
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
        [AllowAnonymous]
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
                {
                    return Forbid("Only administrators can send test notifications");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
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
                    return Content($"// ERROR: Resource {resourceName} not found", "application/javascript");
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
                    CustomFields = SanitizeInput(request.CustomFields, 5000),
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
        public async Task<ActionResult<List<MediaRequest>>> GetMediaRequests()
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

                // Check if user exists - admin check will be done on client side
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                if (!IsJellyfinAdmin(userId))
                {
                    return Forbid("Only administrators can view all requests");
                }

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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                // Check if user exists - admin check will be done on client side
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                if (!IsJellyfinAdmin(userId))
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
                    SanitizeInput(request.CustomFields, 5000),
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
                {
                    return Forbid("Only administrators can access media management");
                }

                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

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
                    IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series },
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
                    query.IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series };
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

                        if (sortField == "playcount")
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
                    else if (item is MediaBrowser.Controller.Entities.TV.Series series)
                    {
                        // Series: sum play counts from all episodes
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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
        /// <returns>List of scheduled deletions.</returns>
        [HttpGet("ScheduledDeletions")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ScheduledDeletion>>> GetScheduledDeletions()
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
                    return Ok(new List<ScheduledDeletion>());
                }

                var deletions = _repository.GetAllScheduledDeletions();
                return Ok(deletions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scheduled deletions");
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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

                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(userId))
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
        public async Task<ActionResult<UserBan>> CreateBan(
            [FromQuery] [Required] Guid userId,
            [FromQuery] [Required] string banType,
            [FromQuery] [Required] string duration)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (adminId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(adminId))
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
        public async Task<ActionResult<List<UserBan>>> GetBans([FromQuery] [Required] string banType)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (adminId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(adminId))
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
        public async Task<ActionResult> LiftBan([FromRoute] [Required] Guid banId)
        {
            try
            {
                var adminId = await GetAuthenticatedUserIdAsync().ConfigureAwait(false);
                if (adminId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                if (!IsJellyfinAdmin(adminId))
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
