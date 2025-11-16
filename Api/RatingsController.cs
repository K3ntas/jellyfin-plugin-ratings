using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
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

                var userId = User.GetUserId();
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
