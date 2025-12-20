using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
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
        private readonly ILogger<RatingsController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingsController"/> class.
        /// </summary>
        /// <param name="repository">Ratings repository.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger instance.</param>
        public RatingsController(
            RatingsRepository repository,
            IUserManager userManager,
            ILibraryManager libraryManager,
            ISessionManager sessionManager,
            ILogger<RatingsController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _libraryManager = libraryManager;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        /// <summary>
        /// Sets a rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="rating">Rating value (1-10).</param>
        /// <returns>The created or updated rating.</returns>
        [HttpPost("Items/{itemId}/Rating")]
        public ActionResult<UserRating> SetRating(
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

                    _logger.LogInformation("Auth header: {Header}", authHeader);

                    // Extract token from header
                    var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                    if (!tokenMatch.Success)
                    {
                        _logger.LogError("Could not extract token from header");
                        return Unauthorized("Invalid authentication header format");
                    }

                    var token = tokenMatch.Groups[1].Value;
                    _logger.LogInformation("Extracted token: {Token}", token.Substring(0, Math.Min(10, token.Length)) + "...");

                    // Get session by authentication token
                    var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                    var session = sessionTask.Result;
                    if (session == null)
                    {
                        _logger.LogError("No active session found for token");
                        return Unauthorized("Invalid or expired token");
                    }

                    userId = session.UserId;
                    _logger.LogInformation("Found user from session: {UserId}", userId);
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

                var result = _repository.SetRatingAsync(userId, itemId, rating).Result;
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
        public ActionResult<RatingStats> GetRatingStats([FromRoute] [Required] Guid itemId)
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
                        _logger.LogInformation("GetStats - Auth header: {Header}", authHeader);

                        // Extract token from header
                        var tokenMatch = System.Text.RegularExpressions.Regex.Match(authHeader, @"Token=""([^""]+)""");
                        if (tokenMatch.Success)
                        {
                            var token = tokenMatch.Groups[1].Value;
                            _logger.LogInformation("GetStats - Extracted token: {Token}", token.Substring(0, Math.Min(10, token.Length)) + "...");

                            // Get session by authentication token
                            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                            var session = sessionTask.Result;
                            if (session != null)
                            {
                                userId = session.UserId;
                                _logger.LogInformation("GetStats - Found user from session: {UserId}", userId);
                            }
                        }
                    }
                }

                var stats = _repository.GetRatingStats(itemId, userId != Guid.Empty ? userId : null);
                _logger.LogInformation("GetStats - Returning stats for item {ItemId}, userId: {UserId}, UserRating: {UserRating}", itemId, userId, stats.UserRating);

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
        /// Deletes the current user's rating for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Items/{itemId}/Rating")]
        [Authorize]
        public ActionResult DeleteRating([FromRoute] [Required] Guid itemId)
        {
            try
            {
                var userId = User.GetUserId();
                if (userId == Guid.Empty)
                {
                    return Unauthorized("User not authenticated");
                }

                var deleted = _repository.DeleteRatingAsync(userId, itemId).Result;
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
            return Ok(new { message = "Ratings plugin is loaded!", version = "1.0.8.0" });
        }

        /// <summary>
        /// Gets plugin configuration for client-side use.
        /// </summary>
        /// <returns>Plugin configuration settings.</returns>
        [HttpGet("Config")]
        [AllowAnonymous]
        public ActionResult GetConfig()
        {
            try
            {
                var config = Plugin.Instance?.Configuration;
                return Ok(new
                {
                    EnableRatings = config?.EnableRatings ?? true,
                    EnableNetflixView = config?.EnableNetflixView ?? false,
                    EnableRequestButton = config?.EnableRequestButton ?? true,
                    EnableNewMediaNotifications = config?.EnableNewMediaNotifications ?? true,
                    MinRating = config?.MinRating ?? 1,
                    MaxRating = config?.MaxRating ?? 10
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
        public ActionResult<List<Models.NewMediaNotification>> GetNotifications([FromQuery] string? since = null)
        {
            try
            {
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
                _logger.LogInformation("GetNotifications: since={Since}, found={Count} notifications", sinceTime.ToString("o"), notifications.Count);
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
            _logger.LogWarning("TEST NOTIFICATION ENDPOINT CALLED");
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
                            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                            var session = sessionTask.Result;
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

                // Note: Admin check is done client-side - only admins see the test button
                // Server trusts authenticated requests for test notifications
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return Unauthorized("User not found");
                }

                // Try to get a random movie or series from the library
                Models.NewMediaNotification notification;
                try
                {
                    var query = new MediaBrowser.Controller.Entities.InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { Jellyfin.Data.Enums.BaseItemKind.Movie, Jellyfin.Data.Enums.BaseItemKind.Series },
                        Recursive = true,
                        Limit = 100
                    };

                    var items = _libraryManager.GetItemList(query);
                    if (items != null && items.Count > 0)
                    {
                        // Pick a random item
                        var random = new Random();
                        var randomItem = items[random.Next(items.Count)];

                        var isMovie = randomItem is MediaBrowser.Controller.Entities.Movies.Movie;
                        string? imageUrl = null;
                        if (randomItem.ImageInfos != null && randomItem.ImageInfos.Any(i => i.Type == MediaBrowser.Model.Entities.ImageType.Primary))
                        {
                            imageUrl = $"/Items/{randomItem.Id}/Images/Primary";
                        }

                        notification = new Models.NewMediaNotification
                        {
                            Id = Guid.NewGuid(),
                            ItemId = randomItem.Id,
                            Title = randomItem.Name,
                            MediaType = isMovie ? "Movie" : "Series",
                            Year = randomItem.ProductionYear,
                            ImageUrl = imageUrl,
                            CreatedAt = DateTime.UtcNow,
                            IsTest = false, // Show as real notification so it looks authentic
                            Message = null
                        };

                        _logger.LogInformation("Admin {UserId} sent test notification with random media: {Title} ({Year})", userId, randomItem.Name, randomItem.ProductionYear);
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

                // Also send DisplayMessage to all active sessions for native app support
                await SendDisplayMessageToAllSessionsAsync(notification.Title, notification.MediaType, notification.Year).ConfigureAwait(false);

                return Ok(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test notification");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Sends a DisplayMessage to all active sessions for native app support.
        /// </summary>
        private async Task SendDisplayMessageToAllSessionsAsync(string title, string mediaType, int? year)
        {
            try
            {
                _logger.LogWarning("SendDisplayMessageToAllSessionsAsync CALLED for: {Title}", title);
                // Log ALL sessions for debugging
                var allSessions = _sessionManager.Sessions.ToList();
                _logger.LogWarning("TOTAL SESSIONS: {Count}", allSessions.Count);
                foreach (var s in allSessions)
                {
                    _logger.LogWarning("SESSION: Id={Id}, Device={Device}, Client={Client}, IsActive={IsActive}, SupportsRemoteControl={SupportsRemote}, SupportsMediaControl={SupportsMedia}",
                        s.Id, s.DeviceName, s.Client, s.IsActive, s.SupportsRemoteControl, s.SupportsMediaControl);
                }

                // Try sending to ALL active sessions, not just SupportsRemoteControl
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
                _logger.LogWarning("Sessions with WebSocket support: {Count}, without: {Count2}",
                    capableSessions.Count, incapableSessions.Count);

                var yearText = year.HasValue ? $" ({year})" : string.Empty;
                var header = mediaType == "Movie" ? "New Movie Available" : (mediaType == "Series" ? "New Series Available" : "Notification");
                var text = $"{title}{yearText}";

                var command = new MediaBrowser.Model.Session.GeneralCommand
                {
                    Name = MediaBrowser.Model.Session.GeneralCommandType.DisplayMessage
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

                _logger.LogInformation("Attempting to load embedded resource: {ResourceName}", resourceName);

                // List all available resources for debugging
                var allResources = assembly.GetManifestResourceNames();
                _logger.LogInformation("Available resources: {Resources}", string.Join(", ", allResources));

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    _logger.LogError("Resource {ResourceName} not found in assembly", resourceName);
                    return Content($"// ERROR: Resource {resourceName} not found\n// Available resources: {string.Join(", ", allResources)}", "application/javascript");
                }

                using var reader = new System.IO.StreamReader(stream);
                var content = reader.ReadToEnd();

                _logger.LogInformation("Successfully loaded ratings.js, {Length} characters", content.Length);

                return Content(content, "application/javascript");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve ratings.js");
                return Content($"// ERROR: {ex.Message}\n// Stack: {ex.StackTrace}", "application/javascript");
            }
        }

        /// <summary>
        /// Creates a new media request.
        /// </summary>
        /// <param name="request">The media request data.</param>
        /// <returns>The created request.</returns>
        [HttpPost("Requests")]
        public ActionResult<MediaRequest> CreateMediaRequest([FromBody] [Required] MediaRequestDto request)
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
                    var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                    var session = sessionTask.Result;
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

                var mediaRequest = new MediaRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Username = user.Username,
                    Title = request.Title,
                    Type = request.Type,
                    Notes = request.Notes,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                var result = _repository.AddMediaRequestAsync(mediaRequest).Result;
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
        public ActionResult<List<MediaRequest>> GetMediaRequests()
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
                            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                            var session = sessionTask.Result;
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

                // For now, allow all authenticated users to see requests
                // Admin-only enforcement can be added later with proper policy checking

                var requests = _repository.GetAllMediaRequestsAsync().Result;
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
        /// <param name="status">The new status (pending, processing, done).</param>
        /// <param name="mediaLink">Optional media link when marking as done.</param>
        /// <returns>The updated request.</returns>
        [HttpPost("Requests/{requestId}/Status")]
        public ActionResult<MediaRequest> UpdateRequestStatus(
            [FromRoute] [Required] Guid requestId,
            [FromQuery] [Required] string status,
            [FromQuery] string? mediaLink = null)
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
                            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                            var session = sessionTask.Result;
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

                // For now, allow all authenticated users to update requests
                // Admin-only enforcement can be added later with proper policy checking

                // Validate status
                var validStatuses = new[] { "pending", "processing", "done" };
                if (!validStatuses.Contains(status.ToLower()))
                {
                    return BadRequest($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }

                var result = _repository.UpdateMediaRequestStatusAsync(requestId, status.ToLower(), mediaLink).Result;
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
        /// Deletes a media request (admin only).
        /// </summary>
        /// <param name="requestId">The request ID to delete.</param>
        /// <returns>Success or failure.</returns>
        [HttpDelete("Requests/{requestId}")]
        public ActionResult DeleteRequest([FromRoute] [Required] Guid requestId)
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
                            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
                            var session = sessionTask.Result;
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

                var result = _repository.DeleteMediaRequestAsync(requestId).Result;
                if (!result)
                {
                    return NotFound("Request not found");
                }

                _logger.LogInformation("Admin {UserId} deleted request {RequestId}", userId, requestId);

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting request");
                return StatusCode(500, "Internal server error");
            }
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
            return string.IsNullOrEmpty(userId) ? Guid.Empty : Guid.Parse(userId);
        }
    }
}
