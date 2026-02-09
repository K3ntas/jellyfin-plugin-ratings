using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private Dictionary<Guid, UserRating> _ratings;
        private Dictionary<Guid, MediaRequest> _mediaRequests;
        private List<NewMediaNotification> _notifications;
        private Dictionary<Guid, ScheduledDeletion> _scheduledDeletions;
        private Dictionary<Guid, DeletionRequest> _deletionRequests;
        private Dictionary<Guid, UserBan> _userBans;

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

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            LoadRatings();
            LoadMediaRequests();
            LoadScheduledDeletions();
            LoadDeletionRequests();
            LoadUserBans();
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
            try
            {
                var ratingsFile = Path.Combine(_dataPath, "ratings.json");
                var json = JsonSerializer.Serialize(_ratings.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(ratingsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} ratings to disk", _ratings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ratings to disk");
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
            try
            {
                var requestsFile = Path.Combine(_dataPath, "media_requests.json");
                var json = JsonSerializer.Serialize(_mediaRequests.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(requestsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} media requests to disk", _mediaRequests.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving media requests to disk");
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
            try
            {
                var deletionsFile = Path.Combine(_dataPath, "scheduled_deletions.json");
                var json = JsonSerializer.Serialize(_scheduledDeletions.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(deletionsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} scheduled deletions to disk", _scheduledDeletions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving scheduled deletions to disk");
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
            try
            {
                var requestsFile = Path.Combine(_dataPath, "deletion_requests.json");
                var json = JsonSerializer.Serialize(_deletionRequests.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(requestsFile, json).ConfigureAwait(false);
                _logger.LogDebug("Saved {Count} deletion requests to disk", _deletionRequests.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving deletion requests to disk");
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
            try
            {
                var bansFile = Path.Combine(_dataPath, "user_bans.json");
                var json = JsonSerializer.Serialize(_userBans.Values.ToList(), new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(bansFile, json).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user bans to disk");
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
    }
}
