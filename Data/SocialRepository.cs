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

        // In-memory storage
        private Dictionary<Guid, UserProfile> _profiles;
        private List<FriendRequest> _friendRequests;
        private List<Friendship> _friendships;

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

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                _logger.LogInformation("[Social] Created social data directory: {Path}", _dataPath);
            }

            LoadProfiles();
            LoadFriendRequests();
            LoadFriendships();

            _logger.LogInformation("[Social] Repository initialized - Profiles: {Profiles}, Requests: {Requests}, Friendships: {Friendships}",
                _profiles.Count, _friendRequests.Count, _friendships.Count);
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
                    var profiles = JsonSerializer.Deserialize<List<UserProfile>>(json);
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
                    var requests = JsonSerializer.Deserialize<List<FriendRequest>>(json);
                    if (requests != null)
                    {
                        _friendRequests = requests;
                        _logger.LogInformation("[Social] Loaded {Count} friend requests from disk", _friendRequests.Count);
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
                    var friendships = JsonSerializer.Deserialize<List<Friendship>>(json);
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
    }
}
