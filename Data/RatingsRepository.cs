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
    /// Repository for managing user ratings data.
    /// </summary>
    public class RatingsRepository
    {
        private readonly IApplicationPaths _appPaths;
        private readonly ILogger<RatingsRepository> _logger;
        private readonly string _dataPath;
        private readonly object _lock = new object();

        // Semaphores to prevent concurrent file writes (fixes race condition)
        private static readonly SemaphoreSlim _ratingsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _requestsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _deletionsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _deletionRequestsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _userBansWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _chatMessagesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _chatUsersWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _chatModeratorsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _chatBansWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _privateMessagesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _publicChatLastSeenWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _moderatorActionsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _userStyleOverridesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _mediaQuotasWriteLock = new(1, 1);
        private Dictionary<Guid, UserRating> _ratings;
        private Dictionary<Guid, MediaRequest> _mediaRequests;
        private List<NewMediaNotification> _notifications;
        private Dictionary<Guid, ScheduledDeletion> _scheduledDeletions;
        private Dictionary<Guid, DeletionRequest> _deletionRequests;
        private Dictionary<Guid, UserBan> _userBans;
        private List<ChatMessage> _chatMessages;
        private Dictionary<Guid, ChatUser> _chatUsers;
        private Dictionary<Guid, ChatModerator> _chatModerators;
        private Dictionary<Guid, ChatBan> _chatBans;
        private List<PrivateMessage> _privateMessages;
        private Dictionary<Guid, DateTime> _publicChatLastSeen;
        private List<ModeratorAction> _moderatorActions;
        private Dictionary<Guid, UserStyleOverride> _userStyleOverrides;
        private Dictionary<Guid, MediaQuota> _mediaQuotas;

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingsRepository"/> class.
        /// </summary>
        /// <param name="appPaths">Application paths.</param>
        /// <param name="logger">Logger instance.</param>
        public RatingsRepository(IApplicationPaths appPaths, ILogger<RatingsRepository> logger)
        {
            _appPaths = appPaths;
            _logger = logger;
            _dataPath = Path.Combine(_appPaths.DataPath, "ratings");
            _ratings = new Dictionary<Guid, UserRating>();
            _mediaRequests = new Dictionary<Guid, MediaRequest>();
            _notifications = new List<NewMediaNotification>();
            _scheduledDeletions = new Dictionary<Guid, ScheduledDeletion>();
            _deletionRequests = new Dictionary<Guid, DeletionRequest>();
            _userBans = new Dictionary<Guid, UserBan>();
            _chatMessages = new List<ChatMessage>();
            _chatUsers = new Dictionary<Guid, ChatUser>();
            _chatModerators = new Dictionary<Guid, ChatModerator>();
            _chatBans = new Dictionary<Guid, ChatBan>();
            _privateMessages = new List<PrivateMessage>();
            _moderatorActions = new List<ModeratorAction>();
            _userStyleOverrides = new Dictionary<Guid, UserStyleOverride>();
            _mediaQuotas = new Dictionary<Guid, MediaQuota>();

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            LoadRatings();
            LoadMediaRequests();
            LoadScheduledDeletions();
            LoadDeletionRequests();
            LoadUserBans();
            LoadChatMessages();
            LoadChatUsers();
            LoadChatModerators();
            LoadChatBans();
            LoadPrivateMessages();
            LoadPublicChatLastSeen();
            LoadModeratorActions();
            LoadUserStyleOverrides();
            LoadMediaQuotas();
        }

        /// <summary>
        /// Reloads all data from disk. Used after importing a backup.
        /// </summary>
        /// <returns>Task.</returns>
        public Task ReloadAllDataAsync()
        {
            lock (_lock)
            {
                LoadRatings();
                LoadMediaRequests();
                LoadScheduledDeletions();
                LoadDeletionRequests();
                LoadUserBans();
                LoadChatMessages();
                LoadChatUsers();
                LoadChatModerators();
                LoadChatBans();
                LoadPrivateMessages();
                LoadPublicChatLastSeen();
                LoadModeratorActions();
                LoadUserStyleOverrides();
                LoadMediaQuotas();
            }

            _logger.LogInformation("All data reloaded from disk after backup import");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Loads ratings from disk.
        /// </summary>
        private void LoadRatings()
        {
            try
            {
                var ratingsFile = Path.Combine(_dataPath, "ratings.json");
                if (File.Exists(ratingsFile))
                {
                    var json = File.ReadAllText(ratingsFile);
                    var ratings = JsonSerializer.Deserialize<List<UserRating>>(json);
                    if (ratings != null)
                    {
                        _ratings = ratings.ToDictionary(r => r.Id);
                        _logger.LogInformation("Loaded {Count} ratings from disk", _ratings.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ratings from disk");
            }
        }

        /// <summary>
        /// Saves ratings to disk.
        /// </summary>
        private async Task SaveRatingsAsync()
        {
            await _ratingsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var ratingsFile = Path.Combine(_dataPath, "ratings.json");
                List<UserRating> snapshot;
                lock (_lock)
                {
                    snapshot = _ratings.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(ratingsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} ratings to disk", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ratings to disk");
            }
            finally
            {
                _ratingsWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds or updates a user rating.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="rating">Rating value.</param>
        /// <returns>The created or updated rating.</returns>
        public async Task<UserRating> SetRatingAsync(Guid userId, Guid itemId, int rating)
        {
            lock (_lock)
            {
                var existing = _ratings.Values.FirstOrDefault(r => r.UserId == userId && r.ItemId == itemId);

                if (existing != null)
                {
                    existing.Rating = rating;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _ = SaveRatingsAsync();
                    return existing;
                }

                var newRating = new UserRating
                {
                    UserId = userId,
                    ItemId = itemId,
                    Rating = rating
                };

                _ratings[newRating.Id] = newRating;
                _ = SaveRatingsAsync();
                return newRating;
            }
        }

        /// <summary>
        /// Gets a user's rating for an item.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <returns>The user's rating or null if not found.</returns>
        public UserRating? GetUserRating(Guid userId, Guid itemId)
        {
            lock (_lock)
            {
                return _ratings.Values.FirstOrDefault(r => r.UserId == userId && r.ItemId == itemId);
            }
        }

        /// <summary>
        /// Gets all ratings for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <returns>List of ratings for the item.</returns>
        public List<UserRating> GetItemRatings(Guid itemId)
        {
            lock (_lock)
            {
                return _ratings.Values.Where(r => r.ItemId == itemId).ToList();
            }
        }

        /// <summary>
        /// Gets all ratings by a specific user.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>List of all ratings by the user, ordered by most recently updated.</returns>
        public List<UserRating> GetUserRatings(Guid userId)
        {
            lock (_lock)
            {
                return _ratings.Values
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.UpdatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets rating statistics for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="userId">Optional user ID to include user's rating.</param>
        /// <returns>Rating statistics.</returns>
        public RatingStats GetRatingStats(Guid itemId, Guid? userId = null)
        {
            lock (_lock)
            {
                var itemRatings = _ratings.Values.Where(r => r.ItemId == itemId).ToList();
                var stats = new RatingStats
                {
                    ItemId = itemId,
                    TotalRatings = itemRatings.Count
                };

                if (itemRatings.Any())
                {
                    stats.AverageRating = Math.Round(itemRatings.Average(r => r.Rating), 2);

                    // Calculate distribution
                    for (int i = 1; i <= 10; i++)
                    {
                        stats.Distribution[i - 1] = itemRatings.Count(r => r.Rating == i);
                    }
                }

                if (userId.HasValue)
                {
                    var userRating = itemRatings.FirstOrDefault(r => r.UserId == userId.Value);
                    stats.UserRating = userRating?.Rating;
                }

                return stats;
            }
        }

        /// <summary>
        /// Deletes a user's rating for an item.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <returns>True if the rating was deleted, false otherwise.</returns>
        public async Task<bool> DeleteRatingAsync(Guid userId, Guid itemId)
        {
            lock (_lock)
            {
                var existing = _ratings.Values.FirstOrDefault(r => r.UserId == userId && r.ItemId == itemId);
                if (existing != null)
                {
                    _ratings.Remove(existing.Id);
                    _ = SaveRatingsAsync();
                    return true;
                }

                return false;
            }
        }

        // Media Request Methods

        /// <summary>
        /// Loads media requests from disk.
        /// </summary>
        private void LoadMediaRequests()
        {
            try
            {
                var requestsFile = Path.Combine(_dataPath, "media_requests.json");
                if (File.Exists(requestsFile))
                {
                    var json = File.ReadAllText(requestsFile);
                    var requests = JsonSerializer.Deserialize<List<MediaRequest>>(json);
                    if (requests != null)
                    {
                        _mediaRequests = requests.ToDictionary(r => r.Id);
                        _logger.LogInformation("Loaded {Count} media requests from disk", _mediaRequests.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading media requests from disk");
            }
        }

        /// <summary>
        /// Saves media requests to disk.
        /// </summary>
        private async Task SaveMediaRequestsAsync()
        {
            await _requestsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var requestsFile = Path.Combine(_dataPath, "media_requests.json");
                List<MediaRequest> snapshot;
                lock (_lock)
                {
                    snapshot = _mediaRequests.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(requestsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} media requests to disk", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving media requests to disk");
            }
            finally
            {
                _requestsWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a new media request.
        /// </summary>
        /// <param name="request">The media request to add.</param>
        /// <returns>The created request.</returns>
        public async Task<MediaRequest> AddMediaRequestAsync(MediaRequest request)
        {
            lock (_lock)
            {
                _mediaRequests[request.Id] = request;
                _ = SaveMediaRequestsAsync();
                return request;
            }
        }

        /// <summary>
        /// Gets all media requests.
        /// </summary>
        /// <returns>List of all media requests.</returns>
        public async Task<List<MediaRequest>> GetAllMediaRequestsAsync()
        {
            lock (_lock)
            {
                return _mediaRequests.Values.OrderByDescending(r => r.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Gets a media request by ID.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The media request or null if not found.</returns>
        public async Task<MediaRequest?> GetMediaRequestAsync(Guid requestId)
        {
            lock (_lock)
            {
                return _mediaRequests.ContainsKey(requestId) ? _mediaRequests[requestId] : null;
            }
        }

        /// <summary>
        /// Updates the status of a media request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="status">The new status.</param>
        /// <param name="mediaLink">Optional media link when marking as done.</param>
        /// <param name="rejectionReason">Optional rejection reason when rejecting.</param>
        /// <returns>The updated request or null if not found.</returns>
        public async Task<MediaRequest?> UpdateMediaRequestStatusAsync(Guid requestId, string status, string? mediaLink = null, string? rejectionReason = null)
        {
            lock (_lock)
            {
                if (_mediaRequests.ContainsKey(requestId))
                {
                    var request = _mediaRequests[requestId];
                    request.Status = status;

                    // Set completion time when marked as done
                    if (status == "done")
                    {
                        request.CompletedAt = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(mediaLink))
                        {
                            request.MediaLink = mediaLink;
                        }
                        request.RejectionReason = string.Empty;
                    }
                    else if (status == "rejected")
                    {
                        request.CompletedAt = DateTime.UtcNow;
                        request.RejectionReason = rejectionReason ?? string.Empty;
                        request.MediaLink = string.Empty;
                    }
                    else
                    {
                        // Clear completion data if status is changed back
                        request.CompletedAt = null;
                        request.MediaLink = string.Empty;
                        request.RejectionReason = string.Empty;
                    }

                    _ = SaveMediaRequestsAsync();
                    return request;
                }

                return null;
            }
        }

        /// <summary>
        /// Deletes a media request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>True if deleted, false if not found.</returns>
        public async Task<bool> DeleteMediaRequestAsync(Guid requestId)
        {
            lock (_lock)
            {
                if (_mediaRequests.ContainsKey(requestId))
                {
                    _mediaRequests.Remove(requestId);
                    _ = SaveMediaRequestsAsync();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Updates a media request (for user editing their own request).
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="title">New title.</param>
        /// <param name="type">New type.</param>
        /// <param name="notes">New notes.</param>
        /// <param name="customFields">New custom fields JSON.</param>
        /// <param name="imdbCode">New IMDB code.</param>
        /// <param name="imdbLink">New IMDB link.</param>
        /// <returns>The updated request or null if not found.</returns>
        public async Task<MediaRequest?> UpdateMediaRequestAsync(
            Guid requestId,
            string? title = null,
            string? type = null,
            string? notes = null,
            string? customFields = null,
            string? imdbCode = null,
            string? imdbLink = null)
        {
            lock (_lock)
            {
                if (_mediaRequests.ContainsKey(requestId))
                {
                    var request = _mediaRequests[requestId];

                    if (title != null)
                    {
                        request.Title = title;
                    }

                    if (type != null)
                    {
                        request.Type = type;
                    }

                    if (notes != null)
                    {
                        request.Notes = notes;
                    }

                    if (customFields != null)
                    {
                        request.CustomFields = customFields;
                    }

                    if (imdbCode != null)
                    {
                        request.ImdbCode = imdbCode;
                    }

                    if (imdbLink != null)
                    {
                        request.ImdbLink = imdbLink;
                    }

                    _ = SaveMediaRequestsAsync();
                    return request;
                }

                return null;
            }
        }

        /// <summary>
        /// Snoozes a media request until a specified date.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="snoozedUntil">The date until which to snooze.</param>
        /// <returns>The updated request or null if not found.</returns>
        public async Task<MediaRequest?> SnoozeMediaRequestAsync(Guid requestId, DateTime snoozedUntil)
        {
            lock (_lock)
            {
                if (_mediaRequests.ContainsKey(requestId))
                {
                    var request = _mediaRequests[requestId];
                    request.SnoozedUntil = snoozedUntil;
                    request.Status = "snoozed";
                    _ = SaveMediaRequestsAsync();
                    return request;
                }

                return null;
            }
        }

        /// <summary>
        /// Unsnoozes a media request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The updated request or null if not found.</returns>
        public async Task<MediaRequest?> UnsnoozeMediaRequestAsync(Guid requestId)
        {
            lock (_lock)
            {
                if (_mediaRequests.ContainsKey(requestId))
                {
                    var request = _mediaRequests[requestId];
                    request.SnoozedUntil = null;
                    request.Status = "pending";
                    _ = SaveMediaRequestsAsync();
                    return request;
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the count of requests made by a user in the current month.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Number of requests made this month.</returns>
        public int GetUserRequestCountThisMonth(Guid userId)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

                return _mediaRequests.Values
                    .Count(r => r.UserId == userId && r.CreatedAt >= startOfMonth);
            }
        }

        /// <summary>
        /// Cleans up rejected requests older than the specified number of days.
        /// </summary>
        /// <param name="daysOld">Number of days after which to delete rejected requests.</param>
        /// <returns>Number of deleted requests.</returns>
        public async Task<int> CleanupOldRejectedRequestsAsync(int daysOld)
        {
            if (daysOld <= 0)
            {
                return 0;
            }

            lock (_lock)
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
                var toDelete = _mediaRequests.Values
                    .Where(r => r.Status == "rejected" && r.CompletedAt.HasValue && r.CompletedAt.Value < cutoffDate)
                    .Select(r => r.Id)
                    .ToList();

                foreach (var id in toDelete)
                {
                    _mediaRequests.Remove(id);
                }

                if (toDelete.Count > 0)
                {
                    _ = SaveMediaRequestsAsync();
                    _logger.LogInformation("Cleaned up {Count} old rejected requests", toDelete.Count);
                }

                return toDelete.Count;
            }
        }

        /// <summary>
        /// Gets requests by user ID.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>List of requests by the user.</returns>
        public List<MediaRequest> GetUserRequests(Guid userId)
        {
            lock (_lock)
            {
                return _mediaRequests.Values
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
        }

        // Notification Methods

        /// <summary>
        /// Adds a new media notification.
        /// </summary>
        /// <param name="notification">The notification to add.</param>
        public void AddNotification(NewMediaNotification notification)
        {
            lock (_lock)
            {
                _notifications.Add(notification);

                // Keep only last 100 notifications to prevent memory issues
                while (_notifications.Count > 100)
                {
                    _notifications.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Gets all notifications newer than the specified time.
        /// </summary>
        /// <param name="since">The time to get notifications since.</param>
        /// <returns>List of notifications.</returns>
        public List<NewMediaNotification> GetNotificationsSince(DateTime since)
        {
            lock (_lock)
            {
                return _notifications
                    .Where(n => n.CreatedAt > since)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all notifications.
        /// </summary>
        /// <returns>List of all notifications.</returns>
        public List<NewMediaNotification> GetAllNotifications()
        {
            lock (_lock)
            {
                return _notifications
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(50)
                    .ToList();
            }
        }

        /// <summary>
        /// Clears all notifications.
        /// </summary>
        public void ClearNotifications()
        {
            lock (_lock)
            {
                _notifications.Clear();
            }
        }

        // Scheduled Deletion Methods

        /// <summary>
        /// Loads scheduled deletions from disk.
        /// </summary>
        private void LoadScheduledDeletions()
        {
            try
            {
                var deletionsFile = Path.Combine(_dataPath, "scheduled_deletions.json");
                if (File.Exists(deletionsFile))
                {
                    var json = File.ReadAllText(deletionsFile);
                    var deletions = JsonSerializer.Deserialize<List<ScheduledDeletion>>(json);
                    if (deletions != null)
                    {
                        _scheduledDeletions = deletions.ToDictionary(d => d.ItemId);
                        _logger.LogInformation("Loaded {Count} scheduled deletions from disk", _scheduledDeletions.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading scheduled deletions from disk");
            }
        }

        /// <summary>
        /// Saves scheduled deletions to disk.
        /// </summary>
        private async Task SaveScheduledDeletionsAsync()
        {
            await _deletionsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var deletionsFile = Path.Combine(_dataPath, "scheduled_deletions.json");
                List<ScheduledDeletion> snapshot;
                lock (_lock)
                {
                    snapshot = _scheduledDeletions.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(deletionsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} scheduled deletions to disk", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scheduled deletions to disk");
            }
            finally
            {
                _deletionsWriteLock.Release();
            }
        }

        /// <summary>
        /// Schedules a media item for deletion.
        /// </summary>
        /// <param name="deletion">The scheduled deletion data.</param>
        /// <returns>The created scheduled deletion.</returns>
        public async Task<ScheduledDeletion> ScheduleDeletionAsync(ScheduledDeletion deletion)
        {
            lock (_lock)
            {
                // If item already has a scheduled deletion, update it
                if (_scheduledDeletions.ContainsKey(deletion.ItemId))
                {
                    var existing = _scheduledDeletions[deletion.ItemId];
                    existing.DeleteAt = deletion.DeleteAt;
                    existing.ScheduledAt = DateTime.UtcNow;
                    existing.ScheduledByUserId = deletion.ScheduledByUserId;
                    existing.ScheduledByUsername = deletion.ScheduledByUsername;
                    existing.IsCancelled = false;
                    existing.CancelledAt = null;
                    _ = SaveScheduledDeletionsAsync();
                    return existing;
                }

                _scheduledDeletions[deletion.ItemId] = deletion;
                _ = SaveScheduledDeletionsAsync();
                return deletion;
            }
        }

        /// <summary>
        /// Cancels a scheduled deletion.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>True if cancelled, false if not found.</returns>
        public async Task<bool> CancelDeletionAsync(Guid itemId)
        {
            lock (_lock)
            {
                if (_scheduledDeletions.ContainsKey(itemId))
                {
                    var deletion = _scheduledDeletions[itemId];
                    deletion.IsCancelled = true;
                    deletion.CancelledAt = DateTime.UtcNow;
                    _ = SaveScheduledDeletionsAsync();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the scheduled deletion for an item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>The scheduled deletion or null if not found.</returns>
        public ScheduledDeletion? GetScheduledDeletion(Guid itemId)
        {
            lock (_lock)
            {
                if (_scheduledDeletions.ContainsKey(itemId) && !_scheduledDeletions[itemId].IsCancelled)
                {
                    return _scheduledDeletions[itemId];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets all active (non-cancelled) scheduled deletions.
        /// </summary>
        /// <returns>List of active scheduled deletions.</returns>
        public List<ScheduledDeletion> GetAllScheduledDeletions()
        {
            lock (_lock)
            {
                return _scheduledDeletions.Values
                    .Where(d => !d.IsCancelled)
                    .OrderBy(d => d.DeleteAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets scheduled deletions that are due for execution.
        /// </summary>
        /// <returns>List of due deletions.</returns>
        public List<ScheduledDeletion> GetPendingDeletions()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                return _scheduledDeletions.Values
                    .Where(d => !d.IsCancelled && d.DeleteAt <= now)
                    .ToList();
            }
        }

        /// <summary>
        /// Removes a scheduled deletion record (after successful deletion or cleanup).
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>True if removed, false if not found.</returns>
        public async Task<bool> RemoveDeletionAsync(Guid itemId)
        {
            lock (_lock)
            {
                if (_scheduledDeletions.ContainsKey(itemId))
                {
                    _scheduledDeletions.Remove(itemId);
                    _ = SaveScheduledDeletionsAsync();
                    return true;
                }

                return false;
            }
        }

        // Deletion Request Methods

        /// <summary>
        /// Loads deletion requests from disk.
        /// </summary>
        private void LoadDeletionRequests()
        {
            try
            {
                var requestsFile = Path.Combine(_dataPath, "deletion_requests.json");
                if (File.Exists(requestsFile))
                {
                    var json = File.ReadAllText(requestsFile);
                    var requests = JsonSerializer.Deserialize<List<DeletionRequest>>(json);
                    if (requests != null)
                    {
                        _deletionRequests = requests.ToDictionary(r => r.Id);
                        _logger.LogInformation("Loaded {Count} deletion requests from disk", _deletionRequests.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading deletion requests from disk");
            }
        }

        /// <summary>
        /// Saves deletion requests to disk.
        /// </summary>
        private async Task SaveDeletionRequestsAsync()
        {
            await _deletionRequestsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var requestsFile = Path.Combine(_dataPath, "deletion_requests.json");
                List<DeletionRequest> snapshot;
                lock (_lock)
                {
                    snapshot = _deletionRequests.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(requestsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} deletion requests to disk", snapshot.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving deletion requests to disk");
            }
            finally
            {
                _deletionRequestsWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a new deletion request.
        /// </summary>
        /// <param name="request">The deletion request to add.</param>
        /// <returns>The created deletion request.</returns>
        public async Task<DeletionRequest> AddDeletionRequestAsync(DeletionRequest request)
        {
            lock (_lock)
            {
                _deletionRequests[request.Id] = request;
                _ = SaveDeletionRequestsAsync();
                return request;
            }
        }

        /// <summary>
        /// Gets all deletion requests.
        /// </summary>
        /// <returns>List of all deletion requests ordered by creation date.</returns>
        public List<DeletionRequest> GetAllDeletionRequests()
        {
            lock (_lock)
            {
                return _deletionRequests.Values.OrderByDescending(r => r.CreatedAt).ToList();
            }
        }

        /// <summary>
        /// Gets a deletion request by ID.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <returns>The deletion request or null if not found.</returns>
        public DeletionRequest? GetDeletionRequestById(Guid requestId)
        {
            lock (_lock)
            {
                return _deletionRequests.ContainsKey(requestId) ? _deletionRequests[requestId] : null;
            }
        }

        /// <summary>
        /// Updates the status of a deletion request.
        /// </summary>
        /// <param name="requestId">The request ID.</param>
        /// <param name="status">The new status (approved/rejected).</param>
        /// <param name="resolvedByUsername">The admin username who resolved it.</param>
        /// <param name="rejectionReason">Optional rejection reason.</param>
        /// <returns>The updated request or null if not found.</returns>
        public async Task<DeletionRequest?> UpdateDeletionRequestStatusAsync(Guid requestId, string status, string resolvedByUsername, string? rejectionReason = null)
        {
            lock (_lock)
            {
                if (_deletionRequests.ContainsKey(requestId))
                {
                    var request = _deletionRequests[requestId];
                    request.Status = status;
                    request.ResolvedAt = DateTime.UtcNow;
                    request.ResolvedByUsername = resolvedByUsername;
                    if (status == "rejected" && !string.IsNullOrEmpty(rejectionReason))
                    {
                        request.RejectionReason = rejectionReason;
                    }
                    else
                    {
                        request.RejectionReason = string.Empty;
                    }

                    _ = SaveDeletionRequestsAsync();
                    return request;
                }

                return null;
            }
        }

        /// <summary>
        /// Checks if a pending deletion request exists for a given media request.
        /// </summary>
        /// <param name="mediaRequestId">The media request ID.</param>
        /// <returns>True if a pending deletion request exists.</returns>
        public bool HasPendingDeletionRequest(Guid mediaRequestId)
        {
            lock (_lock)
            {
                return _deletionRequests.Values.Any(r => r.MediaRequestId == mediaRequestId && r.Status == "pending");
            }
        }

        /// <summary>
        /// Gets the total count of deletion requests for a specific media request.
        /// </summary>
        /// <param name="mediaRequestId">The media request ID.</param>
        /// <returns>Total number of deletion requests.</returns>
        public int GetDeletionRequestCountForMediaRequest(Guid mediaRequestId)
        {
            lock (_lock)
            {
                return _deletionRequests.Values.Count(r => r.MediaRequestId == mediaRequestId);
            }
        }

        /// <summary>
        /// Gets the count of pending deletion requests.
        /// </summary>
        /// <returns>Number of pending deletion requests.</returns>
        public int GetPendingDeletionRequestCount()
        {
            lock (_lock)
            {
                return _deletionRequests.Values.Count(r => r.Status == "pending");
            }
        }

        // User Ban Methods

        /// <summary>
        /// Loads user bans from disk.
        /// </summary>
        private void LoadUserBans()
        {
            try
            {
                var bansFile = Path.Combine(_dataPath, "user_bans.json");
                if (File.Exists(bansFile))
                {
                    var json = File.ReadAllText(bansFile);
                    var bans = JsonSerializer.Deserialize<List<UserBan>>(json);
                    if (bans != null)
                    {
                        _userBans = bans.ToDictionary(b => b.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user bans from disk");
            }
        }

        /// <summary>
        /// Saves user bans to disk.
        /// </summary>
        private async Task SaveUserBansAsync()
        {
            await _userBansWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var bansFile = Path.Combine(_dataPath, "user_bans.json");
                List<UserBan> snapshot;
                lock (_lock)
                {
                    snapshot = _userBans.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(bansFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user bans to disk");
            }
            finally
            {
                _userBansWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a new user ban.
        /// </summary>
        /// <param name="ban">The ban to add.</param>
        /// <returns>The created ban.</returns>
        public async Task<UserBan> AddUserBanAsync(UserBan ban)
        {
            lock (_lock)
            {
                _userBans[ban.Id] = ban;
                _ = SaveUserBansAsync();
                return ban;
            }
        }

        /// <summary>
        /// Gets all active bans of a specific type.
        /// </summary>
        /// <param name="banType">The ban type.</param>
        /// <returns>List of active bans.</returns>
        public List<UserBan> GetActiveBans(string banType)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                return _userBans.Values
                    .Where(b => b.BanType == banType && !b.IsLifted && (b.ExpiresAt == null || b.ExpiresAt > now))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if a user is currently banned for a specific type.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <param name="banType">The ban type.</param>
        /// <returns>The active ban or null.</returns>
        public UserBan? GetActiveBan(Guid userId, string banType)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                return _userBans.Values
                    .FirstOrDefault(b => b.UserId == userId && b.BanType == banType && !b.IsLifted && (b.ExpiresAt == null || b.ExpiresAt > now));
            }
        }

        /// <summary>
        /// Lifts a user ban.
        /// </summary>
        /// <param name="banId">The ban ID.</param>
        /// <returns>True if lifted, false if not found.</returns>
        public async Task<bool> LiftBanAsync(Guid banId)
        {
            lock (_lock)
            {
                if (_userBans.ContainsKey(banId))
                {
                    _userBans[banId].IsLifted = true;
                    _ = SaveUserBansAsync();
                    return true;
                }

                return false;
            }
        }

        // Chat Message Methods

        /// <summary>
        /// Loads chat messages from disk.
        /// </summary>
        private void LoadChatMessages()
        {
            try
            {
                var messagesFile = Path.Combine(_dataPath, "chat_messages.json");
                if (File.Exists(messagesFile))
                {
                    var json = File.ReadAllText(messagesFile);
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                    if (messages != null)
                    {
                        _chatMessages = messages;
                        _logger.LogInformation("Loaded {Count} chat messages from disk", _chatMessages.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat messages from disk");
            }
        }

        /// <summary>
        /// Saves chat messages to disk.
        /// </summary>
        private async Task SaveChatMessagesAsync()
        {
            await _chatMessagesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var messagesFile = Path.Combine(_dataPath, "chat_messages.json");
                List<ChatMessage> snapshot;
                lock (_lock)
                {
                    snapshot = _chatMessages.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(messagesFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat messages to disk");
            }
            finally
            {
                _chatMessagesWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a new chat message.
        /// </summary>
        public async Task<ChatMessage> AddChatMessageAsync(ChatMessage message)
        {
            lock (_lock)
            {
                _chatMessages.Add(message);
                // Keep only last 1000 messages in memory
                while (_chatMessages.Count > 1000)
                {
                    _chatMessages.RemoveAt(0);
                }
                _ = SaveChatMessagesAsync();
                return message;
            }
        }

        /// <summary>
        /// Gets recent chat messages.
        /// </summary>
        public List<ChatMessage> GetRecentChatMessages(int count = 100, DateTime? since = null)
        {
            lock (_lock)
            {
                var query = _chatMessages.AsEnumerable();
                if (since.HasValue)
                {
                    query = query.Where(m => m.Timestamp > since.Value);
                }
                return query.OrderByDescending(m => m.Timestamp).Take(count).Reverse().ToList();
            }
        }

        /// <summary>
        /// Gets a chat message by ID.
        /// </summary>
        public ChatMessage? GetChatMessageById(Guid messageId)
        {
            lock (_lock)
            {
                return _chatMessages.FirstOrDefault(m => m.Id == messageId);
            }
        }

        /// <summary>
        /// Soft deletes a chat message.
        /// </summary>
        public async Task<bool> DeleteChatMessageAsync(Guid messageId, Guid deletedBy)
        {
            lock (_lock)
            {
                var message = _chatMessages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    message.IsDeleted = true;
                    message.DeletedBy = deletedBy;
                    message.DeletedAt = DateTime.UtcNow;
                    _ = SaveChatMessagesAsync();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Cleans up old chat messages.
        /// </summary>
        public async Task<int> CleanupOldChatMessagesAsync(int retentionDays)
        {
            if (retentionDays <= 0) return 0;
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
                var removed = _chatMessages.RemoveAll(m => m.Timestamp < cutoff);
                if (removed > 0)
                {
                    _ = SaveChatMessagesAsync();
                    _logger.LogInformation("Cleaned up {Count} old chat messages", removed);
                }
                return removed;
            }
        }

        /// <summary>
        /// Clears all chat messages (admin action).
        /// </summary>
        public async Task ClearAllChatMessagesAsync()
        {
            lock (_lock)
            {
                var count = _chatMessages.Count;
                _chatMessages.Clear();
                _logger.LogInformation("Cleared all {Count} chat messages", count);
            }
            await SaveChatMessagesAsync();
        }

        /// <summary>
        /// Gets unread message count for a user.
        /// </summary>
        public int GetUnreadChatMessageCount(Guid userId, Guid? lastSeenMessageId)
        {
            lock (_lock)
            {
                if (!lastSeenMessageId.HasValue)
                {
                    return _chatMessages.Count(m => !m.IsDeleted);
                }
                var lastSeenMsg = _chatMessages.FirstOrDefault(m => m.Id == lastSeenMessageId.Value);
                if (lastSeenMsg == null) return _chatMessages.Count(m => !m.IsDeleted);
                return _chatMessages.Count(m => !m.IsDeleted && m.Timestamp > lastSeenMsg.Timestamp);
            }
        }

        // Chat User Methods

        /// <summary>
        /// Loads chat users from disk.
        /// </summary>
        private void LoadChatUsers()
        {
            try
            {
                var usersFile = Path.Combine(_dataPath, "chat_users.json");
                if (File.Exists(usersFile))
                {
                    var json = File.ReadAllText(usersFile);
                    var users = JsonSerializer.Deserialize<List<ChatUser>>(json);
                    if (users != null)
                    {
                        _chatUsers = users.ToDictionary(u => u.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat users from disk");
            }
        }

        /// <summary>
        /// Saves chat users to disk.
        /// </summary>
        private async Task SaveChatUsersAsync()
        {
            await _chatUsersWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var usersFile = Path.Combine(_dataPath, "chat_users.json");
                List<ChatUser> snapshot;
                lock (_lock)
                {
                    snapshot = _chatUsers.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(usersFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat users to disk");
            }
            finally
            {
                _chatUsersWriteLock.Release();
            }
        }

        /// <summary>
        /// Updates user presence/heartbeat.
        /// </summary>
        public async Task UpdateChatUserPresenceAsync(Guid userId, string userName, string? avatar, bool isAdmin)
        {
            lock (_lock)
            {
                var isModerator = _chatModerators.Values.Any(m => m.UserId == userId);
                if (_chatUsers.ContainsKey(userId))
                {
                    _chatUsers[userId].LastSeen = DateTime.UtcNow;
                    _chatUsers[userId].UserName = userName;
                    _chatUsers[userId].Avatar = avatar;
                    _chatUsers[userId].IsAdmin = isAdmin;
                    _chatUsers[userId].IsModerator = isModerator;
                }
                else
                {
                    _chatUsers[userId] = new ChatUser
                    {
                        UserId = userId,
                        UserName = userName,
                        Avatar = avatar,
                        LastSeen = DateTime.UtcNow,
                        IsAdmin = isAdmin,
                        IsModerator = isModerator
                    };
                }
                _ = SaveChatUsersAsync();
            }
        }

        /// <summary>
        /// Sets typing status for a user.
        /// </summary>
        public void SetChatUserTyping(Guid userId, bool isTyping)
        {
            lock (_lock)
            {
                if (_chatUsers.ContainsKey(userId))
                {
                    _chatUsers[userId].IsTyping = isTyping;
                    _chatUsers[userId].TypingStarted = isTyping ? DateTime.UtcNow : null;
                }
            }
        }

        /// <summary>
        /// Gets users who are currently typing.
        /// </summary>
        public List<ChatUser> GetTypingUsers()
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-10);
                return _chatUsers.Values
                    .Where(u => u.IsTyping && u.TypingStarted.HasValue && u.TypingStarted.Value > cutoff)
                    .ToList();
            }
        }

        /// <summary>
        /// Checks if a chat user has admin status.
        /// </summary>
        public bool IsChatUserAdmin(Guid userId)
        {
            lock (_lock)
            {
                return _chatUsers.TryGetValue(userId, out var user) && user.IsAdmin;
            }
        }

        /// <summary>
        /// Updates the last seen message for a user.
        /// </summary>
        public async Task UpdateLastSeenMessageAsync(Guid userId, Guid messageId)
        {
            lock (_lock)
            {
                if (_chatUsers.ContainsKey(userId))
                {
                    _chatUsers[userId].LastSeenMessageId = messageId;
                    _ = SaveChatUsersAsync();
                }
            }
        }

        /// <summary>
        /// Gets online users (active in last N minutes).
        /// </summary>
        public List<ChatUser> GetOnlineChatUsers(int activeMinutes = 5)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-activeMinutes);
                // Clear stale typing indicators (older than 10 seconds)
                var typingCutoff = DateTime.UtcNow.AddSeconds(-10);
                foreach (var user in _chatUsers.Values)
                {
                    if (user.IsTyping && user.TypingStarted.HasValue && user.TypingStarted.Value < typingCutoff)
                    {
                        user.IsTyping = false;
                        user.TypingStarted = null;
                    }
                }
                return _chatUsers.Values.Where(u => u.LastSeen > cutoff).OrderBy(u => u.UserName).ToList();
            }
        }

        /// <summary>
        /// Gets a chat user by ID.
        /// </summary>
        public ChatUser? GetChatUser(Guid userId)
        {
            lock (_lock)
            {
                return _chatUsers.ContainsKey(userId) ? _chatUsers[userId] : null;
            }
        }

        // Chat Moderator Methods

        /// <summary>
        /// Loads chat moderators from disk.
        /// </summary>
        private void LoadChatModerators()
        {
            try
            {
                var modsFile = Path.Combine(_dataPath, "chat_moderators.json");
                if (File.Exists(modsFile))
                {
                    var json = File.ReadAllText(modsFile);
                    var mods = JsonSerializer.Deserialize<List<ChatModerator>>(json);
                    if (mods != null)
                    {
                        _chatModerators = mods.ToDictionary(m => m.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat moderators from disk");
            }
        }

        /// <summary>
        /// Saves chat moderators to disk.
        /// </summary>
        private async Task SaveChatModeratorsAsync()
        {
            await _chatModeratorsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var modsFile = Path.Combine(_dataPath, "chat_moderators.json");
                List<ChatModerator> snapshot;
                lock (_lock)
                {
                    snapshot = _chatModerators.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(modsFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat moderators to disk");
            }
            finally
            {
                _chatModeratorsWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a chat moderator.
        /// </summary>
        public async Task<ChatModerator> AddChatModeratorAsync(ChatModerator moderator)
        {
            lock (_lock)
            {
                _chatModerators[moderator.Id] = moderator;
                // Update user's moderator status
                if (_chatUsers.ContainsKey(moderator.UserId))
                {
                    _chatUsers[moderator.UserId].IsModerator = true;
                }
                _ = SaveChatModeratorsAsync();
                return moderator;
            }
        }

        /// <summary>
        /// Removes a chat moderator.
        /// </summary>
        public async Task<bool> RemoveChatModeratorAsync(Guid moderatorId)
        {
            lock (_lock)
            {
                if (_chatModerators.ContainsKey(moderatorId))
                {
                    var mod = _chatModerators[moderatorId];
                    _chatModerators.Remove(moderatorId);
                    // Update user's moderator status
                    if (_chatUsers.ContainsKey(mod.UserId))
                    {
                        _chatUsers[mod.UserId].IsModerator = false;
                    }
                    _ = SaveChatModeratorsAsync();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets all chat moderators.
        /// </summary>
        public List<ChatModerator> GetAllChatModerators()
        {
            lock (_lock)
            {
                return _chatModerators.Values.OrderBy(m => m.UserName).ToList();
            }
        }

        /// <summary>
        /// Checks if a user is a chat moderator.
        /// </summary>
        public bool IsChatModerator(Guid userId)
        {
            lock (_lock)
            {
                return _chatModerators.Values.Any(m => m.UserId == userId);
            }
        }

        /// <summary>
        /// Gets moderator by user ID.
        /// </summary>
        public ChatModerator? GetChatModeratorByUserId(Guid userId)
        {
            lock (_lock)
            {
                return _chatModerators.Values.FirstOrDefault(m => m.UserId == userId);
            }
        }

        // Chat Ban Methods

        /// <summary>
        /// Loads chat bans from disk.
        /// </summary>
        private void LoadChatBans()
        {
            try
            {
                var bansFile = Path.Combine(_dataPath, "chat_bans.json");
                if (File.Exists(bansFile))
                {
                    var json = File.ReadAllText(bansFile);
                    var bans = JsonSerializer.Deserialize<List<ChatBan>>(json);
                    if (bans != null)
                    {
                        _chatBans = bans.ToDictionary(b => b.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chat bans from disk");
            }
        }

        /// <summary>
        /// Saves chat bans to disk.
        /// </summary>
        private async Task SaveChatBansAsync()
        {
            await _chatBansWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var bansFile = Path.Combine(_dataPath, "chat_bans.json");
                List<ChatBan> snapshot;
                lock (_lock)
                {
                    snapshot = _chatBans.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(bansFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chat bans to disk");
            }
            finally
            {
                _chatBansWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a chat ban.
        /// </summary>
        public async Task<ChatBan> AddChatBanAsync(ChatBan ban)
        {
            lock (_lock)
            {
                _chatBans[ban.Id] = ban;
                _ = SaveChatBansAsync();
                return ban;
            }
        }

        /// <summary>
        /// Gets a chat ban by ID.
        /// </summary>
        public ChatBan? GetChatBanById(Guid banId)
        {
            lock (_lock)
            {
                return _chatBans.TryGetValue(banId, out var ban) ? ban : null;
            }
        }

        /// <summary>
        /// Removes a chat ban.
        /// </summary>
        public async Task<bool> RemoveChatBanAsync(Guid banId)
        {
            lock (_lock)
            {
                if (_chatBans.ContainsKey(banId))
                {
                    _chatBans.Remove(banId);
                    _ = SaveChatBansAsync();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets active chat ban for a user by type.
        /// </summary>
        public ChatBan? GetActiveChatBan(Guid userId, string banType)
        {
            lock (_lock)
            {
                return _chatBans.Values.FirstOrDefault(b =>
                    b.UserId == userId &&
                    b.BanType == banType &&
                    b.IsActive);
            }
        }

        /// <summary>
        /// Gets all active chat bans.
        /// </summary>
        public List<ChatBan> GetAllActiveChatBans()
        {
            lock (_lock)
            {
                return _chatBans.Values.Where(b => b.IsActive).OrderByDescending(b => b.BannedAt).ToList();
            }
        }

        /// <summary>
        /// Gets all chat bans for a user.
        /// </summary>
        public List<ChatBan> GetChatBansForUser(Guid userId)
        {
            lock (_lock)
            {
                return _chatBans.Values.Where(b => b.UserId == userId).OrderByDescending(b => b.BannedAt).ToList();
            }
        }

        // ============ PRIVATE MESSAGES (DM) ============

        /// <summary>
        /// Loads private messages from disk.
        /// </summary>
        private void LoadPrivateMessages()
        {
            try
            {
                var messagesFile = Path.Combine(_dataPath, "private_messages.json");
                if (File.Exists(messagesFile))
                {
                    var json = File.ReadAllText(messagesFile);
                    var messages = JsonSerializer.Deserialize<List<PrivateMessage>>(json);
                    if (messages != null)
                    {
                        _privateMessages = messages;
                        _logger.LogInformation("Loaded {Count} private messages from disk", _privateMessages.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading private messages from disk");
            }
        }

        /// <summary>
        /// Saves private messages to disk.
        /// </summary>
        private async Task SavePrivateMessagesAsync()
        {
            await _privateMessagesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var messagesFile = Path.Combine(_dataPath, "private_messages.json");
                List<PrivateMessage> snapshot;
                lock (_lock)
                {
                    snapshot = _privateMessages.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(messagesFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving private messages to disk");
            }
            finally
            {
                _privateMessagesWriteLock.Release();
            }
        }

        /// <summary>
        /// Loads public chat last seen timestamps from disk.
        /// </summary>
        private void LoadPublicChatLastSeen()
        {
            _publicChatLastSeen = new Dictionary<Guid, DateTime>();
            try
            {
                var lastSeenFile = Path.Combine(_dataPath, "public_chat_last_seen.json");
                if (File.Exists(lastSeenFile))
                {
                    var json = File.ReadAllText(lastSeenFile);
                    var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json);
                    if (data != null)
                    {
                        foreach (var kvp in data)
                        {
                            if (Guid.TryParse(kvp.Key, out var userId))
                            {
                                _publicChatLastSeen[userId] = kvp.Value;
                            }
                        }

                        _logger.LogInformation("Loaded {Count} public chat last seen entries", _publicChatLastSeen.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading public chat last seen from disk");
            }
        }

        /// <summary>
        /// Saves public chat last seen timestamps to disk.
        /// </summary>
        private async Task SavePublicChatLastSeenAsync()
        {
            await _publicChatLastSeenWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var lastSeenFile = Path.Combine(_dataPath, "public_chat_last_seen.json");
                Dictionary<string, DateTime> snapshot;
                lock (_lock)
                {
                    snapshot = _publicChatLastSeen.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(lastSeenFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving public chat last seen to disk");
            }
            finally
            {
                _publicChatLastSeenWriteLock.Release();
            }
        }

        /// <summary>
        /// Gets the public chat unread count for a user.
        /// </summary>
        public int GetPublicChatUnreadCount(Guid userId)
        {
            lock (_lock)
            {
                // Get user's last seen timestamp, default to epoch if never seen
                var lastSeen = _publicChatLastSeen.TryGetValue(userId, out var ts) ? ts : DateTime.MinValue;

                // Count messages newer than last seen, excluding user's own messages
                return _chatMessages.Count(m => m.Timestamp > lastSeen && m.UserId != userId);
            }
        }

        /// <summary>
        /// Marks public chat as read for a user (sets last seen to now).
        /// </summary>
        public async Task MarkPublicChatReadAsync(Guid userId)
        {
            lock (_lock)
            {
                _publicChatLastSeen[userId] = DateTime.UtcNow;
            }

            await SavePublicChatLastSeenAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a new private message.
        /// </summary>
        public async Task<PrivateMessage> AddPrivateMessageAsync(PrivateMessage message)
        {
            lock (_lock)
            {
                _privateMessages.Add(message);
                // Keep only last 5000 private messages in memory
                while (_privateMessages.Count > 5000)
                {
                    _privateMessages.RemoveAt(0);
                }
                _ = SavePrivateMessagesAsync();
                return message;
            }
        }

        /// <summary>
        /// Gets private messages between two users (bidirectional).
        /// SECURITY: Only call after verifying userId is one of the participants.
        /// </summary>
        public List<PrivateMessage> GetPrivateMessages(Guid userId1, Guid userId2, int limit = 50, DateTime? since = null)
        {
            lock (_lock)
            {
                var query = _privateMessages.AsEnumerable()
                    .Where(m => !m.IsDeleted &&
                        ((m.SenderId == userId1 && m.RecipientId == userId2) ||
                         (m.SenderId == userId2 && m.RecipientId == userId1)));

                if (since.HasValue)
                {
                    query = query.Where(m => m.Timestamp > since.Value);
                }

                return query.OrderByDescending(m => m.Timestamp)
                    .Take(limit)
                    .Reverse()
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all DM conversations for a user with last message preview and unread count.
        /// </summary>
        public List<(Guid OtherUserId, string OtherUserName, string? OtherUserAvatar, PrivateMessage LastMessage, int UnreadCount)> GetConversations(Guid userId)
        {
            lock (_lock)
            {
                var conversations = new Dictionary<Guid, (string Name, string? Avatar, PrivateMessage Last, int Unread)>();

                foreach (var msg in _privateMessages.Where(m => !m.IsDeleted && (m.SenderId == userId || m.RecipientId == userId)))
                {
                    Guid otherUserId;
                    string otherName;
                    string? otherAvatar;

                    if (msg.SenderId == userId)
                    {
                        otherUserId = msg.RecipientId;
                        otherName = msg.RecipientName;
                        otherAvatar = null;
                    }
                    else
                    {
                        otherUserId = msg.SenderId;
                        otherName = msg.SenderName;
                        otherAvatar = msg.SenderAvatar;
                    }

                    if (!conversations.ContainsKey(otherUserId))
                    {
                        conversations[otherUserId] = (otherName, otherAvatar, msg, 0);
                    }

                    // Update last message if newer
                    if (msg.Timestamp > conversations[otherUserId].Last.Timestamp)
                    {
                        var existing = conversations[otherUserId];
                        conversations[otherUserId] = (otherName, otherAvatar ?? existing.Avatar, msg, existing.Unread);
                    }

                    // Count unread (messages TO this user that are unread)
                    if (msg.RecipientId == userId && !msg.IsRead)
                    {
                        var existing = conversations[otherUserId];
                        conversations[otherUserId] = (existing.Name, existing.Avatar, existing.Last, existing.Unread + 1);
                    }
                }

                return conversations
                    .Select(kvp => (kvp.Key, kvp.Value.Name, kvp.Value.Avatar, kvp.Value.Last, kvp.Value.Unread))
                    .OrderByDescending(c => c.Last.Timestamp)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets total unread DM count for a user.
        /// </summary>
        public int GetUnreadDMCount(Guid userId)
        {
            lock (_lock)
            {
                return _privateMessages.Count(m => !m.IsDeleted && m.RecipientId == userId && !m.IsRead);
            }
        }

        /// <summary>
        /// Marks all DMs in a conversation as read for a user.
        /// </summary>
        public async Task<int> MarkConversationReadAsync(Guid userId, Guid otherUserId)
        {
            lock (_lock)
            {
                int count = 0;
                foreach (var msg in _privateMessages.Where(m => m.RecipientId == userId && m.SenderId == otherUserId && !m.IsRead))
                {
                    msg.IsRead = true;
                    count++;
                }
                if (count > 0)
                {
                    _ = SavePrivateMessagesAsync();
                }
                return count;
            }
        }

        /// <summary>
        /// Deletes a private message. SECURITY: Only sender can delete their own messages.
        /// </summary>
        public async Task<bool> DeletePrivateMessageAsync(Guid messageId, Guid userId)
        {
            lock (_lock)
            {
                var message = _privateMessages.FirstOrDefault(m => m.Id == messageId && m.SenderId == userId);
                if (message != null && !message.IsDeleted)
                {
                    message.IsDeleted = true;
                    message.DeletedAt = DateTime.UtcNow;
                    _ = SavePrivateMessagesAsync();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Cleans up expired data to prevent unbounded growth.
        /// Should be called periodically (e.g., on plugin startup and daily).
        /// </summary>
        public async Task CleanupExpiredDataAsync()
        {
            var now = DateTime.UtcNow;
            var userBansChanged = false;
            var chatBansChanged = false;
            var chatUsersChanged = false;

            lock (_lock)
            {
                // Remove expired user bans
                var expiredUserBans = _userBans.Values
                    .Where(b => b.ExpiresAt.HasValue && b.ExpiresAt.Value < now)
                    .Select(b => b.Id)
                    .ToList();
                foreach (var id in expiredUserBans)
                {
                    _userBans.Remove(id);
                    userBansChanged = true;
                }

                // Remove expired chat bans
                var expiredChatBans = _chatBans.Values
                    .Where(b => b.ExpiresAt.HasValue && b.ExpiresAt.Value < now)
                    .Select(b => b.Id)
                    .ToList();
                foreach (var id in expiredChatBans)
                {
                    _chatBans.Remove(id);
                    chatBansChanged = true;
                }

                // Remove inactive chat users (not seen in 30 days)
                var inactiveCutoff = now.AddDays(-30);
                var inactiveUsers = _chatUsers.Values
                    .Where(u => u.LastSeen < inactiveCutoff)
                    .Select(u => u.UserId)
                    .ToList();
                foreach (var id in inactiveUsers)
                {
                    _chatUsers.Remove(id);
                    chatUsersChanged = true;
                }

                // Remove old notifications (older than 7 days)
                var notificationCutoff = now.AddDays(-7);
                var oldNotifications = _notifications
                    .Where(n => n.CreatedAt < notificationCutoff)
                    .ToList();
                foreach (var n in oldNotifications)
                {
                    _notifications.Remove(n);
                }
            }

            // Save AFTER releasing the lock to avoid deadlock
            if (userBansChanged || chatBansChanged || chatUsersChanged)
            {
                _logger.LogInformation("Cleaned up expired data (bans, inactive users, old notifications)");
            }

            if (userBansChanged)
            {
                await SaveUserBansAsync().ConfigureAwait(false);
            }

            if (chatBansChanged)
            {
                await SaveChatBansAsync().ConfigureAwait(false);
            }

            if (chatUsersChanged)
            {
                await SaveChatUsersAsync().ConfigureAwait(false);
            }
        }

        // ============ MODERATOR ACTIONS ============

        /// <summary>
        /// Loads moderator actions from disk.
        /// </summary>
        private void LoadModeratorActions()
        {
            try
            {
                var actionsFile = Path.Combine(_dataPath, "moderator_actions.json");
                if (File.Exists(actionsFile))
                {
                    var json = File.ReadAllText(actionsFile);
                    var actions = JsonSerializer.Deserialize<List<ModeratorAction>>(json);
                    if (actions != null)
                    {
                        _moderatorActions = actions;
                        _logger.LogInformation("Loaded {Count} moderator actions from disk", _moderatorActions.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading moderator actions from disk");
            }
        }

        /// <summary>
        /// Saves moderator actions to disk.
        /// </summary>
        private async Task SaveModeratorActionsAsync()
        {
            await _moderatorActionsWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var actionsFile = Path.Combine(_dataPath, "moderator_actions.json");
                List<ModeratorAction> snapshot;
                lock (_lock)
                {
                    snapshot = _moderatorActions.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(actionsFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving moderator actions to disk");
            }
            finally
            {
                _moderatorActionsWriteLock.Release();
            }
        }

        /// <summary>
        /// Adds a moderator action to the log.
        /// </summary>
        public async Task<ModeratorAction> AddModeratorActionAsync(ModeratorAction action)
        {
            lock (_lock)
            {
                _moderatorActions.Add(action);
                // Keep only last 10000 actions
                while (_moderatorActions.Count > 10000)
                {
                    _moderatorActions.RemoveAt(0);
                }
                _ = SaveModeratorActionsAsync();
                return action;
            }
        }

        /// <summary>
        /// Gets moderator actions, optionally filtered by moderator ID.
        /// </summary>
        public List<ModeratorAction> GetModeratorActions(Guid? moderatorId = null, int limit = 100)
        {
            lock (_lock)
            {
                var query = _moderatorActions.AsEnumerable();
                if (moderatorId.HasValue)
                {
                    query = query.Where(a => a.ModeratorId == moderatorId.Value);
                }
                return query.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
            }
        }

        /// <summary>
        /// Gets the action count for a moderator.
        /// </summary>
        public int GetModeratorActionCount(Guid moderatorId)
        {
            lock (_lock)
            {
                return _moderatorActions.Count(a => a.ModeratorId == moderatorId);
            }
        }

        /// <summary>
        /// Gets the media ban days used this month by a moderator for a specific target user.
        /// </summary>
        public int GetMediaBanDaysUsedThisMonth(Guid moderatorId, Guid targetUserId)
        {
            lock (_lock)
            {
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return _moderatorActions
                    .Where(a => a.ModeratorId == moderatorId &&
                               a.TargetUserId == targetUserId &&
                               a.ActionType == "media_ban" &&
                               a.Timestamp >= startOfMonth)
                    .Sum(a =>
                    {
                        // Parse days from Details JSON if present
                        if (!string.IsNullOrEmpty(a.Details))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(a.Details);
                                if (doc.RootElement.TryGetProperty("durationDays", out var days))
                                {
                                    return days.GetInt32();
                                }
                            }
                            catch { }
                        }
                        return 0;
                    });
            }
        }

        // ============ USER STYLE OVERRIDES ============

        /// <summary>
        /// Loads user style overrides from disk.
        /// </summary>
        private void LoadUserStyleOverrides()
        {
            try
            {
                var stylesFile = Path.Combine(_dataPath, "user_style_overrides.json");
                if (File.Exists(stylesFile))
                {
                    var json = File.ReadAllText(stylesFile);
                    var styles = JsonSerializer.Deserialize<List<UserStyleOverride>>(json);
                    if (styles != null)
                    {
                        _userStyleOverrides = styles.ToDictionary(s => s.UserId);
                        _logger.LogInformation("Loaded {Count} user style overrides from disk", _userStyleOverrides.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user style overrides from disk");
            }
        }

        /// <summary>
        /// Saves user style overrides to disk.
        /// </summary>
        private async Task SaveUserStyleOverridesAsync()
        {
            await _userStyleOverridesWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var stylesFile = Path.Combine(_dataPath, "user_style_overrides.json");
                List<UserStyleOverride> snapshot;
                lock (_lock)
                {
                    snapshot = _userStyleOverrides.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(stylesFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user style overrides to disk");
            }
            finally
            {
                _userStyleOverridesWriteLock.Release();
            }
        }

        /// <summary>
        /// Sets or updates a user style override.
        /// </summary>
        public async Task<UserStyleOverride> SetUserStyleOverrideAsync(UserStyleOverride style)
        {
            lock (_lock)
            {
                _userStyleOverrides[style.UserId] = style;
                _ = SaveUserStyleOverridesAsync();
                return style;
            }
        }

        /// <summary>
        /// Gets a user style override by user ID.
        /// </summary>
        public UserStyleOverride? GetUserStyleOverride(Guid userId)
        {
            lock (_lock)
            {
                return _userStyleOverrides.TryGetValue(userId, out var style) ? style : null;
            }
        }

        /// <summary>
        /// Gets all user style overrides.
        /// </summary>
        public List<UserStyleOverride> GetAllUserStyleOverrides()
        {
            lock (_lock)
            {
                return _userStyleOverrides.Values.ToList();
            }
        }

        /// <summary>
        /// Removes a user style override.
        /// </summary>
        public async Task<bool> RemoveUserStyleOverrideAsync(Guid userId)
        {
            lock (_lock)
            {
                if (_userStyleOverrides.ContainsKey(userId))
                {
                    _userStyleOverrides.Remove(userId);
                    _ = SaveUserStyleOverridesAsync();
                    return true;
                }
                return false;
            }
        }

        // ============ MEDIA QUOTAS ============

        /// <summary>
        /// Loads media quotas from disk.
        /// </summary>
        private void LoadMediaQuotas()
        {
            try
            {
                var quotasFile = Path.Combine(_dataPath, "media_quotas.json");
                if (File.Exists(quotasFile))
                {
                    var json = File.ReadAllText(quotasFile);
                    var quotas = JsonSerializer.Deserialize<List<MediaQuota>>(json);
                    if (quotas != null)
                    {
                        _mediaQuotas = quotas.ToDictionary(q => q.UserId);
                        _logger.LogInformation("Loaded {Count} media quotas from disk", _mediaQuotas.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading media quotas from disk");
            }
        }

        /// <summary>
        /// Saves media quotas to disk.
        /// </summary>
        private async Task SaveMediaQuotasAsync()
        {
            await _mediaQuotasWriteLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var quotasFile = Path.Combine(_dataPath, "media_quotas.json");
                List<MediaQuota> snapshot;
                lock (_lock)
                {
                    snapshot = _mediaQuotas.Values.ToList();
                }

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(quotasFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving media quotas to disk");
            }
            finally
            {
                _mediaQuotasWriteLock.Release();
            }
        }

        /// <summary>
        /// Sets or updates a media quota for a user.
        /// </summary>
        public async Task<MediaQuota> SetMediaQuotaAsync(MediaQuota quota)
        {
            var now = DateTime.UtcNow;
            // Initialize reset times if not set
            if (quota.DailyReset == default)
            {
                quota.DailyReset = now.Date.AddDays(1);
            }
            if (quota.WeeklyReset == default)
            {
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0) daysUntilMonday = 7;
                quota.WeeklyReset = now.Date.AddDays(daysUntilMonday);
            }
            if (quota.MonthlyReset == default)
            {
                quota.MonthlyReset = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            }

            lock (_lock)
            {
                _mediaQuotas[quota.UserId] = quota;
                _ = SaveMediaQuotasAsync();
                return quota;
            }
        }

        /// <summary>
        /// Gets a media quota by user ID.
        /// </summary>
        public MediaQuota? GetMediaQuota(Guid userId)
        {
            lock (_lock)
            {
                return _mediaQuotas.TryGetValue(userId, out var quota) ? quota : null;
            }
        }

        /// <summary>
        /// Checks if a user's media quota is exceeded.
        /// </summary>
        public bool IsMediaQuotaExceeded(Guid userId)
        {
            lock (_lock)
            {
                if (!_mediaQuotas.TryGetValue(userId, out var quota))
                {
                    return false; // No quota = no limit
                }
                return quota.IsQuotaExceeded();
            }
        }

        /// <summary>
        /// Increments media usage for a user.
        /// </summary>
        public async Task IncrementMediaUsageAsync(Guid userId)
        {
            lock (_lock)
            {
                if (_mediaQuotas.TryGetValue(userId, out var quota))
                {
                    quota.IncrementUsage();
                    _ = SaveMediaQuotasAsync();
                }
            }
        }

        /// <summary>
        /// Removes a media quota for a user.
        /// </summary>
        public async Task<bool> RemoveMediaQuotaAsync(Guid userId)
        {
            lock (_lock)
            {
                if (_mediaQuotas.ContainsKey(userId))
                {
                    _mediaQuotas.Remove(userId);
                    _ = SaveMediaQuotasAsync();
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Updates a chat moderator.
        /// </summary>
        public async Task<ChatModerator?> UpdateChatModeratorAsync(Guid moderatorId, int? level = null)
        {
            lock (_lock)
            {
                if (_chatModerators.TryGetValue(moderatorId, out var moderator))
                {
                    if (level.HasValue)
                    {
                        moderator.Level = level.Value;
                    }
                    _ = SaveChatModeratorsAsync();
                    return moderator;
                }
                return null;
            }
        }

        /// <summary>
        /// Resets daily delete count for a moderator if needed.
        /// </summary>
        public void ResetModeratorDailyDeleteCount(Guid moderatorId)
        {
            lock (_lock)
            {
                if (_chatModerators.TryGetValue(moderatorId, out var moderator))
                {
                    var now = DateTime.UtcNow;
                    if (now >= moderator.DailyDeleteReset)
                    {
                        moderator.DailyDeleteCount = 0;
                        moderator.DailyDeleteReset = now.Date.AddDays(1);
                    }
                }
            }
        }

        /// <summary>
        /// Increments daily delete count for a moderator.
        /// </summary>
        public async Task IncrementModeratorDeleteCountAsync(Guid moderatorId)
        {
            lock (_lock)
            {
                if (_chatModerators.TryGetValue(moderatorId, out var moderator))
                {
                    moderator.DailyDeleteCount++;
                    _ = SaveChatModeratorsAsync();
                }
            }
        }

        /// <summary>
        /// Gets a chat moderator by ID.
        /// </summary>
        public ChatModerator? GetChatModeratorById(Guid moderatorId)
        {
            lock (_lock)
            {
                return _chatModerators.TryGetValue(moderatorId, out var moderator) ? moderator : null;
            }
        }
    }
}
