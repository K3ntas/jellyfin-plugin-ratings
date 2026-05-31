using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Data
{
    /// <summary>
    /// Repository for managing social features data (profiles, friends, etc.).
    /// </summary>
    public class SocialRepository
    {
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<SocialRepository> _logger;
        private readonly string _dataPath;
        private readonly object _lock = new object();

        // Semaphores for thread-safe writes
        private static readonly SemaphoreSlim _profilesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _friendRequestsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _friendshipsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _notificationsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _onlineStatusWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _followsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _profileLikesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _mediaListsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _listItemsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _profileStylesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _imdbCacheWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _featuredReviewsWriteLock = new(1, 1);

        // In-memory storage
        private Dictionary<Guid, UserProfile> _profiles;
        private List<FriendRequest> _friendRequests;
        private List<Friendship> _friendships;
        private List<SocialNotification> _notifications;
        private Dictionary<Guid, UserOnlineStatus> _onlineStatuses;

        // New social features storage
        private List<UserFollow> _follows;
        private List<ProfileLike> _profileLikes;
        private List<UserMediaList> _mediaLists;
        private List<UserMediaListItem> _listItems;
        private Dictionary<Guid, UserProfileStyle> _profileStyles;
        private Dictionary<string, ImdbCacheItem> _imdbCache;
        private List<FeaturedReview> _featuredReviews;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialRepository"/> class.
        /// </summary>
        /// <param name="appPaths">Application paths.</param>
        /// <param name="logger">Logger instance.</param>
        public SocialRepository(IApplicationPaths appPaths, ILogger<SocialRepository> logger)
        {
            _appPaths = appPaths;
            _logger = logger;
            _dataPath = Path.Combine(_appPaths.DataPath, "ratings", "social");
            _profiles = new Dictionary<Guid, UserProfile>();
            _friendRequests = new List<FriendRequest>();
            _friendships = new List<Friendship>();
            _notifications = new List<SocialNotification>();
            _onlineStatuses = new Dictionary<Guid, UserOnlineStatus>();

            // Initialize new collections
            _follows = new List<UserFollow>();
            _profileLikes = new List<ProfileLike>();
            _mediaLists = new List<UserMediaList>();
            _listItems = new List<UserMediaListItem>();
            _profileStyles = new Dictionary<Guid, UserProfileStyle>();
            _imdbCache = new Dictionary<string, ImdbCacheItem>();
            _featuredReviews = new List<FeaturedReview>();

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogInformation("[Social] Created social data directory: {Path}", _dataPath);
            }

            LoadProfiles();
            LoadFriendRequests();
            LoadFriendships();
            LoadNotifications();
            LoadOnlineStatuses();
            LoadBlockedUsers();
            LoadFollows();
            LoadProfileLikes();
            LoadMediaLists();
            LoadListItems();
            LoadProfileStyles();
            LoadImdbCache();
            LoadFeaturedReviews();

            _logger.LogInformation("[Social] Repository initialized - Profiles: {Profiles}, Requests: {Requests}, Friendships: {Friendships}, OnlineStatuses: {OnlineStatuses}, BlockedUsers: {BlockedUsers}, Follows: {Follows}, ProfileLikes: {ProfileLikes}, MediaLists: {MediaLists}",
                _profiles.Count, _friendRequests.Count, _friendships.Count, _onlineStatuses.Count, _blockedUsers.Count, _follows.Count, _profileLikes.Count, _mediaLists.Count);
        }

        /// <summary>
        /// Gets debug information about the social repository state.
        /// </summary>
        /// <returns>Debug info object.</returns>
        public object GetDebugInfo()
        {
            lock (_lock)
            {
                return new
                {
                    Initialized = true,
                    DataPath = _dataPath,
                    ProfileCount = _profiles.Count,
                    FriendRequestCount = _friendRequests.Count,
                    FriendshipCount = _friendships.Count,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        #region Profiles

        /// <summary>
        /// Gets a user profile by user ID.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The user profile or null.</returns>
        public UserProfile? GetProfile(Guid userId)
        {
            lock (_lock)
            {
                return _profiles.TryGetValue(userId, out var profile) ? profile : null;
            }
        }

        /// <summary>
        /// Creates or updates a user profile.
        /// </summary>
        /// <param name="profile">The profile to save.</param>
        /// <returns>The saved profile.</returns>
        public async Task<UserProfile> SaveProfileAsync(UserProfile profile)
        {
            lock (_lock)
            {
                profile.UpdatedAt = DateTime.UtcNow;
                _profiles[profile.UserId] = profile;
            }

            await SaveProfilesAsync().ConfigureAwait(false);
            _logger.LogDebug("[Social] Saved profile for user {UserId}", profile.UserId);
            return profile;
        }

        /// <summary>
        /// Gets or creates a profile for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="username">The username.</param>
        /// <returns>The existing or new profile.</returns>
        public async Task<UserProfile> GetOrCreateProfileAsync(Guid userId, string username)
        {
            var existing = GetProfile(userId);
            if (existing != null)
            {
                // Update username if changed
                if (existing.Username != username)
                {
                    existing.Username = username;
                    await SaveProfileAsync(existing).ConfigureAwait(false);
                }
                return existing;
            }

            var newProfile = new UserProfile
            {
                UserId = userId,
                Username = username
            };

            return await SaveProfileAsync(newProfile).ConfigureAwait(false);
        }

        private void LoadProfiles()
        {
            try
            {
                var file = Path.Combine(_dataPath, "profiles.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var profiles = JsonSerializer.Deserialize<List<UserProfile>>(json, options);
                    if (profiles != null)
                    {
                        _profiles = profiles.ToDictionary(p => p.UserId);
                        _logger.LogInformation("[Social] Loaded {Count} profiles from disk", _profiles.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading profiles from disk");
            }
        }

        private async Task SaveProfilesAsync()
        {
            await _profilesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "profiles.json");
                List<UserProfile> snapshot;
                lock (_lock)
                {
                    snapshot = _profiles.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving profiles to disk");
            }
            finally
            {
                _profilesWriteLock.Release();
            }
        }

        #endregion

        #region Friend Requests

        /// <summary>
        /// Creates a friend request.
        /// </summary>
        /// <param name="request">The friend request.</param>
        /// <returns>The created request.</returns>
        public async Task<FriendRequest> CreateFriendRequestAsync(FriendRequest request)
        {
            lock (_lock)
            {
                _friendRequests.Add(request);
            }

            await SaveFriendRequestsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Friend request created: {From} -> {To}", request.FromUsername, request.ToUsername);
            return request;
        }

        /// <summary>
        /// Gets incoming friend requests for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of pending incoming requests.</returns>
        public List<FriendRequest> GetIncomingRequests(Guid userId)
        {
            lock (_lock)
            {
                return _friendRequests
                    .Where(r => r.ToUserId == userId && r.Status == "pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets outgoing friend requests for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of pending outgoing requests.</returns>
        public List<FriendRequest> GetOutgoingRequests(Guid userId)
        {
            lock (_lock)
            {
                return _friendRequests
                    .Where(r => r.FromUserId == userId && r.Status == "pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets a friend request by ID.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The friend request or null.</returns>
        public FriendRequest? GetFriendRequest(Guid requestId)
        {
            lock (_lock)
            {
                return _friendRequests.FirstOrDefault(r => r.Id == requestId);
            }
        }

        /// <summary>
        /// Checks if a pending request exists between two users.
        /// </summary>
        /// <param name="fromUserId">Sender user ID.</param>
        /// <param name="toUserId">Target user ID.</param>
        /// <returns>True if pending request exists.</returns>
        public bool HasPendingRequest(Guid fromUserId, Guid toUserId)
        {
            lock (_lock)
            {
                return _friendRequests.Any(r =>
                    r.FromUserId == fromUserId &&
                    r.ToUserId == toUserId &&
                    r.Status == "pending");
            }
        }

        /// <summary>
        /// Updates a friend request status.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="status">New status (accepted/rejected).</param>
        /// <returns>Task.</returns>
        public async Task UpdateFriendRequestStatusAsync(Guid requestId, string status)
        {
            lock (_lock)
            {
                var request = _friendRequests.FirstOrDefault(r => r.Id == requestId);
                if (request != null)
                {
                    request.Status = status;
                    request.ResolvedAt = DateTime.UtcNow;
                }
            }

            await SaveFriendRequestsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Friend request {Id} status updated to {Status}", requestId, status);
        }

        /// <summary>
        /// Deletes a friend request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>Task.</returns>
        public async Task DeleteFriendRequestAsync(Guid requestId)
        {
            lock (_lock)
            {
                _friendRequests.RemoveAll(r => r.Id == requestId);
            }

            await SaveFriendRequestsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Friend request {Id} deleted", requestId);
        }

        private void LoadFriendRequests()
        {
            try
            {
                var file = Path.Combine(_dataPath, "friend_requests.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var requests = JsonSerializer.Deserialize<List<FriendRequest>>(json, options);
                    if (requests != null)
                    {
                        _friendRequests = requests;
                        _logger.LogInformation("[Social] Loaded {Count} friend requests from disk", _friendRequests.Count);
                        foreach (var r in _friendRequests)
                        {
                            _logger.LogDebug("[Social] Loaded request: {From} -> {To}", r.FromUsername, r.ToUsername);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading friend requests from disk");
            }
        }

        private async Task SaveFriendRequestsAsync()
        {
            await _friendRequestsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "friend_requests.json");
                List<FriendRequest> snapshot;
                lock (_lock)
                {
                    snapshot = _friendRequests.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving friend requests to disk");
            }
            finally
            {
                _friendRequestsWriteLock.Release();
            }
        }

        #endregion

        #region Friendships

        /// <summary>
        /// Creates a friendship between two users.
        /// </summary>
        /// <param name="userId1">First user ID.</param>
        /// <param name="userId2">Second user ID.</param>
        /// <returns>The created friendship.</returns>
        public async Task<Friendship> CreateFriendshipAsync(Guid userId1, Guid userId2)
        {
            var friendship = new Friendship
            {
                UserId1 = userId1,
                UserId2 = userId2
            };

            lock (_lock)
            {
                _friendships.Add(friendship);
            }

            await SaveFriendshipsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Friendship created between {User1} and {User2}", userId1, userId2);
            return friendship;
        }

        /// <summary>
        /// Gets all friendships for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of friendships.</returns>
        public List<Friendship> GetFriendships(Guid userId)
        {
            lock (_lock)
            {
                return _friendships
                    .Where(f => f.UserId1 == userId || f.UserId2 == userId)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets friend user IDs for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of friend user IDs.</returns>
        public List<Guid> GetFriendIds(Guid userId)
        {
            lock (_lock)
            {
                return _friendships
                    .Where(f => f.UserId1 == userId || f.UserId2 == userId)
                    .Select(f => f.UserId1 == userId ? f.UserId2 : f.UserId1)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if two users are friends.
        /// </summary>
        /// <param name="userId1">First user ID.</param>
        /// <param name="userId2">Second user ID.</param>
        /// <returns>True if friends.</returns>
        public bool AreFriends(Guid userId1, Guid userId2)
        {
            lock (_lock)
            {
                return _friendships.Any(f =>
                    (f.UserId1 == userId1 && f.UserId2 == userId2) ||
                    (f.UserId1 == userId2 && f.UserId2 == userId1));
            }
        }

        /// <summary>
        /// Deletes a friendship between two users.
        /// </summary>
        /// <param name="userId1">First user ID.</param>
        /// <param name="userId2">Second user ID.</param>
        /// <returns>Task.</returns>
        public async Task DeleteFriendshipAsync(Guid userId1, Guid userId2)
        {
            lock (_lock)
            {
                _friendships.RemoveAll(f =>
                    (f.UserId1 == userId1 && f.UserId2 == userId2) ||
                    (f.UserId1 == userId2 && f.UserId2 == userId1));
            }

            await SaveFriendshipsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Friendship deleted between {User1} and {User2}", userId1, userId2);
        }

        private void LoadFriendships()
        {
            try
            {
                var file = Path.Combine(_dataPath, "friendships.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var friendships = JsonSerializer.Deserialize<List<Friendship>>(json, options);
                    if (friendships != null)
                    {
                        _friendships = friendships;
                        _logger.LogInformation("[Social] Loaded {Count} friendships from disk", _friendships.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading friendships from disk");
            }
        }

        private async Task SaveFriendshipsAsync()
        {
            await _friendshipsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "friendships.json");
                List<Friendship> snapshot;
                lock (_lock)
                {
                    snapshot = _friendships.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving friendships to disk");
            }
            finally
            {
                _friendshipsWriteLock.Release();
            }
        }

        #endregion

        #region Notifications

        /// <summary>
        /// Creates a notification for a user.
        /// </summary>
        /// <param name="notification">The notification.</param>
        /// <returns>The created notification.</returns>
        public async Task<SocialNotification> CreateNotificationAsync(SocialNotification notification)
        {
            lock (_lock)
            {
                _notifications.Add(notification);
            }

            await SaveNotificationsAsync().ConfigureAwait(false);
            _logger.LogDebug("[Social] Notification created for user {UserId}: {Type}", notification.UserId, notification.Type);
            return notification;
        }

        /// <summary>
        /// Creates a friend request notification.
        /// </summary>
        /// <param name="toUserId">Target user ID.</param>
        /// <param name="fromUsername">Sender username.</param>
        /// <param name="fromUserId">Sender user ID.</param>
        /// <returns>Task.</returns>
        public async Task CreateFriendRequestNotificationAsync(Guid toUserId, string fromUsername, Guid fromUserId)
        {
            var notification = new SocialNotification
            {
                UserId = toUserId,
                Type = "FriendRequest",
                Title = "Friend Request",
                Message = $"{fromUsername} sent you a friend request"
            };
            notification.Data["fromUserId"] = fromUserId.ToString();
            notification.Data["fromUsername"] = fromUsername;

            await CreateNotificationAsync(notification).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a friend accepted notification.
        /// </summary>
        /// <param name="toUserId">Original sender user ID.</param>
        /// <param name="accepterUsername">Username of who accepted.</param>
        /// <param name="accepterUserId">User ID of who accepted.</param>
        /// <returns>Task.</returns>
        public async Task CreateFriendAcceptedNotificationAsync(Guid toUserId, string accepterUsername, Guid accepterUserId)
        {
            var notification = new SocialNotification
            {
                UserId = toUserId,
                Type = "FriendAccepted",
                Title = "Friend Request Accepted",
                Message = $"{accepterUsername} accepted your friend request"
            };
            notification.Data["userId"] = accepterUserId.ToString();
            notification.Data["username"] = accepterUsername;

            await CreateNotificationAsync(notification).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets notifications for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="unreadOnly">Only return unread notifications.</param>
        /// <param name="limit">Maximum number to return.</param>
        /// <param name="offset">Offset for pagination.</param>
        /// <returns>List of notifications.</returns>
        public List<SocialNotification> GetNotifications(Guid userId, bool unreadOnly = false, int limit = 20, int offset = 0)
        {
            lock (_lock)
            {
                var query = _notifications.Where(n => n.UserId == userId);

                if (unreadOnly)
                {
                    query = query.Where(n => !n.IsRead);
                }

                return query
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip(offset)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets unread notification count for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Unread count.</returns>
        public int GetUnreadNotificationCount(Guid userId)
        {
            lock (_lock)
            {
                return _notifications.Count(n => n.UserId == userId && !n.IsRead);
            }
        }

        /// <summary>
        /// Marks a notification as read.
        /// </summary>
        /// <param name="notificationId">The notification ID.</param>
        /// <param name="userId">The user ID (for validation).</param>
        /// <returns>True if marked, false if not found or unauthorized.</returns>
        public async Task<bool> MarkNotificationAsReadAsync(Guid notificationId, Guid userId)
        {
            bool found = false;
            lock (_lock)
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == notificationId && n.UserId == userId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    found = true;
                }
            }

            if (found)
            {
                await SaveNotificationsAsync().ConfigureAwait(false);
            }

            return found;
        }

        /// <summary>
        /// Marks all notifications as read for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Number of notifications marked.</returns>
        public async Task<int> MarkAllNotificationsAsReadAsync(Guid userId)
        {
            int count = 0;
            lock (_lock)
            {
                foreach (var n in _notifications.Where(n => n.UserId == userId && !n.IsRead))
                {
                    n.IsRead = true;
                    count++;
                }
            }

            if (count > 0)
            {
                await SaveNotificationsAsync().ConfigureAwait(false);
            }

            return count;
        }

        /// <summary>
        /// Deletes a notification.
        /// </summary>
        /// <param name="notificationId">The notification ID.</param>
        /// <param name="userId">The user ID (for validation).</param>
        /// <returns>True if deleted.</returns>
        public async Task<bool> DeleteNotificationAsync(Guid notificationId, Guid userId)
        {
            int removed;
            lock (_lock)
            {
                removed = _notifications.RemoveAll(n => n.Id == notificationId && n.UserId == userId);
            }

            if (removed > 0)
            {
                await SaveNotificationsAsync().ConfigureAwait(false);
            }

            return removed > 0;
        }

        private void LoadNotifications()
        {
            try
            {
                var file = Path.Combine(_dataPath, "notifications.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var notifications = JsonSerializer.Deserialize<List<SocialNotification>>(json, options);
                    if (notifications != null)
                    {
                        _notifications = notifications;
                        _logger.LogInformation("[Social] Loaded {Count} notifications from disk", _notifications.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading notifications from disk");
            }
        }

        private async Task SaveNotificationsAsync()
        {
            await _notificationsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "notifications.json");
                List<SocialNotification> snapshot;
                lock (_lock)
                {
                    snapshot = _notifications.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving notifications to disk");
            }
            finally
            {
                _notificationsWriteLock.Release();
            }
        }

        #endregion

        #region Online Status

        /// <summary>
        /// Updates a user's heartbeat (marks them as online).
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="watching">Optional currently watching info.</param>
        /// <returns>The updated online status.</returns>
        public async Task<UserOnlineStatus> UpdateHeartbeatAsync(Guid userId, CurrentlyWatching? watching = null)
        {
            UserOnlineStatus status;

            lock (_lock)
            {
                if (!_onlineStatuses.TryGetValue(userId, out status!))
                {
                    status = new UserOnlineStatus
                    {
                        UserId = userId,
                        LastSeen = DateTime.UtcNow,
                        LastHeartbeat = DateTime.UtcNow
                    };
                    _onlineStatuses[userId] = status;
                }
                else
                {
                    status.LastHeartbeat = DateTime.UtcNow;
                    status.LastSeen = DateTime.UtcNow;

                    // Clear ForceOffline only if it's been more than 10 seconds
                    // This prevents race conditions during page unload
                    if (status.ForceOffline && (DateTime.UtcNow - status.ForceOfflineAt).TotalSeconds >= 10)
                    {
                        status.ForceOffline = false;
                    }
                }

                status.Watching = watching;
                status.Status = status.GetEffectiveStatus();
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
            return status;
        }

        /// <summary>
        /// Updates heartbeat ONLY - does not touch watching info.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The updated online status.</returns>
        public async Task<UserOnlineStatus> UpdateHeartbeatOnlyAsync(Guid userId)
        {
            UserOnlineStatus status;

            lock (_lock)
            {
                if (!_onlineStatuses.TryGetValue(userId, out status!))
                {
                    status = new UserOnlineStatus
                    {
                        UserId = userId,
                        LastSeen = DateTime.UtcNow,
                        LastHeartbeat = DateTime.UtcNow
                    };
                    _onlineStatuses[userId] = status;
                }
                else
                {
                    status.LastHeartbeat = DateTime.UtcNow;
                    status.LastSeen = DateTime.UtcNow;

                    // Clear ForceOffline only if it's been more than 10 seconds
                    if (status.ForceOffline && (DateTime.UtcNow - status.ForceOfflineAt).TotalSeconds >= 10)
                    {
                        status.ForceOffline = false;
                    }
                }

                // Don't touch Watching - keep whatever was there
                status.Status = status.GetEffectiveStatus();
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
            return status;
        }

        /// <summary>
        /// Sets what the user is currently watching.
        /// Also clears ForceOffline and sets user Online (handles race condition with beforeunload).
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="watching">The watching info.</param>
        public async Task SetWatchingOnlyAsync(Guid userId, CurrentlyWatching watching)
        {
            lock (_lock)
            {
                if (!_onlineStatuses.TryGetValue(userId, out var status))
                {
                    status = new UserOnlineStatus
                    {
                        UserId = userId,
                        LastSeen = DateTime.UtcNow,
                        LastHeartbeat = DateTime.UtcNow,
                        Status = "Online"
                    };
                    _onlineStatuses[userId] = status;
                }

                // Update watching
                status.Watching = watching;

                // Clear ForceOffline - user is actively watching, not offline
                // This handles the race condition where beforeunload fires during navigation to player
                status.ForceOffline = false;
                status.LastHeartbeat = DateTime.UtcNow;
                status.LastSeen = DateTime.UtcNow;
                status.Status = "Online";
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Clears what the user is watching. Does NOT affect online status at all.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        public async Task ClearWatchingOnlyAsync(Guid userId)
        {
            lock (_lock)
            {
                if (_onlineStatuses.TryGetValue(userId, out var status))
                {
                    // ONLY clear watching - never touch Status or heartbeat
                    status.Watching = null;
                }
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Sets a user's status to offline (called on logout).
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The updated online status.</returns>
        public UserOnlineStatus? SetUserOffline(Guid userId)
        {
            lock (_lock)
            {
                if (_onlineStatuses.TryGetValue(userId, out var status))
                {
                    // Set ForceOffline flag with timestamp - sticky for 10 seconds
                    status.ForceOffline = true;
                    status.ForceOfflineAt = DateTime.UtcNow;
                    status.Watching = null;
                    status.Status = "Offline";
                    _logger.LogInformation("[Social] User {UserId} set to ForceOffline", userId);
                    return status;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets a user's online status.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The online status or null if not found.</returns>
        public UserOnlineStatus? GetOnlineStatus(Guid userId)
        {
            lock (_lock)
            {
                if (_onlineStatuses.TryGetValue(userId, out var status))
                {
                    // Update the effective status before returning
                    status.Status = status.GetEffectiveStatus();
                    return status;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets online statuses for multiple users (friends).
        /// </summary>
        /// <param name="userIds">List of user IDs.</param>
        /// <returns>Dictionary of user ID to online status.</returns>
        public Dictionary<Guid, UserOnlineStatus> GetOnlineStatuses(IEnumerable<Guid> userIds)
        {
            var result = new Dictionary<Guid, UserOnlineStatus>();

            lock (_lock)
            {
                foreach (var userId in userIds)
                {
                    if (_onlineStatuses.TryGetValue(userId, out var status))
                    {
                        status.Status = status.GetEffectiveStatus();
                        result[userId] = status;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Sets a user's manual status override.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="status">The status to set (Online, Away, DoNotDisturb, Invisible) or null to clear.</param>
        /// <returns>Task.</returns>
        public async Task SetManualStatusAsync(Guid userId, string? status)
        {
            lock (_lock)
            {
                if (!_onlineStatuses.TryGetValue(userId, out var onlineStatus))
                {
                    onlineStatus = new UserOnlineStatus
                    {
                        UserId = userId,
                        LastSeen = DateTime.UtcNow,
                        LastHeartbeat = DateTime.UtcNow
                    };
                    _onlineStatuses[userId] = onlineStatus;
                }

                onlineStatus.ManualStatus = status;
                onlineStatus.Status = onlineStatus.GetEffectiveStatus();
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] User {UserId} set manual status to {Status}", userId, status ?? "auto");
        }

        private void LoadOnlineStatuses()
        {
            try
            {
                var file = Path.Combine(_dataPath, "online_statuses.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var statuses = JsonSerializer.Deserialize<List<UserOnlineStatus>>(json, options);
                    if (statuses != null)
                    {
                        _onlineStatuses = statuses.ToDictionary(s => s.UserId);
                        _logger.LogInformation("[Social] Loaded {Count} online statuses from disk", _onlineStatuses.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading online statuses from disk");
            }
        }

        private async Task SaveOnlineStatusesAsync()
        {
            await _onlineStatusWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "online_statuses.json");
                List<UserOnlineStatus> snapshot;
                lock (_lock)
                {
                    snapshot = _onlineStatuses.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving online statuses to disk");
            }
            finally
            {
                _onlineStatusWriteLock.Release();
            }
        }

        #endregion

        #region Block System

        private List<BlockedUser> _blockedUsers = new();
        private readonly string _blockedUsersFile;
        private readonly SemaphoreSlim _blockedUsersLock = new(1, 1);

        /// <summary>
        /// Loads blocked users from disk.
        /// </summary>
        private void LoadBlockedUsers()
        {
            try
            {
                var file = Path.Combine(_dataPath, "blocked_users.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _blockedUsers = JsonSerializer.Deserialize<List<BlockedUser>>(json, options) ?? new List<BlockedUser>();
                    _logger.LogInformation("[Social] Loaded {Count} blocked user entries", _blockedUsers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading blocked users");
                _blockedUsers = new List<BlockedUser>();
            }
        }

        /// <summary>
        /// Saves blocked users to disk.
        /// </summary>
        private async Task SaveBlockedUsersAsync()
        {
            await _blockedUsersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "blocked_users.json");
                var json = JsonSerializer.Serialize(_blockedUsers, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            finally
            {
                _blockedUsersLock.Release();
            }
        }

        /// <summary>
        /// Blocks a user.
        /// </summary>
        /// <param name="userId">The user who is blocking.</param>
        /// <param name="blockedUserId">The user to block.</param>
        /// <returns>The block record.</returns>
        public async Task<BlockedUser> BlockUserAsync(Guid userId, Guid blockedUserId)
        {
            // Check if already blocked
            var existing = _blockedUsers.FirstOrDefault(b => b.UserId == userId && b.BlockedUserId == blockedUserId);
            if (existing != null)
            {
                return existing;
            }

            var block = new BlockedUser
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BlockedUserId = blockedUserId,
                CreatedAt = DateTime.UtcNow
            };

            _blockedUsers.Add(block);
            await SaveBlockedUsersAsync().ConfigureAwait(false);

            // Also remove any existing friendship
            await DeleteFriendshipAsync(userId, blockedUserId).ConfigureAwait(false);

            // Delete any pending friend requests between them
            var requests = _friendRequests.Where(r =>
                (r.FromUserId == userId && r.ToUserId == blockedUserId) ||
                (r.FromUserId == blockedUserId && r.ToUserId == userId)).ToList();

            foreach (var req in requests)
            {
                _friendRequests.Remove(req);
            }

            if (requests.Count > 0)
            {
                await SaveFriendRequestsAsync().ConfigureAwait(false);
            }

            _logger.LogInformation("[Social] User {UserId} blocked {BlockedUserId}", userId, blockedUserId);
            return block;
        }

        /// <summary>
        /// Unblocks a user.
        /// </summary>
        /// <param name="userId">The user who is unblocking.</param>
        /// <param name="blockedUserId">The user to unblock.</param>
        /// <returns>True if unblocked, false if wasn't blocked.</returns>
        public async Task<bool> UnblockUserAsync(Guid userId, Guid blockedUserId)
        {
            var block = _blockedUsers.FirstOrDefault(b => b.UserId == userId && b.BlockedUserId == blockedUserId);
            if (block == null)
            {
                return false;
            }

            _blockedUsers.Remove(block);
            await SaveBlockedUsersAsync().ConfigureAwait(false);

            _logger.LogInformation("[Social] User {UserId} unblocked {BlockedUserId}", userId, blockedUserId);
            return true;
        }

        /// <summary>
        /// Gets the list of users blocked by a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of blocked user records.</returns>
        public List<BlockedUser> GetBlockedUsers(Guid userId)
        {
            return _blockedUsers.Where(b => b.UserId == userId).ToList();
        }

        /// <summary>
        /// Checks if a user has blocked another user.
        /// </summary>
        /// <param name="userId">The user who may have blocked.</param>
        /// <param name="targetUserId">The user who may be blocked.</param>
        /// <returns>True if blocked.</returns>
        public bool HasBlocked(Guid userId, Guid targetUserId)
        {
            return _blockedUsers.Any(b => b.UserId == userId && b.BlockedUserId == targetUserId);
        }

        /// <summary>
        /// Checks if either user has blocked the other.
        /// </summary>
        /// <param name="userId1">First user.</param>
        /// <param name="userId2">Second user.</param>
        /// <returns>True if either has blocked the other.</returns>
        public bool IsBlockedEitherWay(Guid userId1, Guid userId2)
        {
            return _blockedUsers.Any(b =>
                (b.UserId == userId1 && b.BlockedUserId == userId2) ||
                (b.UserId == userId2 && b.BlockedUserId == userId1));
        }

        #endregion

        #region User Follows

        /// <summary>
        /// Creates a follow relationship.
        /// </summary>
        /// <param name="followerId">The user who is following.</param>
        /// <param name="followingId">The user being followed.</param>
        /// <returns>The created follow.</returns>
        public async Task<UserFollow> FollowUserAsync(Guid followerId, Guid followingId)
        {
            var follow = new UserFollow
            {
                FollowerId = followerId,
                FollowingId = followingId
            };

            lock (_lock)
            {
                // Check if already following
                if (!_follows.Any(f => f.FollowerId == followerId && f.FollowingId == followingId))
                {
                    _follows.Add(follow);
                }
                else
                {
                    return _follows.First(f => f.FollowerId == followerId && f.FollowingId == followingId);
                }
            }

            await SaveFollowsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] User {Follower} followed {Following}", followerId, followingId);
            return follow;
        }

        /// <summary>
        /// Removes a follow relationship.
        /// </summary>
        /// <param name="followerId">The user who is following.</param>
        /// <param name="followingId">The user being followed.</param>
        /// <returns>True if unfollowed successfully.</returns>
        public async Task<bool> UnfollowUserAsync(Guid followerId, Guid followingId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _follows.RemoveAll(f => f.FollowerId == followerId && f.FollowingId == followingId) > 0;
            }

            if (removed)
            {
                await SaveFollowsAsync().ConfigureAwait(false);
                _logger.LogInformation("[Social] User {Follower} unfollowed {Following}", followerId, followingId);
            }
            return removed;
        }

        /// <summary>
        /// Checks if a user is following another user.
        /// </summary>
        public bool IsFollowing(Guid followerId, Guid followingId)
        {
            lock (_lock)
            {
                return _follows.Any(f => f.FollowerId == followerId && f.FollowingId == followingId);
            }
        }

        /// <summary>
        /// Gets followers of a user.
        /// </summary>
        public List<UserFollow> GetFollowers(Guid userId)
        {
            lock (_lock)
            {
                return _follows.Where(f => f.FollowingId == userId).OrderByDescending(f => f.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Gets users that a user is following.
        /// </summary>
        public List<UserFollow> GetFollowing(Guid userId)
        {
            lock (_lock)
            {
                return _follows.Where(f => f.FollowerId == userId).OrderByDescending(f => f.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Gets follower count for a user.
        /// </summary>
        public int GetFollowerCount(Guid userId)
        {
            lock (_lock)
            {
                return _follows.Count(f => f.FollowingId == userId);
            }
        }

        /// <summary>
        /// Gets following count for a user.
        /// </summary>
        public int GetFollowingCount(Guid userId)
        {
            lock (_lock)
            {
                return _follows.Count(f => f.FollowerId == userId);
            }
        }

        /// <summary>
        /// Checks if two users are mutual followers (friends).
        /// </summary>
        public bool AreMutualFollowers(Guid userId1, Guid userId2)
        {
            lock (_lock)
            {
                return _follows.Any(f => f.FollowerId == userId1 && f.FollowingId == userId2) &&
                       _follows.Any(f => f.FollowerId == userId2 && f.FollowingId == userId1);
            }
        }

        private void LoadFollows()
        {
            try
            {
                var file = Path.Combine(_dataPath, "follows.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var follows = JsonSerializer.Deserialize<List<UserFollow>>(json, options);
                    if (follows != null)
                    {
                        _follows = follows;
                        _logger.LogInformation("[Social] Loaded {Count} follows from disk", _follows.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading follows from disk");
            }
        }

        private async Task SaveFollowsAsync()
        {
            await _followsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "follows.json");
                List<UserFollow> snapshot;
                lock (_lock)
                {
                    snapshot = _follows.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving follows to disk");
            }
            finally
            {
                _followsWriteLock.Release();
            }
        }

        #endregion

        #region Profile Likes

        /// <summary>
        /// Likes a user's profile.
        /// </summary>
        public async Task<ProfileLike> LikeProfileAsync(Guid profileUserId, Guid likerUserId)
        {
            var like = new ProfileLike
            {
                ProfileUserId = profileUserId,
                LikerUserId = likerUserId
            };

            lock (_lock)
            {
                // Check if already liked
                if (!_profileLikes.Any(l => l.ProfileUserId == profileUserId && l.LikerUserId == likerUserId))
                {
                    _profileLikes.Add(like);
                }
                else
                {
                    return _profileLikes.First(l => l.ProfileUserId == profileUserId && l.LikerUserId == likerUserId);
                }
            }

            await SaveProfileLikesAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] User {Liker} liked profile of {Profile}", likerUserId, profileUserId);
            return like;
        }

        /// <summary>
        /// Unlikes a user's profile.
        /// </summary>
        public async Task<bool> UnlikeProfileAsync(Guid profileUserId, Guid likerUserId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _profileLikes.RemoveAll(l => l.ProfileUserId == profileUserId && l.LikerUserId == likerUserId) > 0;
            }

            if (removed)
            {
                await SaveProfileLikesAsync().ConfigureAwait(false);
                _logger.LogInformation("[Social] User {Liker} unliked profile of {Profile}", likerUserId, profileUserId);
            }
            return removed;
        }

        /// <summary>
        /// Checks if a user has liked a profile.
        /// </summary>
        public bool HasLikedProfile(Guid profileUserId, Guid likerUserId)
        {
            lock (_lock)
            {
                return _profileLikes.Any(l => l.ProfileUserId == profileUserId && l.LikerUserId == likerUserId);
            }
        }

        /// <summary>
        /// Gets profile like count.
        /// </summary>
        public int GetProfileLikeCount(Guid profileUserId)
        {
            lock (_lock)
            {
                return _profileLikes.Count(l => l.ProfileUserId == profileUserId);
            }
        }

        /// <summary>
        /// Gets list of users who liked a profile.
        /// </summary>
        public List<ProfileLike> GetProfileLikers(Guid profileUserId)
        {
            lock (_lock)
            {
                return _profileLikes.Where(l => l.ProfileUserId == profileUserId).OrderByDescending(l => l.CreatedAt).ToList();
            }
        }

        private void LoadProfileLikes()
        {
            try
            {
                var file = Path.Combine(_dataPath, "profile_likes.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var likes = JsonSerializer.Deserialize<List<ProfileLike>>(json, options);
                    if (likes != null)
                    {
                        _profileLikes = likes;
                        _logger.LogInformation("[Social] Loaded {Count} profile likes from disk", _profileLikes.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading profile likes from disk");
            }
        }

        private async Task SaveProfileLikesAsync()
        {
            await _profileLikesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "profile_likes.json");
                List<ProfileLike> snapshot;
                lock (_lock)
                {
                    snapshot = _profileLikes.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving profile likes to disk");
            }
            finally
            {
                _profileLikesWriteLock.Release();
            }
        }

        #endregion

        #region Media Lists

        /// <summary>
        /// Creates a new media list.
        /// </summary>
        public async Task<UserMediaList> CreateListAsync(UserMediaList list)
        {
            lock (_lock)
            {
                // Check max 5 lists per user
                var userListCount = _mediaLists.Count(l => l.UserId == list.UserId && !l.IsDeleted);
                if (userListCount >= 5)
                {
                    throw new InvalidOperationException("Maximum of 5 lists per user allowed.");
                }

                list.SortOrder = userListCount;
                _mediaLists.Add(list);
            }

            await SaveMediaListsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Created list '{Title}' for user {UserId}", list.Title, list.UserId);
            return list;
        }

        /// <summary>
        /// Updates a media list.
        /// </summary>
        public async Task<UserMediaList> UpdateListAsync(UserMediaList list)
        {
            lock (_lock)
            {
                var existing = _mediaLists.FirstOrDefault(l => l.Id == list.Id);
                if (existing != null)
                {
                    var index = _mediaLists.IndexOf(existing);
                    list.UpdatedAt = DateTime.UtcNow;
                    _mediaLists[index] = list;
                }
            }

            await SaveMediaListsAsync().ConfigureAwait(false);
            return list;
        }

        /// <summary>
        /// Deletes a media list (soft delete).
        /// </summary>
        public async Task<bool> DeleteListAsync(Guid listId)
        {
            lock (_lock)
            {
                var list = _mediaLists.FirstOrDefault(l => l.Id == listId);
                if (list != null)
                {
                    list.IsDeleted = true;
                    list.UpdatedAt = DateTime.UtcNow;
                }
            }

            await SaveMediaListsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Deleted list {ListId}", listId);
            return true;
        }

        /// <summary>
        /// Gets a list by ID.
        /// </summary>
        public UserMediaList? GetList(Guid listId)
        {
            lock (_lock)
            {
                return _mediaLists.FirstOrDefault(l => l.Id == listId && !l.IsDeleted);
            }
        }

        /// <summary>
        /// Gets all lists for a user.
        /// </summary>
        public List<UserMediaList> GetUserLists(Guid userId, bool includeDeleted = false)
        {
            lock (_lock)
            {
                return _mediaLists
                    .Where(l => l.UserId == userId && (includeDeleted || !l.IsDeleted))
                    .OrderBy(l => l.SortOrder)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets visible lists for a user (respecting privacy).
        /// </summary>
        public List<UserMediaList> GetVisibleLists(Guid profileUserId, Guid viewerUserId, bool isFriend)
        {
            lock (_lock)
            {
                return _mediaLists
                    .Where(l => l.UserId == profileUserId && !l.IsDeleted &&
                        (l.UserId == viewerUserId || // Owner sees all
                         (isFriend && l.VisibleToFriends) ||
                         (!isFriend && l.VisibleToRegularUsers)))
                    .OrderBy(l => l.SortOrder)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets list count for a user.
        /// </summary>
        public int GetListCount(Guid userId)
        {
            lock (_lock)
            {
                return _mediaLists.Count(l => l.UserId == userId && !l.IsDeleted);
            }
        }

        /// <summary>
        /// Clones a list for another user.
        /// </summary>
        public async Task<UserMediaList> CloneListAsync(Guid sourceListId, Guid newOwnerId, string newOwnerUsername)
        {
            UserMediaList? sourceList;
            List<UserMediaListItem> sourceItems;

            lock (_lock)
            {
                sourceList = _mediaLists.FirstOrDefault(l => l.Id == sourceListId && !l.IsDeleted);
                if (sourceList == null)
                {
                    throw new InvalidOperationException("Source list not found.");
                }

                sourceItems = _listItems.Where(i => i.ListId == sourceListId).ToList();
            }

            // Get source owner's username
            var sourceProfile = GetProfile(sourceList.UserId);
            var sourceUsername = sourceProfile?.Username ?? "Unknown";

            var newList = new UserMediaList
            {
                UserId = newOwnerId,
                Title = sourceList.Title,
                Description = sourceList.Description,
                ListType = sourceList.ListType,
                MaxItems = sourceList.MaxItems,
                ClonedFromUserId = sourceList.UserId,
                ClonedFromUsername = sourceUsername
            };

            await CreateListAsync(newList).ConfigureAwait(false);

            // Clone items
            foreach (var item in sourceItems)
            {
                var newItem = new UserMediaListItem
                {
                    ListId = newList.Id,
                    ItemId = item.ItemId,
                    ImdbId = item.ImdbId,
                    CachedTitle = item.CachedTitle,
                    CachedImageUrl = item.CachedImageUrl,
                    CachedOverview = item.CachedOverview,
                    CachedYear = item.CachedYear,
                    CachedGenres = item.CachedGenres,
                    CachedMediaType = item.CachedMediaType,
                    CachedAt = item.CachedAt,
                    Position = item.Position
                };
                await AddListItemAsync(newItem).ConfigureAwait(false);
            }

            _logger.LogInformation("[Social] List {SourceId} cloned to {NewId} by user {UserId}", sourceListId, newList.Id, newOwnerId);
            return newList;
        }

        private void LoadMediaLists()
        {
            try
            {
                var file = Path.Combine(_dataPath, "media_lists.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var lists = JsonSerializer.Deserialize<List<UserMediaList>>(json, options);
                    if (lists != null)
                    {
                        _mediaLists = lists;
                        _logger.LogInformation("[Social] Loaded {Count} media lists from disk", _mediaLists.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading media lists from disk");
            }
        }

        private async Task SaveMediaListsAsync()
        {
            await _mediaListsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "media_lists.json");
                List<UserMediaList> snapshot;
                lock (_lock)
                {
                    snapshot = _mediaLists.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving media lists to disk");
            }
            finally
            {
                _mediaListsWriteLock.Release();
            }
        }

        #endregion

        #region List Items

        /// <summary>
        /// Adds an item to a list.
        /// </summary>
        public async Task<UserMediaListItem> AddListItemAsync(UserMediaListItem item)
        {
            lock (_lock)
            {
                var list = _mediaLists.FirstOrDefault(l => l.Id == item.ListId);
                if (list == null)
                {
                    throw new InvalidOperationException("List not found.");
                }

                var itemCount = _listItems.Count(i => i.ListId == item.ListId);
                if (itemCount >= list.MaxItems)
                {
                    throw new InvalidOperationException($"List is full. Maximum {list.MaxItems} items allowed.");
                }

                if (item.Position == 0)
                {
                    item.Position = itemCount + 1;
                }

                _listItems.Add(item);
            }

            await SaveListItemsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Added item to list {ListId}", item.ListId);
            return item;
        }

        /// <summary>
        /// Updates a list item.
        /// </summary>
        public async Task<UserMediaListItem> UpdateListItemAsync(UserMediaListItem item)
        {
            lock (_lock)
            {
                var existing = _listItems.FirstOrDefault(i => i.Id == item.Id);
                if (existing != null)
                {
                    var index = _listItems.IndexOf(existing);
                    _listItems[index] = item;
                }
            }

            await SaveListItemsAsync().ConfigureAwait(false);
            return item;
        }

        /// <summary>
        /// Removes an item from a list.
        /// </summary>
        public async Task<bool> RemoveListItemAsync(Guid itemId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _listItems.RemoveAll(i => i.Id == itemId) > 0;
            }

            if (removed)
            {
                await SaveListItemsAsync().ConfigureAwait(false);
                _logger.LogInformation("[Social] Removed item {ItemId} from list", itemId);
            }
            return removed;
        }

        /// <summary>
        /// Gets items in a list.
        /// </summary>
        public List<UserMediaListItem> GetListItems(Guid listId)
        {
            lock (_lock)
            {
                return _listItems.Where(i => i.ListId == listId).OrderBy(i => i.Position).ToList();
            }
        }

        /// <summary>
        /// Reorders items in a list.
        /// </summary>
        public async Task ReorderListItemsAsync(Guid listId, List<Guid> itemIds)
        {
            lock (_lock)
            {
                for (int i = 0; i < itemIds.Count; i++)
                {
                    var item = _listItems.FirstOrDefault(it => it.Id == itemIds[i] && it.ListId == listId);
                    if (item != null)
                    {
                        item.Position = i + 1;
                    }
                }
            }

            await SaveListItemsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Reordered items in list {ListId}", listId);
        }

        private void LoadListItems()
        {
            try
            {
                var file = Path.Combine(_dataPath, "list_items.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var items = JsonSerializer.Deserialize<List<UserMediaListItem>>(json, options);
                    if (items != null)
                    {
                        _listItems = items;
                        _logger.LogInformation("[Social] Loaded {Count} list items from disk", _listItems.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading list items from disk");
            }
        }

        private async Task SaveListItemsAsync()
        {
            await _listItemsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "list_items.json");
                List<UserMediaListItem> snapshot;
                lock (_lock)
                {
                    snapshot = _listItems.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving list items to disk");
            }
            finally
            {
                _listItemsWriteLock.Release();
            }
        }

        #endregion

        #region Profile Styles

        /// <summary>
        /// Gets profile style for a user.
        /// </summary>
        public UserProfileStyle? GetProfileStyle(Guid userId)
        {
            lock (_lock)
            {
                return _profileStyles.TryGetValue(userId, out var style) ? style : null;
            }
        }

        /// <summary>
        /// Gets or creates default profile style for a user.
        /// </summary>
        public UserProfileStyle GetOrCreateProfileStyle(Guid userId)
        {
            lock (_lock)
            {
                if (_profileStyles.TryGetValue(userId, out var style))
                {
                    return style;
                }

                var newStyle = new UserProfileStyle { UserId = userId };
                _profileStyles[userId] = newStyle;
                return newStyle;
            }
        }

        /// <summary>
        /// Saves profile style.
        /// </summary>
        public async Task<UserProfileStyle> SaveProfileStyleAsync(UserProfileStyle style)
        {
            lock (_lock)
            {
                style.UpdatedAt = DateTime.UtcNow;
                _profileStyles[style.UserId] = style;
            }

            await SaveProfileStylesAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Saved profile style for user {UserId}", style.UserId);
            return style;
        }

        private void LoadProfileStyles()
        {
            try
            {
                var file = Path.Combine(_dataPath, "profile_styles.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var styles = JsonSerializer.Deserialize<List<UserProfileStyle>>(json, options);
                    if (styles != null)
                    {
                        _profileStyles = styles.ToDictionary(s => s.UserId);
                        _logger.LogInformation("[Social] Loaded {Count} profile styles from disk", _profileStyles.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading profile styles from disk");
            }
        }

        private async Task SaveProfileStylesAsync()
        {
            await _profileStylesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "profile_styles.json");
                List<UserProfileStyle> snapshot;
                lock (_lock)
                {
                    snapshot = _profileStyles.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving profile styles to disk");
            }
            finally
            {
                _profileStylesWriteLock.Release();
            }
        }

        #endregion

        #region IMDB Cache

        /// <summary>
        /// Gets cached IMDB item.
        /// </summary>
        public ImdbCacheItem? GetImdbCache(string imdbId)
        {
            lock (_lock)
            {
                return _imdbCache.TryGetValue(imdbId, out var item) ? item : null;
            }
        }

        /// <summary>
        /// Saves IMDB cache item.
        /// </summary>
        public async Task<ImdbCacheItem> SaveImdbCacheAsync(ImdbCacheItem item)
        {
            lock (_lock)
            {
                item.CachedAt = DateTime.UtcNow;
                _imdbCache[item.ImdbId] = item;
            }

            await SaveImdbCacheDataAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Cached IMDB item {ImdbId}", item.ImdbId);
            return item;
        }

        /// <summary>
        /// Removes IMDB cache item (for refresh).
        /// </summary>
        public async Task<bool> RemoveImdbCacheAsync(string imdbId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _imdbCache.Remove(imdbId);
            }

            if (removed)
            {
                await SaveImdbCacheDataAsync().ConfigureAwait(false);
            }
            return removed;
        }

        private void LoadImdbCache()
        {
            try
            {
                var file = Path.Combine(_dataPath, "imdb_cache.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var items = JsonSerializer.Deserialize<List<ImdbCacheItem>>(json, options);
                    if (items != null)
                    {
                        _imdbCache = items.ToDictionary(i => i.ImdbId);
                        _logger.LogInformation("[Social] Loaded {Count} IMDB cache items from disk", _imdbCache.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading IMDB cache from disk");
            }
        }

        private async Task SaveImdbCacheDataAsync()
        {
            await _imdbCacheWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "imdb_cache.json");
                List<ImdbCacheItem> snapshot;
                lock (_lock)
                {
                    snapshot = _imdbCache.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving IMDB cache to disk");
            }
            finally
            {
                _imdbCacheWriteLock.Release();
            }
        }

        #endregion

        #region Featured Reviews

        /// <summary>
        /// Features a review.
        /// </summary>
        public async Task<FeaturedReview> FeatureReviewAsync(Guid userId, Guid itemId)
        {
            var featured = new FeaturedReview
            {
                UserId = userId,
                ItemId = itemId
            };

            lock (_lock)
            {
                // Check max 3 featured reviews
                var userFeatured = _featuredReviews.Where(f => f.UserId == userId).ToList();
                if (userFeatured.Count >= 3)
                {
                    throw new InvalidOperationException("Maximum of 3 featured reviews allowed.");
                }

                // Check if already featured
                if (userFeatured.Any(f => f.ItemId == itemId))
                {
                    return userFeatured.First(f => f.ItemId == itemId);
                }

                featured.Position = userFeatured.Count + 1;
                _featuredReviews.Add(featured);
            }

            await SaveFeaturedReviewsAsync().ConfigureAwait(false);
            _logger.LogInformation("[Social] Featured review for item {ItemId} by user {UserId}", itemId, userId);
            return featured;
        }

        /// <summary>
        /// Unfeatures a review.
        /// </summary>
        public async Task<bool> UnfeatureReviewAsync(Guid userId, Guid itemId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _featuredReviews.RemoveAll(f => f.UserId == userId && f.ItemId == itemId) > 0;
            }

            if (removed)
            {
                await SaveFeaturedReviewsAsync().ConfigureAwait(false);
                _logger.LogInformation("[Social] Unfeatured review for item {ItemId} by user {UserId}", itemId, userId);
            }
            return removed;
        }

        /// <summary>
        /// Gets featured reviews for a user.
        /// </summary>
        public List<FeaturedReview> GetFeaturedReviews(Guid userId)
        {
            lock (_lock)
            {
                return _featuredReviews.Where(f => f.UserId == userId).OrderBy(f => f.Position).ToList();
            }
        }

        /// <summary>
        /// Checks if a review is featured.
        /// </summary>
        public bool IsReviewFeatured(Guid userId, Guid itemId)
        {
            lock (_lock)
            {
                return _featuredReviews.Any(f => f.UserId == userId && f.ItemId == itemId);
            }
        }

        private void LoadFeaturedReviews()
        {
            try
            {
                var file = Path.Combine(_dataPath, "featured_reviews.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reviews = JsonSerializer.Deserialize<List<FeaturedReview>>(json, options);
                    if (reviews != null)
                    {
                        _featuredReviews = reviews;
                        _logger.LogInformation("[Social] Loaded {Count} featured reviews from disk", _featuredReviews.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error loading featured reviews from disk");
            }
        }

        private async Task SaveFeaturedReviewsAsync()
        {
            await _featuredReviewsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var file = Path.Combine(_dataPath, "featured_reviews.json");
                List<FeaturedReview> snapshot;
                lock (_lock)
                {
                    snapshot = _featuredReviews.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Social] Error saving featured reviews to disk");
            }
            finally
            {
                _featuredReviewsWriteLock.Release();
            }
        }

        #endregion
    }
}
