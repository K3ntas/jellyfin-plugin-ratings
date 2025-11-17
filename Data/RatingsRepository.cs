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

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            LoadRatings();
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
                    _logger.LogInformation("Updated rating for user {UserId} on item {ItemId}: {Rating}", userId, itemId, rating);
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
                _logger.LogInformation("Created rating for user {UserId} on item {ItemId}: {Rating}", userId, itemId, rating);
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
                _logger.LogInformation("GetItemRatings - Searching for ItemId: {ItemId}", itemId);
                _logger.LogInformation("GetItemRatings - Total ratings in memory: {Count}", _ratings.Count);

                var itemRatings = _ratings.Values.Where(r => r.ItemId == itemId).ToList();
                _logger.LogInformation("GetItemRatings - Found {Count} ratings for ItemId {ItemId}", itemRatings.Count, itemId);

                // Log all ratings in memory for debugging
                foreach (var rating in _ratings.Values.Take(10))
                {
                    _logger.LogInformation("  In-memory rating: Id={Id}, UserId={UserId}, ItemId={ItemId}, Rating={Rating}",
                        rating.Id, rating.UserId, rating.ItemId, rating.Rating);
                }

                return itemRatings;
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
                    _logger.LogInformation("Deleted rating for user {UserId} on item {ItemId}", userId, itemId);
                    _ = SaveRatingsAsync();
                    return true;
                }

                return false;
            }
        }
    }
}
