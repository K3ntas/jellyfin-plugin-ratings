using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// Social features API controller.
    /// </summary>
    [ApiController]
    [Route("Social")]
    [Produces(MediaTypeNames.Application.Json)]
    public class SocialController : ControllerBase
    {
        private readonly SocialRepository _socialRepository;
        private readonly IUserManager _userManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<SocialController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialController"/> class.
        /// </summary>
        /// <param name="socialRepository">Social repository.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="logger">Logger instance.</param>
        public SocialController(
            SocialRepository socialRepository,
            IUserManager userManager,
            ISessionManager sessionManager,
            ILogger<SocialController> logger)
        {
            _socialRepository = socialRepository;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets debug information about the social system.
        /// </summary>
        /// <returns>Debug info.</returns>
        [HttpGet("Debug")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetDebugInfo()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var debugInfo = _socialRepository.GetDebugInfo();
            _logger.LogInformation("[Social] Debug info requested by user {UserId}", userId);

            return Ok(debugInfo);
        }

        /// <summary>
        /// Gets the current user's profile.
        /// </summary>
        /// <returns>The user's profile.</returns>
        [HttpGet("MyProfile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserProfile>> GetMyProfile()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = _userManager.GetUserById(userId.Value);
            if (user == null)
            {
                return Unauthorized();
            }

            var profile = await _socialRepository.GetOrCreateProfileAsync(userId.Value, user.Username);
            _logger.LogDebug("[Social] Profile retrieved for user {Username}", user.Username);

            return Ok(profile);
        }

        /// <summary>
        /// Gets a user's profile by ID.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The user's profile.</returns>
        [HttpGet("Profile/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<UserProfile> GetProfile([FromRoute] [Required] Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var profile = _socialRepository.GetProfile(userId);
            if (profile == null)
            {
                return NotFound(new { error = "Profile not found" });
            }

            // Check privacy settings
            if (profile.Privacy.ProfileVisibility == "Private" && profile.UserId != currentUserId)
            {
                return NotFound(new { error = "Profile is private" });
            }

            if (profile.Privacy.ProfileVisibility == "Friends" && profile.UserId != currentUserId)
            {
                if (!_socialRepository.AreFriends(currentUserId.Value, userId))
                {
                    return NotFound(new { error = "Profile is friends-only" });
                }
            }

            return Ok(profile);
        }

        /// <summary>
        /// Updates the current user's profile.
        /// </summary>
        /// <param name="request">The profile update request.</param>
        /// <returns>The updated profile.</returns>
        [HttpPost("Profile")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserProfile>> UpdateProfile([FromBody] ProfileUpdateRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var user = _userManager.GetUserById(userId.Value);
            if (user == null)
            {
                return Unauthorized();
            }

            var profile = await _socialRepository.GetOrCreateProfileAsync(userId.Value, user.Username);

            // Update allowed fields (sanitize input)
            if (request.Bio != null)
            {
                // Limit bio length and sanitize
                profile.Bio = SanitizeInput(request.Bio, 500);
            }

            if (request.Privacy != null)
            {
                // Validate and apply privacy settings
                if (IsValidPrivacySetting(request.Privacy.ProfileVisibility))
                {
                    profile.Privacy.ProfileVisibility = request.Privacy.ProfileVisibility;
                }
                if (IsValidVisibilitySetting(request.Privacy.ShowOnlineStatus))
                {
                    profile.Privacy.ShowOnlineStatus = request.Privacy.ShowOnlineStatus;
                }
                if (IsValidVisibilitySetting(request.Privacy.ShowWatchedHistory))
                {
                    profile.Privacy.ShowWatchedHistory = request.Privacy.ShowWatchedHistory;
                }
                if (IsValidVisibilitySetting(request.Privacy.ShowFriendsList))
                {
                    profile.Privacy.ShowFriendsList = request.Privacy.ShowFriendsList;
                }
                if (IsValidVisibilitySetting(request.Privacy.ShowCurrentlyWatching))
                {
                    profile.Privacy.ShowCurrentlyWatching = request.Privacy.ShowCurrentlyWatching;
                }
                if (IsValidAllowSetting(request.Privacy.AllowFriendRequests))
                {
                    profile.Privacy.AllowFriendRequests = request.Privacy.AllowFriendRequests;
                }
                if (IsValidVisibilitySetting(request.Privacy.AllowMessages))
                {
                    profile.Privacy.AllowMessages = request.Privacy.AllowMessages;
                }
            }

            var updated = await _socialRepository.SaveProfileAsync(profile);
            _logger.LogInformation("[Social] Profile updated for user {Username}", user.Username);

            return Ok(updated);
        }

        #region Helper Methods

        private Guid? GetCurrentUserId()
        {
            var authHeader = HttpContext.Request.Headers["X-Emby-Token"].ToString();
            if (string.IsNullOrEmpty(authHeader))
            {
                return null;
            }

            var session = _sessionManager.GetSessionByAuthenticationToken(authHeader, null, null).Result;
            return session?.UserId;
        }

        private static string SanitizeInput(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            // Remove HTML tags
            var sanitized = System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);

            // Trim and limit length
            sanitized = sanitized.Trim();
            if (sanitized.Length > maxLength)
            {
                sanitized = sanitized.Substring(0, maxLength);
            }

            return sanitized;
        }

        private static bool IsValidPrivacySetting(string? value)
        {
            return value == "Public" || value == "Friends" || value == "Private";
        }

        private static bool IsValidVisibilitySetting(string? value)
        {
            return value == "Everyone" || value == "Friends" || value == "Nobody";
        }

        private static bool IsValidAllowSetting(string? value)
        {
            return value == "Everyone" || value == "Nobody";
        }

        #endregion
    }

    /// <summary>
    /// Request model for profile updates.
    /// </summary>
    public class ProfileUpdateRequest
    {
        /// <summary>
        /// Gets or sets the bio.
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// Gets or sets the privacy settings.
        /// </summary>
        public UserPrivacySettings? Privacy { get; set; }
    }
}
