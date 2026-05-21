using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
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
        private readonly RatingsRepository _ratingsRepository;
        private readonly IUserManager _userManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserDataManager _userDataManager;
        private readonly ILogger<SocialController> _logger;
        private readonly SocialWebSocketListener _webSocketListener;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialController"/> class.
        /// </summary>
        /// <param name="socialRepository">Social repository.</param>
        /// <param name="ratingsRepository">Ratings repository.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="sessionManager">Session manager.</param>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="userDataManager">User data manager.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="webSocketListener">WebSocket listener for real-time updates.</param>
        public SocialController(
            SocialRepository socialRepository,
            RatingsRepository ratingsRepository,
            IUserManager userManager,
            ISessionManager sessionManager,
            ILibraryManager libraryManager,
            IUserDataManager userDataManager,
            ILogger<SocialController> logger,
            SocialWebSocketListener webSocketListener)
        {
            _socialRepository = socialRepository;
            _ratingsRepository = ratingsRepository;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _libraryManager = libraryManager;
            _userDataManager = userDataManager;
            _logger = logger;
            _webSocketListener = webSocketListener;
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

            var repoDebugInfo = _socialRepository.GetDebugInfo();
            _logger.LogInformation("[Social] Debug info requested by user {UserId}", userId);

            var connectedUserIds = _webSocketListener.GetConnectedUserIds();
            var connectedUserNames = connectedUserIds
                .Select(id => _userManager.GetUserById(id)?.Username ?? id.ToString())
                .ToList();

            return Ok(new
            {
                Repository = repoDebugInfo,
                WebSocket = new
                {
                    ConnectedUsers = _webSocketListener.GetConnectedUserCount(),
                    TotalConnections = _webSocketListener.GetTotalConnectionCount(),
                    ConnectedUserNames = connectedUserNames
                },
                CurrentUserId = userId.Value,
                IsCurrentUserConnected = connectedUserIds.Contains(userId.Value)
            });
        }

        /// <summary>
        /// Searches for users by username.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="limit">Max results.</param>
        /// <returns>List of matching users.</returns>
        [HttpGet("SearchUsers")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> SearchUsers([FromQuery] string query, [FromQuery] int limit = 10)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new { users = new object[0] });
            }

            // Get all users and filter
            var allUsers = _userManager.Users;
            var queryLower = query.ToLowerInvariant();

            var results = allUsers
                .Where(u => u.Id != userId.Value && u.Username.ToLowerInvariant().Contains(queryLower))
                .Take(limit)
                .Select(u => {
                    var isFriend = _socialRepository.AreFriends(userId.Value, u.Id);
                    var hasPendingRequest = _socialRepository.HasPendingRequest(userId.Value, u.Id);
                    var hasIncomingRequest = _socialRepository.HasPendingRequest(u.Id, userId.Value);
                    var profile = _socialRepository.GetProfile(u.Id);
                    var allowsRequests = profile == null || profile.Privacy.AllowFriendRequests != "Nobody";

                    return new {
                        userId = u.Id,
                        username = u.Username,
                        isFriend,
                        hasPendingRequest,
                        hasIncomingRequest,
                        canSendRequest = !isFriend && !hasPendingRequest && !hasIncomingRequest && allowsRequests
                    };
                })
                .ToList();

            return Ok(new { users = results });
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
        public async Task<ActionResult<UserProfile>> GetProfile([FromRoute] [Required] Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Get user from Jellyfin to ensure they exist and get current username
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Get or create profile
            var profile = await _socialRepository.GetOrCreateProfileAsync(userId, user.Username);

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

            // Get last seen from online status
            var onlineStatus = _socialRepository.GetOnlineStatus(userId);
            var lastSeen = onlineStatus?.LastSeen ?? profile.UpdatedAt;

            // Return explicit object to ensure correct data
            return Ok(new
            {
                id = profile.Id,
                userId = profile.UserId,
                username = user.Username,  // Always from Jellyfin
                bio = profile.Bio,
                avatarUrl = profile.AvatarUrl,
                createdAt = profile.CreatedAt,
                updatedAt = lastSeen,
                privacy = profile.Privacy
            });
        }

        /// <summary>
        /// Gets a user's profile statistics.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The user's stats.</returns>
        [HttpGet("Profile/{userId}/Stats")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<object> GetProfileStats([FromRoute] [Required] Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Get user from Jellyfin
            var user = _userManager.GetUserById(userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Get profile for privacy settings
            var profile = _socialRepository.GetProfile(userId);
            var isOwnProfile = currentUserId.Value == userId;
            var areFriends = !isOwnProfile && _socialRepository.AreFriends(currentUserId.Value, userId);

            // Check privacy for friends list visibility
            var canSeeFriendsList = isOwnProfile ||
                profile?.Privacy.ShowFriendsList == "Everyone" ||
                (profile?.Privacy.ShowFriendsList == "Friends" && areFriends);

            // Check privacy for watch history visibility
            var canSeeWatchHistory = isOwnProfile ||
                profile?.Privacy.ShowWatchedHistory == "Everyone" ||
                (profile?.Privacy.ShowWatchedHistory == "Friends" && areFriends);

            // Get friends count (respect privacy)
            int friendsCount = 0;
            if (canSeeFriendsList)
            {
                var friendIds = _socialRepository.GetFriendIds(userId);
                friendsCount = friendIds.Count;
            }

            // Get ratings data
            var userRatings = _ratingsRepository.GetUserRatings(userId);
            var ratingsCount = userRatings.Count;
            var averageRating = ratingsCount > 0
                ? Math.Round(userRatings.Average(r => r.Rating), 1)
                : 0;

            // Get how many ratings this user received (ratings on their reviews - not implemented yet)
            // For now, we can skip this or show 0

            // Calculate member duration
            var memberSince = profile?.CreatedAt ?? DateTime.UtcNow;
            var memberDays = (int)(DateTime.UtcNow - memberSince).TotalDays;

            // Get Jellyfin watch statistics (respect ShowWatchedHistory privacy)
            int moviesWatched = 0;
            int seriesWatched = 0;
            long totalWatchTicks = 0;

            if (canSeeWatchHistory)
            {
                try
                {
                    // Get movies watched
                    var movieQuery = new InternalItemsQuery(user)
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Movie },
                        IsPlayed = true,
                        Recursive = true
                    };
                    var playedMovies = _libraryManager.GetItemList(movieQuery);
                    moviesWatched = playedMovies?.Count ?? 0;

                    // Sum up movie watch time (use runtime as approximation)
                    if (playedMovies != null)
                    {
                        foreach (var movie in playedMovies)
                        {
                            if (movie.RunTimeTicks.HasValue)
                            {
                                totalWatchTicks += movie.RunTimeTicks.Value;
                            }
                        }
                    }

                    // Get series watched (series with at least one played episode)
                    var seriesQuery = new InternalItemsQuery(user)
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Series },
                        Recursive = true
                    };
                    var allSeries = _libraryManager.GetItemList(seriesQuery);

                    if (allSeries != null)
                    {
                        foreach (var series in allSeries)
                        {
                            var episodeQuery = new InternalItemsQuery(user)
                            {
                                IncludeItemTypes = new[] { BaseItemKind.Episode },
                                AncestorIds = new[] { series.Id },
                                IsPlayed = true,
                                Recursive = true,
                                Limit = 1
                            };
                            var playedEpisodes = _libraryManager.GetItemList(episodeQuery);
                            if (playedEpisodes != null && playedEpisodes.Count > 0)
                            {
                                seriesWatched++;
                            }
                        }

                        // Get all played episodes for watch time calculation
                        var allPlayedEpisodesQuery = new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { BaseItemKind.Episode },
                            IsPlayed = true,
                            Recursive = true
                        };
                        var allPlayedEpisodes = _libraryManager.GetItemList(allPlayedEpisodesQuery);
                        if (allPlayedEpisodes != null)
                        {
                            foreach (var episode in allPlayedEpisodes)
                            {
                                if (episode.RunTimeTicks.HasValue)
                                {
                                    totalWatchTicks += episode.RunTimeTicks.Value;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get Jellyfin watch stats for user {UserId}", userId);
                }
            }

            // Convert ticks to hours (1 tick = 100 nanoseconds, 10,000,000 ticks = 1 second)
            var totalWatchHours = (int)(totalWatchTicks / TimeSpan.TicksPerHour);

            return Ok(new
            {
                friendsCount,
                ratingsCount,
                averageRating,
                memberDays,
                moviesWatched,
                seriesWatched,
                totalWatchHours
            });
        }

        /// <summary>
        /// Registers that the current user is viewing a profile (for real-time updates).
        /// </summary>
        /// <param name="userId">The profile user ID being viewed.</param>
        /// <returns>Success status.</returns>
        [HttpPost("Profile/{userId}/View")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult RegisterProfileView([FromRoute] [Required] Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _webSocketListener.RegisterProfileViewer(currentUserId.Value, userId);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Unregisters profile view (when closing profile modal).
        /// </summary>
        /// <returns>Success status.</returns>
        [HttpDelete("Profile/View")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult UnregisterProfileView()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            _webSocketListener.UnregisterProfileViewer(currentUserId.Value);
            return Ok(new { success = true });
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

            // Validation: Check if either user has blocked the other
            if (_socialRepository.IsBlockedEitherWay(userId.Value, targetUserId))
            {
                _logger.LogWarning("[Social] User {From} tried to send friend request to {To} but blocked", currentUser.Username, targetUser.Username);
                return BadRequest(new { success = false, error = "Cannot send friend request to this user" });
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

            // Create notification for target user
            await _socialRepository.CreateFriendRequestNotificationAsync(targetUserId, currentUser.Username, userId.Value);

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

            // Notify the original sender that their request was accepted
            var currentUser = _userManager.GetUserById(userId.Value);
            if (currentUser != null)
            {
                await _socialRepository.CreateFriendAcceptedNotificationAsync(request.FromUserId, currentUser.Username, userId.Value);
            }

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

        #region Notifications

        /// <summary>
        /// Gets notifications for the current user.
        /// </summary>
        /// <param name="unreadOnly">Only return unread notifications.</param>
        /// <param name="limit">Maximum number to return.</param>
        /// <param name="offset">Offset for pagination.</param>
        /// <returns>List of notifications.</returns>
        [HttpGet("Notifications")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 20, [FromQuery] int offset = 0)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var notifications = _socialRepository.GetNotifications(userId.Value, unreadOnly, limit, offset);
            var unreadCount = _socialRepository.GetUnreadNotificationCount(userId.Value);

            return Ok(new
            {
                notifications,
                unreadCount
            });
        }

        /// <summary>
        /// Gets unread notification count.
        /// </summary>
        /// <returns>Unread count.</returns>
        [HttpGet("Notifications/UnreadCount")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetUnreadNotificationCount()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var count = _socialRepository.GetUnreadNotificationCount(userId.Value);

            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Marks a notification as read.
        /// </summary>
        /// <param name="notificationId">The notification ID.</param>
        /// <returns>Success status.</returns>
        [HttpPost("Notifications/{notificationId}/Read")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> MarkNotificationAsRead([FromRoute] [Required] Guid notificationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _socialRepository.MarkNotificationAsReadAsync(notificationId, userId.Value);
            if (!success)
            {
                return NotFound(new { success = false, error = "Notification not found" });
            }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Marks all notifications as read.
        /// </summary>
        /// <returns>Number marked.</returns>
        [HttpPost("Notifications/ReadAll")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> MarkAllNotificationsAsRead()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var count = await _socialRepository.MarkAllNotificationsAsReadAsync(userId.Value);

            return Ok(new { success = true, markedCount = count });
        }

        /// <summary>
        /// Deletes a notification.
        /// </summary>
        /// <param name="notificationId">The notification ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Notifications/{notificationId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> DeleteNotification([FromRoute] [Required] Guid notificationId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var success = await _socialRepository.DeleteNotificationAsync(notificationId, userId.Value);
            if (!success)
            {
                return NotFound(new { success = false, error = "Notification not found" });
            }

            return Ok(new { success = true });
        }

        #endregion

        #region Online Status

        /// <summary>
        /// Updates the user's heartbeat (marks them as online).
        /// </summary>
        /// <param name="request">Optional watching info from client for instant updates.</param>
        /// <returns>Current status info.</returns>
        [HttpPost("Heartbeat")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> Heartbeat()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Heartbeat ONLY updates online status, NOT watching
            var status = await _socialRepository.UpdateHeartbeatOnlyAsync(userId.Value);

            // Broadcast status update (with current watching info from repository)
            var user = _userManager.GetUserById(userId.Value);
            if (user != null)
            {
                _ = _webSocketListener.BroadcastStatusUpdateAsync(userId.Value, user.Username, status, status.Watching);
            }

            return Ok(new { status = status.Status });
        }

        /// <summary>
        /// Sets what the user is currently watching. Completely separate from online status.
        /// </summary>
        [HttpPost("Watching")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> SetWatching([FromBody] WatchingInfo watching)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var currentWatching = new CurrentlyWatching
            {
                ItemId = watching.ItemId,
                Title = watching.Title ?? "Unknown",
                Type = watching.Type ?? "Video",
                SeriesName = watching.SeriesName,
                EpisodeInfo = watching.EpisodeInfo,
                PositionTicks = watching.PositionTicks,
                DurationTicks = watching.DurationTicks,
                StartedAt = DateTime.UtcNow
            };

            // ONLY update watching - this does NOT affect online status
            await _socialRepository.SetWatchingOnlyAsync(userId.Value, currentWatching);

            // Broadcast ONLY the watching update, not status
            var user = _userManager.GetUserById(userId.Value);
            if (user != null)
            {
                _ = _webSocketListener.BroadcastWatchingUpdateAsync(userId.Value, user.Username, currentWatching);
            }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Clears what the user is watching. Completely separate from online status.
        /// </summary>
        [HttpPost("StopWatching")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> StopWatching()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // ONLY clear watching - this does NOT affect online status
            await _socialRepository.ClearWatchingOnlyAsync(userId.Value);

            // Broadcast ONLY the watching update (cleared), not status
            var user = _userManager.GetUserById(userId.Value);
            if (user != null)
            {
                _ = _webSocketListener.BroadcastWatchingUpdateAsync(userId.Value, user.Username, null);
            }

            return Ok(new { success = true });
        }

        // Legacy endpoint for backwards compatibility - redirects to new endpoints
        [HttpPost("HeartbeatLegacy")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> HeartbeatLegacy([FromBody] HeartbeatRequest? request = null)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            CurrentlyWatching? watching = null;
            if (request?.Watching != null)
            {
                watching = new CurrentlyWatching
                {
                    ItemId = request.Watching.ItemId,
                    Title = request.Watching.Title ?? "Unknown",
                    Type = request.Watching.Type ?? "Video",
                    SeriesName = request.Watching.SeriesName,
                    EpisodeInfo = request.Watching.EpisodeInfo,
                    PositionTicks = request.Watching.PositionTicks,
                    DurationTicks = request.Watching.DurationTicks,
                    StartedAt = DateTime.UtcNow
                };
            }

            var status = await _socialRepository.UpdateHeartbeatAsync(userId.Value, watching);
            var user = _userManager.GetUserById(userId.Value);
            if (user != null)
            {
                _ = _webSocketListener.BroadcastStatusUpdateAsync(userId.Value, user.Username, status, watching);
            }

            return Ok(new
            {
                status = status.Status,
                watching = watching != null ? new
                {
                    title = watching.Title,
                    type = watching.Type,
                    seriesName = watching.SeriesName,
                    episodeInfo = watching.EpisodeInfo,
                    position = watching.FormattedPosition,
                    duration = watching.FormattedDuration,
                    progress = watching.ProgressPercent
                } : null
            });
        }

        /// <summary>
        /// Marks the user as offline (called on logout or page unload).
        /// </summary>
        /// <returns>Success status.</returns>
        [HttpPost("Offline")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> GoOffline()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Set user's status to offline
            var status = _socialRepository.SetUserOffline(userId.Value);

            // Broadcast offline status to friends and profile viewers (skip rate limit for immediate update)
            var user = _userManager.GetUserById(userId.Value);
            if (user != null && status != null)
            {
                await _webSocketListener.BroadcastStatusUpdateAsync(userId.Value, user.Username, status, null, skipRateLimit: true);
            }

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets online status for all friends.
        /// </summary>
        /// <returns>Friends' online statuses.</returns>
        [HttpGet("OnlineStatus")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetOnlineStatus()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Get friend IDs
            var friendIds = _socialRepository.GetFriendIds(userId.Value);

            // Get their online statuses
            var statuses = _socialRepository.GetOnlineStatuses(friendIds);

            // Build response respecting privacy settings
            var friends = new System.Collections.Generic.List<object>();

            foreach (var friendId in friendIds)
            {
                var user = _userManager.GetUserById(friendId);
                var profile = _socialRepository.GetProfile(friendId);
                statuses.TryGetValue(friendId, out var friendStatus);

                // Check privacy - respect ShowOnlineStatus and ShowCurrentlyWatching
                var showStatus = profile == null ||
                    profile.Privacy.ShowOnlineStatus == "Everyone" ||
                    (profile.Privacy.ShowOnlineStatus == "Friends" && _socialRepository.AreFriends(userId.Value, friendId));

                var showWatching = profile == null ||
                    profile.Privacy.ShowCurrentlyWatching == "Everyone" ||
                    (profile.Privacy.ShowCurrentlyWatching == "Friends" && _socialRepository.AreFriends(userId.Value, friendId));

                // Handle Invisible - appears as Offline
                var effectiveStatus = friendStatus?.Status ?? "Offline";
                if (effectiveStatus == "Invisible")
                {
                    effectiveStatus = "Offline";
                }

                friends.Add(new
                {
                    userId = friendId,
                    username = user?.Username ?? "Unknown",
                    status = showStatus ? effectiveStatus : "Offline",
                    lastSeen = showStatus ? friendStatus?.LastSeen : null,
                    watching = showWatching && effectiveStatus != "Offline" && friendStatus?.Watching != null ? new
                    {
                        itemId = friendStatus.Watching.ItemId,
                        title = friendStatus.Watching.Title,
                        type = friendStatus.Watching.Type,
                        seriesName = friendStatus.Watching.SeriesName,
                        episodeInfo = friendStatus.Watching.EpisodeInfo,
                        position = friendStatus.Watching.FormattedPosition,
                        duration = friendStatus.Watching.FormattedDuration,
                        progress = friendStatus.Watching.ProgressPercent
                    } : null
                });
            }

            return Ok(new { friends });
        }

        /// <summary>
        /// Gets all online users (for testing/discovery).
        /// </summary>
        /// <returns>All users with online status.</returns>
        [HttpGet("AllOnlineUsers")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetAllOnlineUsers()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Get all users except current user
            var allUsers = _userManager.Users.Where(u => u.Id != userId.Value).ToList();
            var userIds = allUsers.Select(u => u.Id).ToList();

            // Get their online statuses
            var statuses = _socialRepository.GetOnlineStatuses(userIds);

            // Build response
            var users = new System.Collections.Generic.List<object>();

            foreach (var user in allUsers)
            {
                var profile = _socialRepository.GetProfile(user.Id);
                statuses.TryGetValue(user.Id, out var userStatus);
                var isFriend = _socialRepository.AreFriends(userId.Value, user.Id);

                // Check privacy - respect ShowOnlineStatus
                var showStatus = profile == null ||
                    profile.Privacy.ShowOnlineStatus == "Everyone" ||
                    (profile.Privacy.ShowOnlineStatus == "Friends" && isFriend);

                // Handle Invisible - appears as Offline
                var effectiveStatus = userStatus?.Status ?? "Offline";
                if (effectiveStatus == "Invisible")
                {
                    effectiveStatus = "Offline";
                }

                // Only show users who are Online (skip Away and Offline)
                var displayStatus = showStatus ? effectiveStatus : "Offline";
                if (displayStatus != "Online")
                {
                    continue; // Skip non-online users
                }

                users.Add(new
                {
                    userId = user.Id,
                    username = user.Username,
                    status = displayStatus,
                    isFriend,
                    lastSeen = showStatus ? userStatus?.LastSeen : null
                });
            }

            // Sort by username
            var sortedUsers = users
                .OrderBy(u => ((dynamic)u).username)
                .ToList();

            return Ok(new { users = sortedUsers });
        }

        /// <summary>
        /// Sets the user's manual status.
        /// </summary>
        /// <param name="request">Status request.</param>
        /// <returns>Success status.</returns>
        [HttpPost("Status")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> SetStatus([FromBody] SetStatusRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            // Validate status value
            var validStatuses = new[] { "Online", "Away", "DoNotDisturb", "Invisible", null };
            if (!validStatuses.Contains(request.Status))
            {
                return BadRequest(new { success = false, error = "Invalid status. Valid values: Online, Away, DoNotDisturb, Invisible, or null to clear." });
            }

            await _socialRepository.SetManualStatusAsync(userId.Value, request.Status);

            return Ok(new
            {
                success = true,
                status = request.Status ?? "auto",
                message = request.Status == null ? "Status set to automatic" : $"Status set to {request.Status}"
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

        #region Privacy Settings

        /// <summary>
        /// Gets the current user's privacy settings.
        /// </summary>
        /// <returns>Privacy settings.</returns>
        [HttpGet("Settings")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetSettings()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var profile = _socialRepository.GetProfile(userId.Value);
            var settings = profile?.Privacy ?? new UserPrivacySettings();

            return Ok(new
            {
                profileVisibility = settings.ProfileVisibility,
                showOnlineStatus = settings.ShowOnlineStatus,
                showWatchedHistory = settings.ShowWatchedHistory,
                showFriendsList = settings.ShowFriendsList,
                showCurrentlyWatching = settings.ShowCurrentlyWatching,
                allowFriendRequests = settings.AllowFriendRequests,
                allowMessages = settings.AllowMessages
            });
        }

        /// <summary>
        /// Updates the current user's privacy settings.
        /// </summary>
        /// <param name="request">The settings to update.</param>
        /// <returns>Updated settings.</returns>
        [HttpPost("Settings")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> UpdateSettings([FromBody] PrivacySettingsRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var profile = _socialRepository.GetProfile(userId.Value);
            if (profile == null)
            {
                // Create profile if it doesn't exist
                var user = _userManager.GetUserById(userId.Value);
                profile = new UserProfile
                {
                    UserId = userId.Value,
                    Username = user?.Username ?? "Unknown"
                };
            }

            // Update individual settings if provided and valid
            if (request.ProfileVisibility != null && IsValidPrivacySetting(request.ProfileVisibility))
            {
                profile.Privacy.ProfileVisibility = request.ProfileVisibility;
            }
            if (request.ShowOnlineStatus != null && IsValidVisibilitySetting(request.ShowOnlineStatus))
            {
                profile.Privacy.ShowOnlineStatus = request.ShowOnlineStatus;
            }
            if (request.ShowWatchedHistory != null && IsValidVisibilitySetting(request.ShowWatchedHistory))
            {
                profile.Privacy.ShowWatchedHistory = request.ShowWatchedHistory;
            }
            if (request.ShowFriendsList != null && IsValidVisibilitySetting(request.ShowFriendsList))
            {
                profile.Privacy.ShowFriendsList = request.ShowFriendsList;
            }
            if (request.ShowCurrentlyWatching != null && IsValidVisibilitySetting(request.ShowCurrentlyWatching))
            {
                profile.Privacy.ShowCurrentlyWatching = request.ShowCurrentlyWatching;
            }
            if (request.AllowFriendRequests != null && IsValidAllowSetting(request.AllowFriendRequests))
            {
                profile.Privacy.AllowFriendRequests = request.AllowFriendRequests;
            }
            if (request.AllowMessages != null && IsValidVisibilitySetting(request.AllowMessages))
            {
                profile.Privacy.AllowMessages = request.AllowMessages;
            }

            profile.UpdatedAt = DateTime.UtcNow;
            await _socialRepository.SaveProfileAsync(profile).ConfigureAwait(false);

            _logger.LogInformation("[Social] User {UserId} updated privacy settings", userId);

            return Ok(new
            {
                success = true,
                message = "Settings updated",
                settings = new
                {
                    profileVisibility = profile.Privacy.ProfileVisibility,
                    showOnlineStatus = profile.Privacy.ShowOnlineStatus,
                    showWatchedHistory = profile.Privacy.ShowWatchedHistory,
                    showFriendsList = profile.Privacy.ShowFriendsList,
                    showCurrentlyWatching = profile.Privacy.ShowCurrentlyWatching,
                    allowFriendRequests = profile.Privacy.AllowFriendRequests,
                    allowMessages = profile.Privacy.AllowMessages
                }
            });
        }

        /// <summary>
        /// Applies a privacy preset.
        /// </summary>
        /// <param name="preset">The preset name: Public, FriendsOnly, or Private.</param>
        /// <returns>Updated settings.</returns>
        [HttpPost("Settings/Preset/{preset}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> ApplyPreset(string preset)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var profile = _socialRepository.GetProfile(userId.Value);
            if (profile == null)
            {
                var user = _userManager.GetUserById(userId.Value);
                profile = new UserProfile
                {
                    UserId = userId.Value,
                    Username = user?.Username ?? "Unknown"
                };
            }

            // Ensure Privacy is initialized
            if (profile.Privacy == null)
            {
                profile.Privacy = new UserPrivacySettings();
            }

            switch (preset.ToLowerInvariant())
            {
                case "public":
                    profile.Privacy.ProfileVisibility = "Public";
                    profile.Privacy.ShowOnlineStatus = "Everyone";
                    profile.Privacy.ShowWatchedHistory = "Everyone";
                    profile.Privacy.ShowFriendsList = "Everyone";
                    profile.Privacy.ShowCurrentlyWatching = "Everyone";
                    profile.Privacy.AllowFriendRequests = "Everyone";
                    profile.Privacy.AllowMessages = "Everyone";
                    break;

                case "friendsonly":
                case "friends":
                    profile.Privacy.ProfileVisibility = "Friends";
                    profile.Privacy.ShowOnlineStatus = "Friends";
                    profile.Privacy.ShowWatchedHistory = "Friends";
                    profile.Privacy.ShowFriendsList = "Friends";
                    profile.Privacy.ShowCurrentlyWatching = "Friends";
                    profile.Privacy.AllowFriendRequests = "Everyone";
                    profile.Privacy.AllowMessages = "Friends";
                    break;

                case "private":
                    profile.Privacy.ProfileVisibility = "Private";
                    profile.Privacy.ShowOnlineStatus = "Nobody";
                    profile.Privacy.ShowWatchedHistory = "Nobody";
                    profile.Privacy.ShowFriendsList = "Nobody";
                    profile.Privacy.ShowCurrentlyWatching = "Nobody";
                    profile.Privacy.AllowFriendRequests = "Nobody";
                    profile.Privacy.AllowMessages = "Nobody";
                    break;

                default:
                    return BadRequest(new { message = "Invalid preset. Use: Public, FriendsOnly, or Private" });
            }

            profile.UpdatedAt = DateTime.UtcNow;
            await _socialRepository.SaveProfileAsync(profile).ConfigureAwait(false);

            _logger.LogInformation("[Social] User {UserId} applied privacy preset: {Preset}", userId, preset);

            return Ok(new
            {
                success = true,
                message = $"Applied '{preset}' preset",
                settings = new
                {
                    profileVisibility = profile.Privacy.ProfileVisibility,
                    showOnlineStatus = profile.Privacy.ShowOnlineStatus,
                    showWatchedHistory = profile.Privacy.ShowWatchedHistory,
                    showFriendsList = profile.Privacy.ShowFriendsList,
                    showCurrentlyWatching = profile.Privacy.ShowCurrentlyWatching,
                    allowFriendRequests = profile.Privacy.AllowFriendRequests,
                    allowMessages = profile.Privacy.AllowMessages
                }
            });
        }

        #endregion

        #region Block System

        /// <summary>
        /// Blocks a user.
        /// </summary>
        /// <param name="userId">The user to block.</param>
        /// <returns>Result of the operation.</returns>
        [HttpPost("Block/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> BlockUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (userId == currentUserId.Value)
            {
                return BadRequest(new { message = "Cannot block yourself" });
            }

            // Check if target user exists
            var targetUser = _userManager.GetUserById(userId);
            if (targetUser == null)
            {
                return BadRequest(new { message = "User not found" });
            }

            var block = await _socialRepository.BlockUserAsync(currentUserId.Value, userId).ConfigureAwait(false);

            _logger.LogInformation("[Social] User {UserId} blocked {BlockedUserId}", currentUserId, userId);

            return Ok(new
            {
                success = true,
                message = "User blocked",
                blockedUser = new
                {
                    userId = userId,
                    username = targetUser.Username,
                    blockedAt = block.CreatedAt
                }
            });
        }

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="userId">The user to unblock.</param>
        /// <returns>Result of the operation.</returns>
        [HttpDelete("Block/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> UnblockUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var unblocked = await _socialRepository.UnblockUserAsync(currentUserId.Value, userId).ConfigureAwait(false);

            if (!unblocked)
            {
                return NotFound(new { message = "User was not blocked" });
            }

            _logger.LogInformation("[Social] User {UserId} unblocked {BlockedUserId}", currentUserId, userId);

            return Ok(new
            {
                success = true,
                message = "User unblocked"
            });
        }

        /// <summary>
        /// Gets the list of blocked users.
        /// </summary>
        /// <returns>List of blocked users.</returns>
        [HttpGet("Blocked")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetBlockedUsers()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var blockedUsers = _socialRepository.GetBlockedUsers(userId.Value);

            var result = blockedUsers.Select(b =>
            {
                var user = _userManager.GetUserById(b.BlockedUserId);
                return new
                {
                    userId = b.BlockedUserId,
                    username = user?.Username ?? "Unknown",
                    blockedAt = b.CreatedAt
                };
            }).ToList();

            return Ok(new { blockedUsers = result, count = result.Count });
        }

        /// <summary>
        /// Checks if a user is blocked.
        /// </summary>
        /// <param name="userId">The user to check.</param>
        /// <returns>Block status.</returns>
        [HttpGet("Block/{userId}/Status")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetBlockStatus(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var iBlocked = _socialRepository.HasBlocked(currentUserId.Value, userId);
            var theyBlocked = _socialRepository.HasBlocked(userId, currentUserId.Value);

            return Ok(new
            {
                iBlockedThem = iBlocked,
                theyBlockedMe = theyBlocked,
                isBlockedEitherWay = iBlocked || theyBlocked
            });
        }

        #endregion

        #region Following System

        /// <summary>
        /// Follow a user.
        /// </summary>
        /// <param name="userId">The user to follow.</param>
        /// <returns>The follow relationship.</returns>
        [HttpPost("Follow/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> FollowUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (currentUserId.Value == userId)
            {
                return BadRequest(new { error = "Cannot follow yourself" });
            }

            // Check if blocked
            if (_socialRepository.IsBlockedEitherWay(currentUserId.Value, userId))
            {
                return BadRequest(new { error = "Cannot follow blocked user" });
            }

            var follow = await _socialRepository.FollowUserAsync(currentUserId.Value, userId).ConfigureAwait(false);

            // Create notification for the followed user
            var currentUser = _userManager.GetUserById(currentUserId.Value);
            if (currentUser != null)
            {
                var notification = new SocialNotification
                {
                    UserId = userId,
                    Type = "NewFollower",
                    Title = "New Follower",
                    Message = $"{currentUser.Username} started following you"
                };
                notification.Data["fromUserId"] = currentUserId.Value.ToString();
                notification.Data["fromUsername"] = currentUser.Username;
                await _socialRepository.CreateNotificationAsync(notification).ConfigureAwait(false);
            }

            return Ok(new
            {
                success = true,
                isFollowing = true,
                isMutual = _socialRepository.AreMutualFollowers(currentUserId.Value, userId)
            });
        }

        /// <summary>
        /// Unfollow a user.
        /// </summary>
        /// <param name="userId">The user to unfollow.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Follow/{userId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> UnfollowUser(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            await _socialRepository.UnfollowUserAsync(currentUserId.Value, userId).ConfigureAwait(false);

            return Ok(new { success = true, isFollowing = false });
        }

        /// <summary>
        /// Get followers of a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of followers.</returns>
        [HttpGet("Profile/{userId}/Followers")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFollowers(Guid userId)
        {
            var followers = _socialRepository.GetFollowers(userId);
            var result = followers.Select(f =>
            {
                var user = _userManager.GetUserById(f.FollowerId);
                var profile = _socialRepository.GetProfile(f.FollowerId);
                return new
                {
                    userId = f.FollowerId,
                    username = user?.Username ?? "Unknown",
                    avatarUrl = profile?.AvatarUrl,
                    followedAt = f.CreatedAt
                };
            }).ToList();

            return Ok(new { followers = result, count = result.Count });
        }

        /// <summary>
        /// Get users that a user is following.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of following.</returns>
        [HttpGet("Profile/{userId}/Following")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFollowing(Guid userId)
        {
            var following = _socialRepository.GetFollowing(userId);
            var result = following.Select(f =>
            {
                var user = _userManager.GetUserById(f.FollowingId);
                var profile = _socialRepository.GetProfile(f.FollowingId);
                return new
                {
                    userId = f.FollowingId,
                    username = user?.Username ?? "Unknown",
                    avatarUrl = profile?.AvatarUrl,
                    followedAt = f.CreatedAt
                };
            }).ToList();

            return Ok(new { following = result, count = result.Count });
        }

        /// <summary>
        /// Get follow status for a user.
        /// </summary>
        /// <param name="userId">The user to check.</param>
        /// <returns>Follow status.</returns>
        [HttpGet("Follow/{userId}/Status")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFollowStatus(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            return Ok(new
            {
                isFollowing = _socialRepository.IsFollowing(currentUserId.Value, userId),
                isFollowedBy = _socialRepository.IsFollowing(userId, currentUserId.Value),
                isMutual = _socialRepository.AreMutualFollowers(currentUserId.Value, userId)
            });
        }

        #endregion

        #region Profile Likes

        /// <summary>
        /// Like a user's profile.
        /// </summary>
        /// <param name="userId">The profile to like.</param>
        /// <returns>Like status.</returns>
        [HttpPost("Profile/{userId}/Like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> LikeProfile(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (currentUserId.Value == userId)
            {
                return BadRequest(new { error = "Cannot like your own profile" });
            }

            await _socialRepository.LikeProfileAsync(userId, currentUserId.Value).ConfigureAwait(false);

            // Create notification
            var currentUser = _userManager.GetUserById(currentUserId.Value);
            if (currentUser != null)
            {
                var notification = new SocialNotification
                {
                    UserId = userId,
                    Type = "ProfileLike",
                    Title = "Profile Liked",
                    Message = $"{currentUser.Username} liked your profile"
                };
                notification.Data["fromUserId"] = currentUserId.Value.ToString();
                notification.Data["fromUsername"] = currentUser.Username;
                await _socialRepository.CreateNotificationAsync(notification).ConfigureAwait(false);
            }

            return Ok(new
            {
                success = true,
                liked = true,
                likeCount = _socialRepository.GetProfileLikeCount(userId)
            });
        }

        /// <summary>
        /// Unlike a user's profile.
        /// </summary>
        /// <param name="userId">The profile to unlike.</param>
        /// <returns>Like status.</returns>
        [HttpDelete("Profile/{userId}/Like")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> UnlikeProfile(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            await _socialRepository.UnlikeProfileAsync(userId, currentUserId.Value).ConfigureAwait(false);

            return Ok(new
            {
                success = true,
                liked = false,
                likeCount = _socialRepository.GetProfileLikeCount(userId)
            });
        }

        /// <summary>
        /// Get profile likes.
        /// </summary>
        /// <param name="userId">The profile user ID.</param>
        /// <returns>Like count and likers.</returns>
        [HttpGet("Profile/{userId}/Likes")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetProfileLikes(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var likers = _socialRepository.GetProfileLikers(userId);
            var profile = _socialRepository.GetProfile(userId);

            // Check privacy settings for showing likers
            var showLikers = profile?.Privacy?.ProfileLikersVisibleRegular == true ||
                             (profile?.Privacy?.ProfileLikersVisibleFriends == true &&
                              _socialRepository.AreFriends(currentUserId ?? Guid.Empty, userId));

            var likersResult = showLikers ? likers.Select(l =>
            {
                var user = _userManager.GetUserById(l.LikerUserId);
                var likerProfile = _socialRepository.GetProfile(l.LikerUserId);
                return new
                {
                    userId = l.LikerUserId,
                    username = user?.Username ?? "Unknown",
                    avatarUrl = likerProfile?.AvatarUrl,
                    likedAt = l.CreatedAt
                };
            }).ToList() : null;

            return Ok(new
            {
                likeCount = likers.Count,
                hasLiked = currentUserId.HasValue && _socialRepository.HasLikedProfile(userId, currentUserId.Value),
                likers = likersResult
            });
        }

        #endregion

        #region Media Lists

        /// <summary>
        /// Create a new media list.
        /// </summary>
        /// <param name="request">List creation request.</param>
        /// <returns>The created list.</returns>
        [HttpPost("Lists")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserMediaList>> CreateList([FromBody] CreateListRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            try
            {
                var list = new UserMediaList
                {
                    UserId = currentUserId.Value,
                    Title = request.Title,
                    Description = request.Description ?? string.Empty,
                    ListType = request.ListType ?? "Mixed",
                    MaxItems = Math.Min(request.MaxItems ?? 10, 50),
                    VisibleToRegularUsers = request.VisibleToRegularUsers ?? true,
                    VisibleToFriends = request.VisibleToFriends ?? true,
                    IsFavorites = request.IsFavorites ?? false,
                    IsWatchlist = request.IsWatchlist ?? false
                };

                var created = await _socialRepository.CreateListAsync(list).ConfigureAwait(false);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get user's own lists.
        /// </summary>
        /// <returns>List of user's media lists.</returns>
        [HttpGet("MyLists")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetMyLists()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var lists = _socialRepository.GetUserLists(currentUserId.Value);
            var result = lists.Select(l => new
            {
                id = l.Id,
                title = l.Title,
                description = l.Description,
                listType = l.ListType,
                maxItems = l.MaxItems,
                itemCount = _socialRepository.GetListItems(l.Id).Count,
                visibleToRegularUsers = l.VisibleToRegularUsers,
                visibleToFriends = l.VisibleToFriends,
                isFavorites = l.IsFavorites,
                isWatchlist = l.IsWatchlist,
                clonedFrom = l.ClonedFromUsername,
                createdAt = l.CreatedAt,
                updatedAt = l.UpdatedAt
            }).ToList();

            return Ok(new { lists = result, count = result.Count });
        }

        /// <summary>
        /// Get a user's visible lists.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of visible media lists.</returns>
        [HttpGet("Profile/{userId}/Lists")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetUserLists(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var isFriend = currentUserId.HasValue && _socialRepository.AreFriends(currentUserId.Value, userId);
            var isOwner = currentUserId.HasValue && currentUserId.Value == userId;

            var lists = isOwner
                ? _socialRepository.GetUserLists(userId)
                : _socialRepository.GetVisibleLists(userId, currentUserId ?? Guid.Empty, isFriend);

            var result = lists.Select(l => new
            {
                id = l.Id,
                title = l.Title,
                description = l.Description,
                listType = l.ListType,
                itemCount = _socialRepository.GetListItems(l.Id).Count,
                clonedFrom = l.ClonedFromUsername,
                createdAt = l.CreatedAt
            }).ToList();

            return Ok(new { lists = result, count = result.Count });
        }

        /// <summary>
        /// Get a single list with items.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <returns>The list with items.</returns>
        [HttpGet("Lists/{listId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<object> GetList(Guid listId)
        {
            var list = _socialRepository.GetList(listId);
            if (list == null)
            {
                return NotFound(new { error = "List not found" });
            }

            var currentUserId = GetCurrentUserId();
            var isFriend = currentUserId.HasValue && _socialRepository.AreFriends(currentUserId.Value, list.UserId);
            var isOwner = currentUserId.HasValue && currentUserId.Value == list.UserId;

            // Check visibility
            if (!isOwner && !((isFriend && list.VisibleToFriends) || (!isFriend && list.VisibleToRegularUsers)))
            {
                return NotFound(new { error = "List not found" });
            }

            var items = _socialRepository.GetListItems(listId);
            var ownerProfile = _socialRepository.GetProfile(list.UserId);
            var owner = _userManager.GetUserById(list.UserId);

            return Ok(new
            {
                id = list.Id,
                title = list.Title,
                description = list.Description,
                listType = list.ListType,
                maxItems = list.MaxItems,
                visibleToRegularUsers = list.VisibleToRegularUsers,
                visibleToFriends = list.VisibleToFriends,
                isFavorites = list.IsFavorites,
                isWatchlist = list.IsWatchlist,
                clonedFromUserId = list.ClonedFromUserId,
                clonedFromUsername = list.ClonedFromUsername,
                owner = new
                {
                    userId = list.UserId,
                    username = owner?.Username ?? "Unknown",
                    avatarUrl = ownerProfile?.AvatarUrl
                },
                items = items.Select(i => new
                {
                    id = i.Id,
                    itemId = i.ItemId,
                    imdbId = i.ImdbId,
                    title = i.CachedTitle,
                    imageUrl = i.CachedImageUrl,
                    overview = i.CachedOverview,
                    year = i.CachedYear,
                    mediaType = i.CachedMediaType,
                    note = i.Note,
                    position = i.Position,
                    addedAt = i.AddedAt
                }).ToList(),
                isOwner = isOwner,
                createdAt = list.CreatedAt,
                updatedAt = list.UpdatedAt
            });
        }

        /// <summary>
        /// Update a list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="request">Update request.</param>
        /// <returns>Updated list.</returns>
        [HttpPut("Lists/{listId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<UserMediaList>> UpdateList(Guid listId, [FromBody] UpdateListRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null)
            {
                return NotFound(new { error = "List not found" });
            }

            if (list.UserId != currentUserId.Value)
            {
                return Forbid();
            }

            if (request.Title != null) list.Title = request.Title;
            if (request.Description != null) list.Description = request.Description;
            if (request.ListType != null) list.ListType = request.ListType;
            if (request.MaxItems.HasValue) list.MaxItems = Math.Min(request.MaxItems.Value, 50);
            if (request.VisibleToRegularUsers.HasValue) list.VisibleToRegularUsers = request.VisibleToRegularUsers.Value;
            if (request.VisibleToFriends.HasValue) list.VisibleToFriends = request.VisibleToFriends.Value;

            var updated = await _socialRepository.UpdateListAsync(list).ConfigureAwait(false);
            return Ok(updated);
        }

        /// <summary>
        /// Delete a list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Lists/{listId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<object>> DeleteList(Guid listId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null)
            {
                return NotFound(new { error = "List not found" });
            }

            if (list.UserId != currentUserId.Value)
            {
                return Forbid();
            }

            await _socialRepository.DeleteListAsync(listId).ConfigureAwait(false);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Clone a list.
        /// </summary>
        /// <param name="listId">The list ID to clone.</param>
        /// <returns>The cloned list.</returns>
        [HttpPost("Lists/{listId}/Clone")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserMediaList>> CloneList(Guid listId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var currentUser = _userManager.GetUserById(currentUserId.Value);
            try
            {
                var cloned = await _socialRepository.CloneListAsync(listId, currentUserId.Value, currentUser?.Username ?? "Unknown").ConfigureAwait(false);
                return Ok(cloned);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Add item to list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="request">Add item request.</param>
        /// <returns>The added item.</returns>
        [HttpPost("Lists/{listId}/Items")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<UserMediaListItem>> AddListItem(Guid listId, [FromBody] AddListItemRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null || list.UserId != currentUserId.Value)
            {
                return NotFound(new { error = "List not found" });
            }

            try
            {
                var item = new UserMediaListItem
                {
                    ListId = listId,
                    ItemId = request.ItemId,
                    ImdbId = request.ImdbId,
                    CachedTitle = request.Title ?? string.Empty,
                    CachedImageUrl = request.ImageUrl,
                    CachedOverview = request.Overview,
                    CachedYear = request.Year,
                    CachedMediaType = request.MediaType,
                    CachedGenres = request.Genres,
                    CachedAt = DateTime.UtcNow,
                    Note = request.Note
                };

                // If itemId provided, get metadata from Jellyfin
                if (request.ItemId.HasValue)
                {
                    var jellyfinItem = _libraryManager.GetItemById(request.ItemId.Value);
                    if (jellyfinItem != null)
                    {
                        item.CachedTitle = jellyfinItem.Name;
                        item.CachedYear = jellyfinItem.ProductionYear;
                        item.CachedOverview = jellyfinItem.Overview;
                        item.CachedMediaType = jellyfinItem.GetType().Name;
                        item.CachedImageUrl = jellyfinItem.PrimaryImagePath;
                    }
                }

                var added = await _socialRepository.AddListItemAsync(item).ConfigureAwait(false);
                return Ok(added);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Update list item.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="itemId">The item ID.</param>
        /// <param name="request">Update request.</param>
        /// <returns>Updated item.</returns>
        [HttpPut("Lists/{listId}/Items/{itemId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserMediaListItem>> UpdateListItem(Guid listId, Guid itemId, [FromBody] UpdateListItemRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null || list.UserId != currentUserId.Value)
            {
                return NotFound(new { error = "List not found" });
            }

            var items = _socialRepository.GetListItems(listId);
            var item = items.FirstOrDefault(i => i.Id == itemId);
            if (item == null)
            {
                return NotFound(new { error = "Item not found" });
            }

            if (request.Note != null) item.Note = request.Note;
            if (request.Position.HasValue) item.Position = request.Position.Value;

            var updated = await _socialRepository.UpdateListItemAsync(item).ConfigureAwait(false);
            return Ok(updated);
        }

        /// <summary>
        /// Remove item from list.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("Lists/{listId}/Items/{itemId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<object>> RemoveListItem(Guid listId, Guid itemId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null || list.UserId != currentUserId.Value)
            {
                return NotFound(new { error = "List not found" });
            }

            await _socialRepository.RemoveListItemAsync(itemId).ConfigureAwait(false);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Reorder list items.
        /// </summary>
        /// <param name="listId">The list ID.</param>
        /// <param name="request">Reorder request.</param>
        /// <returns>Success status.</returns>
        [HttpPut("Lists/{listId}/Reorder")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> ReorderListItems(Guid listId, [FromBody] ReorderItemsRequest request)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var list = _socialRepository.GetList(listId);
            if (list == null || list.UserId != currentUserId.Value)
            {
                return NotFound(new { error = "List not found" });
            }

            await _socialRepository.ReorderListItemsAsync(listId, request.ItemIds).ConfigureAwait(false);
            return Ok(new { success = true });
        }

        #endregion

        #region Profile Styles

        /// <summary>
        /// Get profile style settings.
        /// </summary>
        /// <returns>Style settings.</returns>
        [HttpGet("MyProfile/Style")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<UserProfileStyle> GetMyProfileStyle()
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var style = _socialRepository.GetOrCreateProfileStyle(currentUserId.Value);
            return Ok(style);
        }

        /// <summary>
        /// Get a user's profile style.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Style settings.</returns>
        [HttpGet("Profile/{userId}/Style")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<UserProfileStyle> GetProfileStyle(Guid userId)
        {
            var style = _socialRepository.GetProfileStyle(userId);
            if (style == null)
            {
                // Return default style
                return Ok(new UserProfileStyle { UserId = userId });
            }
            return Ok(style);
        }

        /// <summary>
        /// Update profile style settings.
        /// </summary>
        /// <param name="style">Style settings.</param>
        /// <returns>Updated style.</returns>
        [HttpPut("MyProfile/Style")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<UserProfileStyle>> UpdateMyProfileStyle([FromBody] UserProfileStyle style)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            style.UserId = currentUserId.Value;
            var updated = await _socialRepository.SaveProfileStyleAsync(style).ConfigureAwait(false);
            return Ok(updated);
        }

        #endregion

        #region Enhanced Stats

        /// <summary>
        /// Get full profile stats for header display.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Full stats object.</returns>
        [HttpGet("Profile/{userId}/Stats/Full")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFullProfileStats(Guid userId)
        {
            var currentUserId = GetCurrentUserId();
            var profile = _socialRepository.GetProfile(userId);
            var user = _userManager.GetUserById(userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            // Get all ratings for the user
            var userRatings = _ratingsRepository.GetUserRatings(userId);
            var currentYear = DateTime.UtcNow.Year;
            var ratingsThisYear = userRatings.Where(r => r.CreatedAt.Year == currentYear).ToList();

            // Count movies and series
            var movieCount = 0;
            var seriesCount = 0;
            foreach (var rating in userRatings)
            {
                var item = _libraryManager.GetItemById(rating.ItemId);
                if (item != null)
                {
                    if (item.GetType().Name == "Movie") movieCount++;
                    else if (item.GetType().Name == "Series") seriesCount++;
                }
            }

            // Get review likes/dislikes
            var totalLikes = 0;
            var totalDislikes = 0;
            foreach (var rating in userRatings.Where(r => !string.IsNullOrEmpty(r.ReviewText)))
            {
                var (likes, dislikes) = _ratingsRepository.GetReviewLikeCounts(userId, rating.ItemId);
                totalLikes += likes;
                totalDislikes += dislikes;
            }

            return Ok(new
            {
                userId = userId,
                username = user.Username,
                bio = profile?.Bio ?? string.Empty,
                avatarUrl = profile?.AvatarUrl,
                films = movieCount,
                series = seriesCount,
                thisYear = ratingsThisYear.Count,
                lists = _socialRepository.GetListCount(userId),
                following = _socialRepository.GetFollowingCount(userId),
                followers = _socialRepository.GetFollowerCount(userId),
                profileLikes = _socialRepository.GetProfileLikeCount(userId),
                reviewLikesReceived = totalLikes,
                reviewDislikesReceived = totalDislikes,
                totalRatings = userRatings.Count,
                averageRating = userRatings.Any() ? Math.Round(userRatings.Average(r => r.Rating), 1) : 0,
                memberSince = profile?.CreatedAt ?? DateTime.UtcNow,
                isOnline = _socialRepository.GetOnlineStatus(userId)?.GetEffectiveStatus() == "Online",
                isFollowing = currentUserId.HasValue && _socialRepository.IsFollowing(currentUserId.Value, userId),
                isFollowedBy = currentUserId.HasValue && _socialRepository.IsFollowing(userId, currentUserId.Value),
                isFriend = currentUserId.HasValue && _socialRepository.AreFriends(currentUserId.Value, userId),
                hasLikedProfile = currentUserId.HasValue && _socialRepository.HasLikedProfile(userId, currentUserId.Value)
            });
        }

        /// <summary>
        /// Get stats for a specific year.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="year">The year.</param>
        /// <returns>Yearly stats.</returns>
        [HttpGet("Profile/{userId}/Stats/Year/{year}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetYearlyStats(Guid userId, int year)
        {
            var userRatings = _ratingsRepository.GetUserRatings(userId);
            var yearRatings = userRatings.Where(r => r.CreatedAt.Year == year).ToList();

            var movieCount = 0;
            var seriesCount = 0;
            var totalMinutes = 0L;

            foreach (var rating in yearRatings)
            {
                var item = _libraryManager.GetItemById(rating.ItemId);
                if (item != null)
                {
                    if (item.GetType().Name == "Movie")
                    {
                        movieCount++;
                        totalMinutes += item.RunTimeTicks.HasValue ? item.RunTimeTicks.Value / 600000000 : 0;
                    }
                    else if (item.GetType().Name == "Series")
                    {
                        seriesCount++;
                    }
                }
            }

            // Get rating distribution for the year
            var distribution = new int[10];
            foreach (var rating in yearRatings)
            {
                if (rating.Rating >= 1 && rating.Rating <= 10)
                {
                    distribution[rating.Rating - 1]++;
                }
            }

            return Ok(new
            {
                year = year,
                films = movieCount,
                series = seriesCount,
                totalRatings = yearRatings.Count,
                averageRating = yearRatings.Any() ? Math.Round(yearRatings.Average(r => r.Rating), 1) : 0,
                hoursWatched = Math.Round(totalMinutes / 60.0, 1),
                reviewsWritten = yearRatings.Count(r => !string.IsNullOrEmpty(r.ReviewText)),
                distribution = distribution
            });
        }

        #endregion

        #region Featured Reviews

        /// <summary>
        /// Feature a review.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Success status.</returns>
        [HttpPut("MyReviews/{itemId}/Feature")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> FeatureReview(Guid itemId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            // Check if user has a review for this item
            var rating = _ratingsRepository.GetUserRating(currentUserId.Value, itemId);
            if (rating == null || string.IsNullOrEmpty(rating.ReviewText))
            {
                return BadRequest(new { error = "You don't have a review for this item" });
            }

            try
            {
                await _socialRepository.FeatureReviewAsync(currentUserId.Value, itemId).ConfigureAwait(false);
                return Ok(new { success = true, featured = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Unfeature a review.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("MyReviews/{itemId}/Feature")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> UnfeatureReview(Guid itemId)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            await _socialRepository.UnfeatureReviewAsync(currentUserId.Value, itemId).ConfigureAwait(false);
            return Ok(new { success = true, featured = false });
        }

        /// <summary>
        /// Get featured reviews for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Featured reviews.</returns>
        [HttpGet("Profile/{userId}/FeaturedReviews")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ActionResult<object> GetFeaturedReviews(Guid userId)
        {
            var featured = _socialRepository.GetFeaturedReviews(userId);
            var result = featured.Select(f =>
            {
                var rating = _ratingsRepository.GetUserRating(userId, f.ItemId);
                var item = _libraryManager.GetItemById(f.ItemId);
                var (likes, dislikes) = _ratingsRepository.GetReviewLikeCounts(userId, f.ItemId);

                return new
                {
                    itemId = f.ItemId,
                    title = item?.Name ?? "Unknown",
                    year = item?.ProductionYear,
                    imageUrl = item?.PrimaryImagePath,
                    rating = rating?.Rating,
                    reviewText = rating?.ReviewText,
                    reviewDate = rating?.CreatedAt,
                    likes = likes,
                    dislikes = dislikes,
                    position = f.Position
                };
            }).ToList();

            return Ok(new { featuredReviews = result });
        }

        #endregion

        #region IMDB Lookup

        /// <summary>
        /// Get cached IMDB item or lookup.
        /// </summary>
        /// <param name="imdbId">The IMDB ID.</param>
        /// <returns>Cached IMDB data.</returns>
        [HttpGet("IMDB/{imdbId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<ImdbCacheItem> GetImdbItem(string imdbId)
        {
            var cached = _socialRepository.GetImdbCache(imdbId);
            if (cached != null)
            {
                return Ok(cached);
            }

            // Return empty item indicating not found
            return NotFound(new { error = "IMDB item not found in cache", imdbId = imdbId });
        }

        /// <summary>
        /// Save IMDB cache item (for manual entry).
        /// </summary>
        /// <param name="item">The IMDB cache item.</param>
        /// <returns>Saved item.</returns>
        [HttpPost("IMDB")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ImdbCacheItem>> SaveImdbItem([FromBody] ImdbCacheItem item)
        {
            if (string.IsNullOrEmpty(item.ImdbId))
            {
                return BadRequest(new { error = "IMDB ID is required" });
            }

            item.FetchSuccess = true;
            var saved = await _socialRepository.SaveImdbCacheAsync(item).ConfigureAwait(false);
            return Ok(saved);
        }

        /// <summary>
        /// Refresh IMDB cache (remove cached item).
        /// </summary>
        /// <param name="imdbId">The IMDB ID.</param>
        /// <returns>Success status.</returns>
        [HttpDelete("IMDB/{imdbId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<object>> RefreshImdbItem(string imdbId)
        {
            await _socialRepository.RemoveImdbCacheAsync(imdbId).ConfigureAwait(false);
            return Ok(new { success = true, message = "Cache cleared. Re-lookup to refresh." });
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

    /// <summary>
    /// Request model for setting status.
    /// </summary>
    public class SetStatusRequest
    {
        /// <summary>
        /// Gets or sets the status. Valid values: Online, Away, DoNotDisturb, Invisible, or null to clear.
        /// </summary>
        public string? Status { get; set; }
    }

    /// <summary>
    /// Request model for updating privacy settings.
    /// </summary>
    public class PrivacySettingsRequest
    {
        /// <summary>
        /// Gets or sets profile visibility. Values: Public, Friends, Private.
        /// </summary>
        public string? ProfileVisibility { get; set; }

        /// <summary>
        /// Gets or sets who can see online status. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string? ShowOnlineStatus { get; set; }

        /// <summary>
        /// Gets or sets who can see watched history. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string? ShowWatchedHistory { get; set; }

        /// <summary>
        /// Gets or sets who can see friends list. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string? ShowFriendsList { get; set; }

        /// <summary>
        /// Gets or sets who can see currently watching. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string? ShowCurrentlyWatching { get; set; }

        /// <summary>
        /// Gets or sets who can send friend requests. Values: Everyone, Nobody.
        /// </summary>
        public string? AllowFriendRequests { get; set; }

        /// <summary>
        /// Gets or sets who can send messages. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string? AllowMessages { get; set; }
    }

    /// <summary>
    /// Request model for creating a list.
    /// </summary>
    public class CreateListRequest
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [Required]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the list type (Movies, Series, Mixed).
        /// </summary>
        public string? ListType { get; set; }

        /// <summary>
        /// Gets or sets the max items (up to 50).
        /// </summary>
        public int? MaxItems { get; set; }

        /// <summary>
        /// Gets or sets visibility to regular users.
        /// </summary>
        public bool? VisibleToRegularUsers { get; set; }

        /// <summary>
        /// Gets or sets visibility to friends.
        /// </summary>
        public bool? VisibleToFriends { get; set; }

        /// <summary>
        /// Gets or sets whether this is a favorites list.
        /// </summary>
        public bool? IsFavorites { get; set; }

        /// <summary>
        /// Gets or sets whether this is a watchlist.
        /// </summary>
        public bool? IsWatchlist { get; set; }
    }

    /// <summary>
    /// Request model for updating a list.
    /// </summary>
    public class UpdateListRequest
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the list type.
        /// </summary>
        public string? ListType { get; set; }

        /// <summary>
        /// Gets or sets the max items.
        /// </summary>
        public int? MaxItems { get; set; }

        /// <summary>
        /// Gets or sets visibility to regular users.
        /// </summary>
        public bool? VisibleToRegularUsers { get; set; }

        /// <summary>
        /// Gets or sets visibility to friends.
        /// </summary>
        public bool? VisibleToFriends { get; set; }
    }

    /// <summary>
    /// Request model for adding a list item.
    /// </summary>
    public class AddListItemRequest
    {
        /// <summary>
        /// Gets or sets the Jellyfin item ID.
        /// </summary>
        public Guid? ItemId { get; set; }

        /// <summary>
        /// Gets or sets the IMDB ID (for external items).
        /// </summary>
        public string? ImdbId { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the image URL.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the overview.
        /// </summary>
        public string? Overview { get; set; }

        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets genres as JSON.
        /// </summary>
        public string? Genres { get; set; }

        /// <summary>
        /// Gets or sets the note.
        /// </summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Request model for updating a list item.
    /// </summary>
    public class UpdateListItemRequest
    {
        /// <summary>
        /// Gets or sets the note.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Gets or sets the position.
        /// </summary>
        public int? Position { get; set; }
    }

    /// <summary>
    /// Request model for reordering items.
    /// </summary>
    public class ReorderItemsRequest
    {
        /// <summary>
        /// Gets or sets the item IDs in new order.
        /// </summary>
        [Required]
        public System.Collections.Generic.List<Guid> ItemIds { get; set; } = new System.Collections.Generic.List<Guid>();
    }
}
