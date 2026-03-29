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

        // In-memory storage
        private Dictionary<Guid, UserProfile> _profiles;
        private List<FriendRequest> _friendRequests;
        private List<Friendship> _friendships;
        private List<SocialNotification> _notifications;
        private Dictionary<Guid, UserOnlineStatus> _onlineStatuses;

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

            _logger.LogInformation("[Social] Repository initialized - Profiles: {Profiles}, Requests: {Requests}, Friendships: {Friendships}, OnlineStatuses: {OnlineStatuses}, BlockedUsers: {BlockedUsers}",
                _profiles.Count, _friendRequests.Count, _friendships.Count, _onlineStatuses.Count, _blockedUsers.Count);
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
        /// Sets what the user is currently watching. Does not affect online status.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="watching">The watching info.</param>
        /// <returns>The updated online status.</returns>
        public async Task<UserOnlineStatus> SetWatchingAsync(Guid userId, CurrentlyWatching watching)
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

                // Only update watching
                status.Watching = watching;
                // Refresh the status field to reflect current state
                status.Status = status.GetEffectiveStatus();
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
            return status;
        }

        /// <summary>
        /// Clears what the user is watching. Does not affect online status.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>The updated online status.</returns>
        public async Task<UserOnlineStatus> ClearWatchingAsync(Guid userId)
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

                // Only clear watching
                status.Watching = null;
                // Refresh the status field to reflect current state
                status.Status = status.GetEffectiveStatus();
            }

            await SaveOnlineStatusesAsync().ConfigureAwait(false);
            return status;
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
    }
}
