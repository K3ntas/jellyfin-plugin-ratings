using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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

        #region Friend Requests

        /// <summary>
        /// Sends a friend request to another user.
        /// </summary>
        /// <param name="targetUserId">The target user's ID.</param>
        /// <returns>The created friend request.</returns>
        [HttpPost("FriendRequest/{targetUserId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<object>> SendFriendRequest([FromRoute] [Required] Guid targetUserId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Validation: Cannot send to self
            if (userId.Value == targetUserId)
            {
                _logger.LogWarning("[Social] User {UserId} tried to send friend request to self", userId);
                return BadRequest(new { success = false, error = "Cannot send friend request to yourself" });
            }

            // Get current user info
            var currentUser = _userManager.GetUserById(userId.Value);
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Get target user info
            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null)
            {
                return BadRequest(new { success = false, error = "User not found" });
            }

            // Validation: Check if already friends
            if (_socialRepository.AreFriends(userId.Value, targetUserId))
            {
                _logger.LogWarning("[Social] User {From} tried to send friend request to {To} but already friends", currentUser.Username, targetUser.Username);
                return BadRequest(new { success = false, error = "Already friends with this user" });
            }

            // Validation: Check if request already pending (either direction)
            if (_socialRepository.HasPendingRequest(userId.Value, targetUserId))
            {
                _logger.LogWarning("[Social] User {From} tried to send duplicate friend request to {To}", currentUser.Username, targetUser.Username);
                return BadRequest(new { success = false, error = "Friend request already sent" });
            }

            if (_socialRepository.HasPendingRequest(targetUserId, userId.Value))
            {
                _logger.LogWarning("[Social] User {From} has pending request from {To}", currentUser.Username, targetUser.Username);
                return BadRequest(new { success = false, error = "This user already sent you a friend request" });
            }

            // Validation: Check if target allows friend requests
            var targetProfile = _socialRepository.GetProfile(targetUserId);
            if (targetProfile != null && targetProfile.Privacy.AllowFriendRequests == "Nobody")
            {
                _logger.LogWarning("[Social] User {From} tried to send friend request to {To} but requests disabled", currentUser.Username, targetUser.Username);
                return BadRequest(new { success = false, error = "This user is not accepting friend requests" });
            }

            // Create the friend request
            var request = new FriendRequest
            {
                FromUserId = userId.Value,
                FromUsername = currentUser.Username,
                ToUserId = targetUserId,
                ToUsername = targetUser.Username
            };

            var created = await _socialRepository.CreateFriendRequestAsync(request);

            _logger.LogInformation("[Social] Friend request sent: {From} -> {To}", currentUser.Username, targetUser.Username);

            return Ok(new
            {
                success = true,
                requestId = created.Id,
                message = $"Friend request sent to {targetUser.Username}"
            });
        }

        /// <summary>
        /// Gets incoming friend requests for the current user.
        /// </summary>
        /// <returns>List of incoming requests.</returns>
        [HttpGet("FriendRequests/Incoming")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetIncomingFriendRequests()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var requests = _socialRepository.GetIncomingRequests(userId.Value);

            return Ok(new { requests });
        }

        /// <summary>
        /// Gets outgoing friend requests for the current user.
        /// </summary>
        /// <returns>List of outgoing requests.</returns>
        [HttpGet("FriendRequests/Outgoing")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetOutgoingFriendRequests()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var requests = _socialRepository.GetOutgoingRequests(userId.Value);

            return Ok(new { requests });
        }

        /// <summary>
        /// Accepts a friend request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>Success status.</returns>
        [HttpPost("FriendRequest/{requestId}/Accept")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> AcceptFriendRequest([FromRoute] [Required] Guid requestId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var request = _socialRepository.GetFriendRequest(requestId);
            if (request == null)
            {
                return NotFound(new { success = false, error = "Friend request not found" });
            }

            // Only the target can accept
            if (request.ToUserId != userId.Value)
            {
                return BadRequest(new { success = false, error = "You cannot accept this request" });
            }

            // Must be pending
            if (request.Status != "pending")
            {
                return BadRequest(new { success = false, error = "Request is no longer pending" });
            }

            // Update request status
            await _socialRepository.UpdateFriendRequestStatusAsync(requestId, "accepted");

            // Create friendship
            await _socialRepository.CreateFriendshipAsync(request.FromUserId, request.ToUserId);

            _logger.LogInformation("[Social] Friend request accepted: {From} <-> {To}", request.FromUsername, request.ToUsername);

            return Ok(new
            {
                success = true,
                message = $"You are now friends with {request.FromUsername}"
            });
        }

        /// <summary>
        /// Rejects a friend request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>Success status.</returns>
        [HttpPost("FriendRequest/{requestId}/Reject")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> RejectFriendRequest([FromRoute] [Required] Guid requestId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var request = _socialRepository.GetFriendRequest(requestId);
            if (request == null)
            {
                return NotFound(new { success = false, error = "Friend request not found" });
            }

            // Only the target can reject
            if (request.ToUserId != userId.Value)
            {
                return BadRequest(new { success = false, error = "You cannot reject this request" });
            }

            // Must be pending
            if (request.Status != "pending")
            {
                return BadRequest(new { success = false, error = "Request is no longer pending" });
            }

            // Update request status
            await _socialRepository.UpdateFriendRequestStatusAsync(requestId, "rejected");

            _logger.LogInformation("[Social] Friend request rejected: {From} -> {To}", request.FromUsername, request.ToUsername);

            return Ok(new
            {
                success = true,
                message = "Friend request rejected"
            });
        }

        /// <summary>
        /// Cancels an outgoing friend request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("FriendRequest/{requestId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> CancelFriendRequest([FromRoute] [Required] Guid requestId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var request = _socialRepository.GetFriendRequest(requestId);
            if (request == null)
            {
                return NotFound(new { success = false, error = "Friend request not found" });
            }

            // Only the sender can cancel
            if (request.FromUserId != userId.Value)
            {
                return BadRequest(new { success = false, error = "You cannot cancel this request" });
            }

            // Must be pending
            if (request.Status != "pending")
            {
                return BadRequest(new { success = false, error = "Request is no longer pending" });
            }

            // Delete request
            await _socialRepository.DeleteFriendRequestAsync(requestId);

            _logger.LogInformation("[Social] Friend request cancelled: {From} -> {To}", request.FromUsername, request.ToUsername);

            return Ok(new
            {
                success = true,
                message = "Friend request cancelled"
            });
        }

        /// <summary>
        /// Gets the current user's friends list.
        /// </summary>
        /// <returns>List of friends.</returns>
        [HttpGet("Friends")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFriends()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var friendIds = _socialRepository.GetFriendIds(userId.Value);
            var friends = new System.Collections.Generic.List<object>();

            foreach (var friendId in friendIds)
            {
                var user = _userManager.GetUserById(friendId);
                var profile = _socialRepository.GetProfile(friendId);
                var friendship = _socialRepository.GetFriendships(userId.Value)
                    .FirstOrDefault(f => f.UserId1 == friendId || f.UserId2 == friendId);

                friends.Add(new
                {
                    userId = friendId,
                    username = user?.Username ?? "Unknown",
                    avatarUrl = profile?.AvatarUrl ?? string.Empty,
                    friendsSince = friendship?.CreatedAt
                });
            }

            return Ok(new
            {
                friends,
                totalCount = friends.Count
            });
        }

        /// <summary>
        /// Removes a friend.
        /// </summary>
        /// <param name="friendUserId">The friend's user ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Friend/{friendUserId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> RemoveFriend([FromRoute] [Required] Guid friendUserId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (!_socialRepository.AreFriends(userId.Value, friendUserId))
            {
                return BadRequest(new { success = false, error = "Not friends with this user" });
            }

            await _socialRepository.DeleteFriendshipAsync(userId.Value, friendUserId);

            var friendUser = _userManager.GetUserById(friendUserId);
            _logger.LogInformation("[Social] Friendship removed: {User1} <-> {User2}", userId, friendUserId);

            return Ok(new
            {
                success = true,
                message = $"Removed {friendUser?.Username ?? "user"} from friends"
            });
        }

        #endregion

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
