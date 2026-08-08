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
        private readonly JsonFileWriter _writer;

        // Files written by older versions used PascalCase property names. The API-facing models
        // now carry explicit camelCase names, so reads MUST be case-insensitive or every existing
        // ratings.json would silently deserialize to defaults - i.e. look like total data loss.
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

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
        private static readonly SemaphoreSlim _notifiedItemsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _seenMediaWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _moderatorActionsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _userStyleOverridesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _mediaQuotasWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _keepRequestsWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _reviewLikesWriteLock = new(1, 1);
        private static readonly SemaphoreSlim _reviewCommentsWriteLock = new(1, 1);
        private Dictionary<Guid, UserRating> _ratings;

        // Secondary indexes for fast lookups (O(1) instead of O(n))
        private Dictionary<Guid, List<UserRating>> _ratingsByItemId;
        private Dictionary<string, List<UserRating>> _ratingsByTmdbId;
        private Dictionary<string, List<UserRating>> _ratingsByImdbId;
        private Dictionary<string, List<UserRating>> _ratingsByAniDbId;

        private Dictionary<Guid, ReviewLike> _reviewLikes;
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

        // Persistent set of item IDs we have ALREADY announced as "new media", with the time we
        // announced them. Disk-backed so a server restart (e.g. after a plugin update) does not wipe
        // the dedup and cause a rescan/metadata-refresh to re-announce already-known media.
        private Dictionary<Guid, DateTime> _notifiedItems;

        // Persistent set of STABLE content keys (path / provider-id based, see NotificationService)
        // for every media item the plugin has ever seen. Unlike the Guid set above, these survive
        // Jellyfin re-creating an item row with a new internal Id and resetting its DateCreated
        // (common on NAS/Docker rescans). This is the authoritative "have we ever seen this file?"
        // record that prevents already-present media from being announced as new.
        private Dictionary<string, DateTime> _seenMediaKeys;
        private List<ModeratorAction> _moderatorActions;
        private Dictionary<Guid, UserStyleOverride> _userStyleOverrides;
        private Dictionary<Guid, MediaQuota> _mediaQuotas;
        private List<KeepRequest> _keepRequests;
        private Dictionary<Guid, ReviewComment> _reviewComments;

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
            _writer = new JsonFileWriter(_dataPath, logger);
            _ratings = new Dictionary<Guid, UserRating>();
            _ratingsByItemId = new Dictionary<Guid, List<UserRating>>();
            _ratingsByTmdbId = new Dictionary<string, List<UserRating>>();
            _ratingsByImdbId = new Dictionary<string, List<UserRating>>();
            _ratingsByAniDbId = new Dictionary<string, List<UserRating>>();
            _reviewLikes = new Dictionary<Guid, ReviewLike>();
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
            _keepRequests = new List<KeepRequest>();
            _reviewComments = new Dictionary<Guid, ReviewComment>();
            _notifiedItems = new Dictionary<Guid, DateTime>();
            _seenMediaKeys = new Dictionary<string, DateTime>();

            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            // Load all persisted data in parallel to cut startup/first-use latency.
            // Each loader reads its own file into its own collection, so they are independent.
            System.Threading.Tasks.Parallel.Invoke(
                LoadRatings,
                LoadMediaRequests,
                LoadScheduledDeletions,
                LoadDeletionRequests,
                LoadUserBans,
                LoadChatMessages,
                LoadChatUsers,
                LoadChatModerators,
                LoadChatBans,
                LoadPrivateMessages,
                LoadPublicChatLastSeen,
                LoadModeratorActions,
                LoadUserStyleOverrides,
                LoadMediaQuotas,
                LoadKeepRequests,
                LoadReviewLikes,
                LoadReviewComments,
                LoadNotifiedItems,
                LoadSeenMediaKeys);
        }

        /// <summary>
        /// Queues an atomic, coalesced write of a snapshot. See <see cref="JsonFileWriter"/>.
        /// </summary>
        /// <remarks>
        /// The <paramref name="gate"/> parameter is retained so the many call sites keep compiling;
        /// JsonFileWriter serialises per file internally, so it is no longer used.
        /// </remarks>
        /// <typeparam name="T">Snapshot type.</typeparam>
        /// <param name="fileName">File name within the data directory.</param>
        /// <param name="snapshot">Already-captured snapshot to persist.</param>
        /// <param name="gate">Unused; kept for call-site compatibility.</param>
        /// <param name="label">Human-readable label for log messages.</param>
        /// <returns>Task.</returns>
        private Task WriteJsonAtomicAsync<T>(string fileName, T snapshot, SemaphoreSlim gate, string label)
        {
            _writer.Queue(fileName, snapshot, label);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Writes any debounced snapshots to disk immediately.
        /// </summary>
        /// <remarks>
        /// Must be called on shutdown, and before anything that reads these files straight off
        /// disk (backup export), otherwise the debounce window can hide the newest data.
        /// </remarks>
        /// <returns>Task.</returns>
        public Task FlushPendingWritesAsync() => _writer.FlushAsync();

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
                    var ratings = JsonSerializer.Deserialize<List<UserRating>>(json, ReadOptions);
                    if (ratings != null)
                    {
                        _ratings = ratings.ToDictionary(r => r.Id);
                        RebuildRatingIndexes();
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
        /// Rebuilds all secondary indexes for ratings. Must be called inside lock.
        /// </summary>
        private void RebuildRatingIndexes()
        {
            _ratingsByItemId = new Dictionary<Guid, List<UserRating>>();
            _ratingsByTmdbId = new Dictionary<string, List<UserRating>>();
            _ratingsByImdbId = new Dictionary<string, List<UserRating>>();
            _ratingsByAniDbId = new Dictionary<string, List<UserRating>>();

            foreach (var rating in _ratings.Values)
            {
                AddRatingToIndexes(rating);
            }
        }

        /// <summary>
        /// Adds a rating to all applicable indexes. Must be called inside lock.
        /// </summary>
        private void AddRatingToIndexes(UserRating rating)
        {
            // Index by ItemId
            if (!_ratingsByItemId.TryGetValue(rating.ItemId, out var itemList))
            {
                itemList = new List<UserRating>();
                _ratingsByItemId[rating.ItemId] = itemList;
            }

            itemList.Add(rating);

            // Index by TmdbId
            if (!string.IsNullOrEmpty(rating.TmdbId))
            {
                if (!_ratingsByTmdbId.TryGetValue(rating.TmdbId, out var tmdbList))
                {
                    tmdbList = new List<UserRating>();
                    _ratingsByTmdbId[rating.TmdbId] = tmdbList;
                }

                tmdbList.Add(rating);
            }

            // Index by ImdbId
            if (!string.IsNullOrEmpty(rating.ImdbId))
            {
                if (!_ratingsByImdbId.TryGetValue(rating.ImdbId, out var imdbList))
                {
                    imdbList = new List<UserRating>();
                    _ratingsByImdbId[rating.ImdbId] = imdbList;
                }

                imdbList.Add(rating);
            }

            // Index by AniDbId
            if (!string.IsNullOrEmpty(rating.AniDbId))
            {
                if (!_ratingsByAniDbId.TryGetValue(rating.AniDbId, out var anidbList))
                {
                    anidbList = new List<UserRating>();
                    _ratingsByAniDbId[rating.AniDbId] = anidbList;
                }

                anidbList.Add(rating);
            }
        }

        /// <summary>
        /// Removes a rating from all indexes. Must be called inside lock.
        /// </summary>
        private void RemoveRatingFromIndexes(UserRating rating)
        {
            // Remove from ItemId index
            if (_ratingsByItemId.TryGetValue(rating.ItemId, out var itemList))
            {
                itemList.Remove(rating);
                if (itemList.Count == 0)
                {
                    _ratingsByItemId.Remove(rating.ItemId);
                }
            }

            // Remove from TmdbId index
            if (!string.IsNullOrEmpty(rating.TmdbId) && _ratingsByTmdbId.TryGetValue(rating.TmdbId, out var tmdbList))
            {
                tmdbList.Remove(rating);
                if (tmdbList.Count == 0)
                {
                    _ratingsByTmdbId.Remove(rating.TmdbId);
                }
            }

            // Remove from ImdbId index
            if (!string.IsNullOrEmpty(rating.ImdbId) && _ratingsByImdbId.TryGetValue(rating.ImdbId, out var imdbList))
            {
                imdbList.Remove(rating);
                if (imdbList.Count == 0)
                {
                    _ratingsByImdbId.Remove(rating.ImdbId);
                }
            }

            // Remove from AniDbId index
            if (!string.IsNullOrEmpty(rating.AniDbId) && _ratingsByAniDbId.TryGetValue(rating.AniDbId, out var anidbList))
            {
                anidbList.Remove(rating);
                if (anidbList.Count == 0)
                {
                    _ratingsByAniDbId.Remove(rating.AniDbId);
                }
            }
        }

        /// <summary>
        /// Updates indexes when a rating's ItemId changes. Must be called inside lock.
        /// </summary>
        private void UpdateRatingItemIdIndex(UserRating rating, Guid oldItemId)
        {
            // Remove from old ItemId index
            if (_ratingsByItemId.TryGetValue(oldItemId, out var oldList))
            {
                oldList.Remove(rating);
                if (oldList.Count == 0)
                {
                    _ratingsByItemId.Remove(oldItemId);
                }
            }

            // Add to new ItemId index
            if (!_ratingsByItemId.TryGetValue(rating.ItemId, out var newList))
            {
                newList = new List<UserRating>();
                _ratingsByItemId[rating.ItemId] = newList;
            }

            newList.Add(rating);
        }

        /// <summary>
        /// Saves ratings to disk.
        /// </summary>
        private Task SaveRatingsAsync()
        {
            List<UserRating> snapshot;
            lock (_lock)
            {
                snapshot = _ratings.Values.ToList();
            }

            return WriteJsonAtomicAsync("ratings.json", snapshot, _ratingsWriteLock, "ratings");
        }

        /// <summary>
        /// Adds or updates a user rating.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="rating">Rating value.</param>
        /// <param name="tmdbId">Optional TMDB ID for fallback lookup.</param>
        /// <param name="imdbId">Optional IMDB ID for fallback lookup.</param>
        /// <param name="aniDbId">Optional AniDB ID for fallback lookup (anime).</param>
        /// <param name="reviewText">Optional review text.</param>
        /// <param name="snapshot">Optional title/year/type/poster to remember with the rating.</param>
        /// <returns>The created or updated rating.</returns>
        public async Task<UserRating> SetRatingAsync(Guid userId, Guid itemId, int rating, string? tmdbId = null, string? imdbId = null, string? aniDbId = null, string? reviewText = null, RatingSnapshot? snapshot = null)
        {
            lock (_lock)
            {
                var existing = FindUserRatingInternal(userId, itemId, tmdbId, imdbId, aniDbId);

                if (existing != null)
                {
                    existing.Rating = rating;
                    existing.UpdatedAt = DateTime.UtcNow;
                    // Update ItemId if it changed (media was replaced)
                    if (existing.ItemId != itemId)
                    {
                        var oldItemId = existing.ItemId;
                        existing.ItemId = itemId;
                        UpdateRatingItemIdIndex(existing, oldItemId);
                    }

                    // Update provider IDs if they were missing
                    if (string.IsNullOrEmpty(existing.TmdbId) && !string.IsNullOrEmpty(tmdbId))
                    {
                        existing.TmdbId = tmdbId;
                    }

                    if (string.IsNullOrEmpty(existing.ImdbId) && !string.IsNullOrEmpty(imdbId))
                    {
                        existing.ImdbId = imdbId;
                    }

                    if (string.IsNullOrEmpty(existing.AniDbId) && !string.IsNullOrEmpty(aniDbId))
                    {
                        existing.AniDbId = aniDbId;
                    }

                    // Update review text if provided
                    if (reviewText != null)
                    {
                        existing.ReviewText = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText;
                    }

                    ApplySnapshot(existing, snapshot);

                    _ = SaveRatingsAsync();
                    return existing;
                }

                var newRating = new UserRating
                {
                    UserId = userId,
                    ItemId = itemId,
                    Rating = rating,
                    TmdbId = tmdbId,
                    ImdbId = imdbId,
                    AniDbId = aniDbId,
                    ReviewText = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText
                };

                ApplySnapshot(newRating, snapshot);

                _ratings[newRating.Id] = newRating;
                AddRatingToIndexes(newRating);
                _ = SaveRatingsAsync();
                return newRating;
            }
        }

        /// <summary>
        /// Copies title/year/type/poster onto a rating, filling gaps without discarding what is
        /// already there.
        /// </summary>
        /// <remarks>
        /// Called on every rating write, so ratings saved before these fields existed get
        /// backfilled the next time the user touches them while the item is still in the library.
        /// A value already stored is never overwritten with an empty one - that is what keeps the
        /// snapshot alive once the item is gone.
        /// </remarks>
        private static void ApplySnapshot(UserRating rating, RatingSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.Title))
            {
                rating.Title = snapshot.Title;
            }

            if (snapshot.Year.HasValue)
            {
                rating.Year = snapshot.Year;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.MediaType))
            {
                rating.MediaType = snapshot.MediaType;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.PosterUrl))
            {
                rating.PosterUrl = snapshot.PosterUrl;
            }

            if (snapshot.IsExternal.HasValue)
            {
                rating.IsExternal = snapshot.IsExternal.Value;
            }
        }

        /// <summary>
        /// Stores a poster URL resolved after the fact (see the TMDB backfill in RatingsController).
        /// </summary>
        /// <param name="ratingId">Rating id.</param>
        /// <param name="posterUrl">Poster URL to store.</param>
        /// <returns>True if the rating existed and was updated.</returns>
        public bool SetRatingPoster(Guid ratingId, string posterUrl)
        {
            if (string.IsNullOrWhiteSpace(posterUrl))
            {
                return false;
            }

            lock (_lock)
            {
                if (!_ratings.TryGetValue(ratingId, out var rating))
                {
                    return false;
                }

                if (string.Equals(rating.PosterUrl, posterUrl, StringComparison.Ordinal))
                {
                    return false;
                }

                rating.PosterUrl = posterUrl;
                _ = SaveRatingsAsync();
                return true;
            }
        }

        /// <summary>
        /// Gets a user's rating for an item.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="tmdbId">Optional TMDB ID for fallback lookup.</param>
        /// <param name="imdbId">Optional IMDB ID for fallback lookup.</param>
        /// <param name="aniDbId">Optional AniDB ID for fallback lookup (anime).</param>
        /// <returns>The user's rating or null if not found.</returns>
        public UserRating? GetUserRating(Guid userId, Guid itemId, string? tmdbId = null, string? imdbId = null, string? aniDbId = null)
        {
            lock (_lock)
            {
                var rating = FindUserRatingInternal(userId, itemId, tmdbId, imdbId, aniDbId);

                // Auto-migrate ItemId if found by provider ID
                if (rating != null && rating.ItemId != itemId)
                {
                    var oldItemId = rating.ItemId;
                    rating.ItemId = itemId;

                    // The ItemId index has to follow the move, otherwise the rating stays filed
                    // under the old ItemId and every later lookup falls back to the provider scan.
                    UpdateRatingItemIdIndex(rating, oldItemId);
                    _ = SaveRatingsAsync();
                }

                return rating;
            }
        }

        /// <summary>
        /// Finds a single user's rating for an item, using the secondary indexes.
        /// </summary>
        /// <remarks>
        /// Previously this was up to four <c>_ratings.Values.FirstOrDefault(...)</c> scans over
        /// every rating in the system, on paths that run for every detail page view and every
        /// rating submission. Each index bucket holds only the ratings for one item / provider ID,
        /// so this is a handful of comparisons instead of a full sweep.
        /// Caller must hold <c>_lock</c>.
        /// </remarks>
        private UserRating? FindUserRatingInternal(Guid userId, Guid itemId, string? tmdbId, string? imdbId, string? aniDbId)
        {
            static UserRating? FirstByUser(List<UserRating>? bucket, Guid userId)
            {
                if (bucket == null)
                {
                    return null;
                }

                for (var i = 0; i < bucket.Count; i++)
                {
                    if (bucket[i].UserId == userId)
                    {
                        return bucket[i];
                    }
                }

                return null;
            }

            _ratingsByItemId.TryGetValue(itemId, out var byItem);
            var found = FirstByUser(byItem, userId);
            if (found != null)
            {
                return found;
            }

            if (!string.IsNullOrEmpty(tmdbId) && _ratingsByTmdbId.TryGetValue(tmdbId, out var byTmdb))
            {
                found = FirstByUser(byTmdb, userId);
                if (found != null)
                {
                    return found;
                }
            }

            if (!string.IsNullOrEmpty(imdbId) && _ratingsByImdbId.TryGetValue(imdbId, out var byImdb))
            {
                found = FirstByUser(byImdb, userId);
                if (found != null)
                {
                    return found;
                }
            }

            if (!string.IsNullOrEmpty(aniDbId) && _ratingsByAniDbId.TryGetValue(aniDbId, out var byAniDb))
            {
                found = FirstByUser(byAniDb, userId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all ratings for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="tmdbId">Optional TMDB ID for fallback lookup.</param>
        /// <param name="imdbId">Optional IMDB ID for fallback lookup.</param>
        /// <param name="aniDbId">Optional AniDB ID for fallback lookup (anime).</param>
        /// <returns>List of ratings for the item.</returns>
        public List<UserRating> GetItemRatings(Guid itemId, string? tmdbId = null, string? imdbId = null, string? aniDbId = null)
        {
            lock (_lock)
            {
                // Public callers get their own copy - the internal helper hands back the live
                // indexed list to avoid allocating on the batch-stats hot path.
                return GetItemRatingsInternal(itemId, tmdbId, imdbId, aniDbId).ToList();
            }
        }

        private static readonly List<UserRating> EmptyRatings = new List<UserRating>();

        /// <summary>
        /// Internal helper for GetItemRatings without lock (for use within already-locked methods).
        /// Uses secondary indexes for O(1) lookup instead of O(n) scan.
        /// </summary>
        /// <remarks>
        /// The returned list must be treated as READ-ONLY: on the common path it is the live list
        /// held by the index, not a copy. GetBatchRatingStats calls this once per item (up to 100
        /// per request), and the previous unconditional ToList() meant 100 list allocations per
        /// card batch. A copy is still taken on the rare provider-ID migration path, where
        /// UpdateRatingItemIdIndex mutates the very list being iterated.
        /// </remarks>
        private List<UserRating> GetItemRatingsInternal(Guid itemId, string? tmdbId, string? imdbId, string? aniDbId = null)
        {
            // Resolve via the indexes, preferring an exact ItemId hit and falling back to provider IDs.
            List<UserRating>? source = null;

            if (_ratingsByItemId.TryGetValue(itemId, out var itemList) && itemList.Count > 0)
            {
                source = itemList;
            }

            if (source == null && !string.IsNullOrEmpty(tmdbId)
                && _ratingsByTmdbId.TryGetValue(tmdbId, out var tmdbList) && tmdbList.Count > 0)
            {
                source = tmdbList;
            }

            if (source == null && !string.IsNullOrEmpty(imdbId)
                && _ratingsByImdbId.TryGetValue(imdbId, out var imdbList) && imdbList.Count > 0)
            {
                source = imdbList;
            }

            if (source == null && !string.IsNullOrEmpty(aniDbId)
                && _ratingsByAniDbId.TryGetValue(aniDbId, out var anidbList) && anidbList.Count > 0)
            {
                source = anidbList;
            }

            if (source == null)
            {
                return EmptyRatings;
            }

            // Fast path: nothing to migrate, hand back the live list without copying.
            var needsMigration = false;
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i].ItemId != itemId)
                {
                    needsMigration = true;
                    break;
                }
            }

            if (!needsMigration)
            {
                return source;
            }

            // Migration path: copy first, because UpdateRatingItemIdIndex removes entries from
            // the list we are iterating.
            var migrated = source.ToList();
            foreach (var rating in migrated)
            {
                if (rating.ItemId != itemId)
                {
                    var oldItemId = rating.ItemId;
                    rating.ItemId = itemId;
                    UpdateRatingItemIdIndex(rating, oldItemId);
                }
            }

            _ = SaveRatingsAsync();
            return migrated;
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
        /// Gets a snapshot of every stored rating (all users, all items). Used by the one-time
        /// "sync to Jellyfin" backfill.
        /// </summary>
        /// <returns>List of all ratings.</returns>
        public List<UserRating> GetAllRatings()
        {
            lock (_lock)
            {
                return _ratings.Values.ToList();
            }
        }

        /// <summary>
        /// Gets all distinct item IDs that have ratings with their average rating and count.
        /// Used for efficient library sorting by rating.
        /// </summary>
        /// <returns>Dictionary mapping ItemId to (AverageRating, RatingCount).</returns>
        public Dictionary<Guid, (double AverageRating, int RatingCount)> GetAllItemRatingStats()
        {
            lock (_lock)
            {
                return _ratings.Values
                    .GroupBy(r => r.ItemId)
                    .ToDictionary(
                        g => g.Key,
                        g => (Math.Round(g.Average(r => r.Rating), 2), g.Count()));
            }
        }

        /// <summary>
        /// Gets all items rated by a specific user with their personal rating.
        /// Used for efficient library sorting by personal rating.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <returns>Dictionary mapping ItemId to UserRating value.</returns>
        public Dictionary<Guid, int> GetUserRatingsMap(Guid userId)
        {
            lock (_lock)
            {
                return _ratings.Values
                    .Where(r => r.UserId == userId)
                    .ToDictionary(r => r.ItemId, r => r.Rating);
            }
        }

        /// <summary>
        /// Gets rating statistics for an item.
        /// </summary>
        /// <param name="itemId">Item ID.</param>
        /// <param name="userId">Optional user ID to include user's rating.</param>
        /// <param name="tmdbId">Optional TMDB ID for fallback lookup.</param>
        /// <param name="imdbId">Optional IMDB ID for fallback lookup.</param>
        /// <param name="aniDbId">Optional AniDB ID for fallback lookup (anime).</param>
        /// <returns>Rating statistics.</returns>
        public RatingStats GetRatingStats(Guid itemId, Guid? userId = null, string? tmdbId = null, string? imdbId = null, string? aniDbId = null)
        {
            lock (_lock)
            {
                // Use GetItemRatings which already handles provider ID fallback
                var itemRatings = GetItemRatingsInternal(itemId, tmdbId, imdbId, aniDbId);
                return BuildStats(itemId, itemRatings, userId);
            }
        }

        /// <summary>
        /// Builds rating statistics from a rating list in a single pass.
        /// </summary>
        /// <remarks>
        /// This used to walk the list twelve times: once for Average, ten times for the
        /// distribution buckets, and once more for the user's own rating. On a 100-item batch
        /// request that was 1200 enumerations instead of 100.
        /// </remarks>
        private static RatingStats BuildStats(Guid itemId, List<UserRating> itemRatings, Guid? userId)
        {
            var stats = new RatingStats
            {
                ItemId = itemId,
                TotalRatings = itemRatings.Count
            };

            if (itemRatings.Count == 0)
            {
                return stats;
            }

            var sum = 0L;
            for (var i = 0; i < itemRatings.Count; i++)
            {
                var rating = itemRatings[i];
                sum += rating.Rating;

                var bucket = rating.Rating - 1;
                if (bucket >= 0 && bucket < stats.Distribution.Length)
                {
                    stats.Distribution[bucket]++;
                }

                if (userId.HasValue && rating.UserId == userId.Value)
                {
                    stats.UserRating = rating.Rating;
                }
            }

            stats.AverageRating = Math.Round((double)sum / itemRatings.Count, 2);
            return stats;
        }

        /// <summary>
        /// Gets rating statistics for multiple items in a single lock acquisition.
        /// Much more efficient than calling GetRatingStats for each item separately.
        /// </summary>
        /// <param name="items">List of tuples containing (ItemId, TmdbId, ImdbId, AniDbId).</param>
        /// <param name="userId">Optional user ID to include user's rating.</param>
        /// <returns>Dictionary of item ID to rating statistics.</returns>
        public Dictionary<string, RatingStats> GetBatchRatingStats(
            List<(Guid ItemId, string? TmdbId, string? ImdbId, string? AniDbId)> items,
            Guid? userId = null)
        {
            var result = new Dictionary<string, RatingStats>();

            lock (_lock)
            {
                foreach (var (itemId, tmdbId, imdbId, aniDbId) in items)
                {
                    var itemRatings = GetItemRatingsInternal(itemId, tmdbId, imdbId, aniDbId);
                    result[itemId.ToString("N")] = BuildStats(itemId, itemRatings, userId);
                }
            }

            return result;
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
                var existing = FindUserRatingInternal(userId, itemId, null, null, null);
                if (existing != null)
                {
                    RemoveRatingFromIndexes(existing);
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
                    var requests = JsonSerializer.Deserialize<List<MediaRequest>>(json, ReadOptions);
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
        private Task SaveMediaRequestsAsync()
        {
            List<MediaRequest> snapshot;
            lock (_lock)
            {
                snapshot = _mediaRequests.Values.ToList();
            }

            return WriteJsonAtomicAsync("media_requests.json", snapshot, _requestsWriteLock, "media requests");
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

        // How long an item ID stays remembered as "already announced". Only needs to outlive the
        // notification recency window (see NotificationService); 60 days gives ample slack while
        // keeping the on-disk set tiny.
        private static readonly TimeSpan NotifiedItemRetention = TimeSpan.FromDays(60);

        /// <summary>
        /// Returns true if a "new media" notification has already been created for this item.
        /// Backed by disk so it survives restarts.
        /// </summary>
        /// <param name="itemId">The media item ID.</param>
        /// <returns>True if already announced.</returns>
        public bool HasNotifiedItem(Guid itemId)
        {
            lock (_lock)
            {
                return _notifiedItems.ContainsKey(itemId);
            }
        }

        /// <summary>
        /// Records that a "new media" notification has been created for this item, so it is never
        /// announced again. Prunes entries older than the retention window and persists to disk.
        /// </summary>
        /// <param name="itemId">The media item ID.</param>
        public void MarkItemNotified(Guid itemId)
        {
            lock (_lock)
            {
                _notifiedItems[itemId] = DateTime.UtcNow;

                // Prune long-expired entries so the file stays small.
                var cutoff = DateTime.UtcNow - NotifiedItemRetention;
                var expired = _notifiedItems.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
                foreach (var key in expired)
                {
                    _notifiedItems.Remove(key);
                }
            }

            _ = SaveNotifiedItemsAsync();
        }

        /// <summary>
        /// Loads the already-notified item set from disk.
        /// </summary>
        private void LoadNotifiedItems()
        {
            _notifiedItems = new Dictionary<Guid, DateTime>();
            try
            {
                var file = Path.Combine(_dataPath, "notified_items.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json, ReadOptions);
                    if (data != null)
                    {
                        var cutoff = DateTime.UtcNow - NotifiedItemRetention;
                        foreach (var kvp in data)
                        {
                            // Drop expired entries on load so the set cannot grow unbounded.
                            if (kvp.Value >= cutoff && Guid.TryParse(kvp.Key, out var itemId))
                            {
                                _notifiedItems[itemId] = kvp.Value;
                            }
                        }

                        _logger.LogInformation("Loaded {Count} notified-item entries", _notifiedItems.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notified items from disk");
            }
        }

        /// <summary>
        /// Saves the already-notified item set to disk.
        /// </summary>
        private Task SaveNotifiedItemsAsync()
        {
            Dictionary<string, DateTime> snapshot;
            lock (_lock)
            {
                snapshot = _notifiedItems.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
            }

            return WriteJsonAtomicAsync("notified_items.json", snapshot, _notifiedItemsWriteLock, "notified items");
        }

        // --- Seen-media baseline (stable content-key dedup for new-media notifications) ---
        //
        // These back the new-media notification "have we ever seen this file?" check. They are keyed
        // by a STABLE content key (path / provider-id, built in NotificationService.GetContentKey)
        // rather than the volatile item Guid, so a rescan that re-creates the same file under a new
        // internal Id and a reset DateCreated is still recognised as already-known and stays silent.

        /// <summary>
        /// Returns true if this content key has been seen before (existing library, previously
        /// announced, or a re-add of the same underlying file).
        /// </summary>
        /// <param name="key">Stable content key.</param>
        /// <returns>True if already seen.</returns>
        public bool HasSeenMedia(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_lock)
            {
                return _seenMediaKeys.ContainsKey(key);
            }
        }

        /// <summary>
        /// Records a single content key as seen and persists.
        /// </summary>
        /// <param name="key">Stable content key.</param>
        public void MarkSeenMedia(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (_lock)
            {
                _seenMediaKeys[key] = DateTime.UtcNow;
            }

            _ = SaveSeenMediaKeysAsync();
        }

        /// <summary>
        /// Records many content keys as seen in one shot and persists once. Used to seed the baseline
        /// from the existing library on first run so the whole pre-existing collection is silent.
        /// </summary>
        /// <param name="keys">Stable content keys.</param>
        public void MarkSeenMediaBulk(IEnumerable<string> keys)
        {
            if (keys == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                foreach (var key in keys)
                {
                    if (!string.IsNullOrEmpty(key))
                    {
                        _seenMediaKeys[key] = now;
                    }
                }
            }

            _ = SaveSeenMediaKeysAsync();
        }

        /// <summary>
        /// Returns true once the one-time new-media notification baseline has been established (i.e.
        /// the existing library has been recorded as known so it is never announced as new).
        /// </summary>
        /// <returns>True if the baseline marker exists on disk.</returns>
        public bool IsNotificationBaselineEstablished()
        {
            var marker = Path.Combine(_dataPath, "notification_baseline.marker");
            return File.Exists(marker);
        }

        /// <summary>
        /// Writes the baseline marker so the seeding step only ever runs once.
        /// </summary>
        public void SetNotificationBaselineEstablished()
        {
            try
            {
                var marker = Path.Combine(_dataPath, "notification_baseline.marker");
                File.WriteAllText(marker, DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing notification baseline marker");
            }
        }

        /// <summary>
        /// Loads the seen-media key set from disk.
        /// </summary>
        private void LoadSeenMediaKeys()
        {
            _seenMediaKeys = new Dictionary<string, DateTime>();
            try
            {
                var file = Path.Combine(_dataPath, "seen_media.json");
                if (File.Exists(file))
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json, ReadOptions);
                    if (data != null)
                    {
                        _seenMediaKeys = data;
                        _logger.LogInformation("Loaded {Count} seen-media keys", _seenMediaKeys.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading seen-media keys from disk");
            }
        }

        /// <summary>
        /// Saves the seen-media key set to disk.
        /// </summary>
        private Task SaveSeenMediaKeysAsync()
        {
            Dictionary<string, DateTime> snapshot;
            lock (_lock)
            {
                snapshot = new Dictionary<string, DateTime>(_seenMediaKeys);
            }

            return WriteJsonAtomicAsync("seen_media.json", snapshot, _seenMediaWriteLock, "seen-media keys");
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
                    var deletions = JsonSerializer.Deserialize<List<ScheduledDeletion>>(json, ReadOptions);
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
        private Task SaveScheduledDeletionsAsync()
        {
            List<ScheduledDeletion> snapshot;
            lock (_lock)
            {
                snapshot = _scheduledDeletions.Values.ToList();
            }

            return WriteJsonAtomicAsync("scheduled_deletions.json", snapshot, _deletionsWriteLock, "scheduled deletions");
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
            bool found;
            lock (_lock)
            {
                if (_scheduledDeletions.ContainsKey(itemId))
                {
                    var deletion = _scheduledDeletions[itemId];
                    deletion.IsCancelled = true;
                    deletion.CancelledAt = DateTime.UtcNow;
                    _ = SaveScheduledDeletionsAsync();
                    found = true;
                }
                else
                {
                    found = false;
                }
            }

            // Clean up keep requests when deletion is cancelled
            if (found)
            {
                await RemoveKeepRequestsForItemAsync(itemId).ConfigureAwait(false);
            }

            return found;
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
            bool found;
            lock (_lock)
            {
                if (_scheduledDeletions.ContainsKey(itemId))
                {
                    _scheduledDeletions.Remove(itemId);
                    _ = SaveScheduledDeletionsAsync();
                    found = true;
                }
                else
                {
                    found = false;
                }
            }

            // Clean up keep requests when deletion is removed
            if (found)
            {
                await RemoveKeepRequestsForItemAsync(itemId).ConfigureAwait(false);
            }

            return found;
        }

        // Keep Request Methods (for "Ask to not delete" feature)

        /// <summary>
        /// Loads keep requests from disk.
        /// </summary>
        private void LoadKeepRequests()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "keep_requests.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var requests = JsonSerializer.Deserialize<List<KeepRequest>>(json, ReadOptions);
                    if (requests != null)
                    {
                        _keepRequests = requests;
                        _logger.LogInformation("Loaded {Count} keep requests from disk", _keepRequests.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading keep requests from disk");
            }
        }

        /// <summary>
        /// Saves keep requests to disk.
        /// </summary>
        private Task SaveKeepRequestsAsync()
        {
            List<KeepRequest> snapshot;
            lock (_lock)
            {
                snapshot = _keepRequests.ToList();
            }

            return WriteJsonAtomicAsync("keep_requests.json", snapshot, _keepRequestsWriteLock, "keep requests");
        }

        /// <summary>
        /// Loads review likes from disk.
        /// </summary>
        private void LoadReviewLikes()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "review_likes.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var likes = JsonSerializer.Deserialize<List<ReviewLike>>(json, ReadOptions);
                    if (likes != null)
                    {
                        _reviewLikes = likes.ToDictionary(l => l.Id);
                        _logger.LogInformation("Loaded {Count} review likes from disk", _reviewLikes.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading review likes from disk");
            }
        }

        /// <summary>
        /// Saves review likes to disk.
        /// </summary>
        private Task SaveReviewLikesAsync()
        {
            List<ReviewLike> snapshot;
            lock (_lock)
            {
                snapshot = _reviewLikes.Values.ToList();
            }

            return WriteJsonAtomicAsync("review_likes.json", snapshot, _reviewLikesWriteLock, "review likes");
        }

        /// <summary>
        /// Loads review comments from disk.
        /// </summary>
        private void LoadReviewComments()
        {
            try
            {
                var filePath = Path.Combine(_dataPath, "review_comments.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var comments = JsonSerializer.Deserialize<List<ReviewComment>>(json, ReadOptions);
                    if (comments != null)
                    {
                        _reviewComments = comments.ToDictionary(c => c.Id);
                        _logger.LogInformation("Loaded {Count} review comments from disk", _reviewComments.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading review comments from disk");
            }
        }

        /// <summary>
        /// Saves review comments to disk.
        /// </summary>
        private Task SaveReviewCommentsAsync()
        {
            List<ReviewComment> snapshot;
            lock (_lock)
            {
                snapshot = _reviewComments.Values.ToList();
            }

            return WriteJsonAtomicAsync("review_comments.json", snapshot, _reviewCommentsWriteLock, "review comments");
        }

        /// <summary>
        /// Adds a comment to a review.
        /// </summary>
        /// <param name="comment">The review comment to add.</param>
        /// <returns>The created comment.</returns>
        public async Task<ReviewComment> AddReviewCommentAsync(ReviewComment comment)
        {
            lock (_lock)
            {
                _reviewComments[comment.Id] = comment;
            }

            await SaveReviewCommentsAsync().ConfigureAwait(false);
            return comment;
        }

        /// <summary>
        /// Gets all comments for a specific review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID of the review.</param>
        /// <returns>List of comments for the review.</returns>
        public List<ReviewComment> GetReviewComments(Guid reviewerUserId, Guid itemId)
        {
            lock (_lock)
            {
                return _reviewComments.Values
                    .Where(c => c.ReviewerUserId == reviewerUserId && c.ItemId == itemId)
                    .OrderBy(c => c.CreatedAt)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets the comment count for a specific review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID of the review.</param>
        /// <returns>Number of comments on the review.</returns>
        public int GetReviewCommentCount(Guid reviewerUserId, Guid itemId)
        {
            lock (_lock)
            {
                return _reviewComments.Values
                    .Count(c => c.ReviewerUserId == reviewerUserId && c.ItemId == itemId);
            }
        }

        /// <summary>
        /// Deletes a review comment.
        /// </summary>
        /// <param name="commentId">Comment ID to delete.</param>
        /// <param name="userId">User requesting deletion (must be comment owner).</param>
        /// <returns>True if deleted, false otherwise.</returns>
        public async Task<bool> DeleteReviewCommentAsync(Guid commentId, Guid userId)
        {
            lock (_lock)
            {
                if (_reviewComments.TryGetValue(commentId, out var comment))
                {
                    if (comment.CommenterId == userId)
                    {
                        _reviewComments.Remove(commentId);
                        _ = SaveReviewCommentsAsync();
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Sets or updates a like/dislike on a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID of the review.</param>
        /// <param name="userId">User ID who is liking/disliking.</param>
        /// <param name="isLike">True for like, false for dislike.</param>
        /// <returns>The created or updated ReviewLike.</returns>
        public async Task<ReviewLike> SetReviewLikeAsync(Guid reviewerUserId, Guid itemId, Guid userId, bool isLike)
        {
            lock (_lock)
            {
                // Can't like your own review
                if (reviewerUserId == userId)
                {
                    throw new InvalidOperationException("Cannot like your own review");
                }

                // Find existing like/dislike
                var existing = _reviewLikes.Values.FirstOrDefault(l =>
                    l.ReviewerUserId == reviewerUserId &&
                    l.ItemId == itemId &&
                    l.UserId == userId);

                if (existing != null)
                {
                    // Toggle off if same action
                    if (existing.IsLike == isLike)
                    {
                        _reviewLikes.Remove(existing.Id);
                        _ = SaveReviewLikesAsync();
                        return existing;
                    }

                    // Update to opposite action
                    existing.IsLike = isLike;
                    existing.CreatedAt = DateTime.UtcNow;
                    _ = SaveReviewLikesAsync();
                    return existing;
                }

                // Create new like/dislike
                var newLike = new ReviewLike
                {
                    ReviewerUserId = reviewerUserId,
                    ItemId = itemId,
                    UserId = userId,
                    IsLike = isLike
                };

                _reviewLikes[newLike.Id] = newLike;
                _ = SaveReviewLikesAsync();
                return newLike;
            }
        }

        /// <summary>
        /// Gets the like/dislike counts for a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID of the review.</param>
        /// <returns>Tuple of (likeCount, dislikeCount).</returns>
        public (int LikeCount, int DislikeCount) GetReviewLikeCounts(Guid reviewerUserId, Guid itemId)
        {
            lock (_lock)
            {
                var likes = _reviewLikes.Values.Where(l =>
                    l.ReviewerUserId == reviewerUserId &&
                    l.ItemId == itemId);

                return (likes.Count(l => l.IsLike), likes.Count(l => !l.IsLike));
            }
        }

        /// <summary>
        /// Gets the current user's like/dislike status for a review.
        /// </summary>
        /// <param name="reviewerUserId">User ID of the review owner.</param>
        /// <param name="itemId">Item ID of the review.</param>
        /// <param name="userId">Current user's ID.</param>
        /// <returns>True if liked, false if disliked, null if no vote.</returns>
        public bool? GetUserReviewLike(Guid reviewerUserId, Guid itemId, Guid userId)
        {
            lock (_lock)
            {
                var like = _reviewLikes.Values.FirstOrDefault(l =>
                    l.ReviewerUserId == reviewerUserId &&
                    l.ItemId == itemId &&
                    l.UserId == userId);

                return like?.IsLike;
            }
        }

        /// <summary>
        /// Updates a rating's review text.
        /// </summary>
        /// <param name="userId">User ID.</param>
        /// <param name="itemId">Item ID.</param>
        /// <param name="reviewText">Review text (null to clear).</param>
        /// <returns>The updated rating or null if not found.</returns>
        public async Task<UserRating?> UpdateReviewTextAsync(Guid userId, Guid itemId, string? reviewText)
        {
            lock (_lock)
            {
                var rating = _ratings.Values.FirstOrDefault(r => r.UserId == userId && r.ItemId == itemId);
                if (rating == null)
                {
                    return null;
                }

                rating.ReviewText = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText;
                rating.UpdatedAt = DateTime.UtcNow;
                _ = SaveRatingsAsync();
                return rating;
            }
        }

        /// <summary>
        /// Adds a keep request for an item.
        /// </summary>
        /// <param name="request">The keep request.</param>
        /// <returns>The created keep request, or null if user already requested today.</returns>
        public async Task<KeepRequest?> AddKeepRequestAsync(KeepRequest request)
        {
            lock (_lock)
            {
                // Check if user already requested for this item today
                var today = DateTime.UtcNow.Date;
                var existingToday = _keepRequests.Any(r =>
                    r.ItemId == request.ItemId &&
                    r.UserId == request.UserId &&
                    r.RequestedAt.Date == today);

                if (existingToday)
                {
                    return null; // User already requested today
                }

                _keepRequests.Add(request);
            }

            _ = SaveKeepRequestsAsync();
            return request;
        }

        /// <summary>
        /// Gets the count of keep requests for an item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Number of keep requests.</returns>
        public int GetKeepRequestCount(Guid itemId)
        {
            lock (_lock)
            {
                return _keepRequests.Count(r => r.ItemId == itemId);
            }
        }

        /// <summary>
        /// Gets all keep requests for an item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>List of keep requests.</returns>
        public List<KeepRequest> GetKeepRequestsForItem(Guid itemId)
        {
            lock (_lock)
            {
                return _keepRequests.Where(r => r.ItemId == itemId).ToList();
            }
        }

        /// <summary>
        /// Checks if a user has already requested to keep an item today.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if user already requested today.</returns>
        public bool HasUserRequestedKeepToday(Guid itemId, Guid userId)
        {
            lock (_lock)
            {
                var today = DateTime.UtcNow.Date;
                return _keepRequests.Any(r =>
                    r.ItemId == itemId &&
                    r.UserId == userId &&
                    r.RequestedAt.Date == today);
            }
        }

        /// <summary>
        /// Checks if a user has ever requested to keep an item.
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <param name="userId">The user ID.</param>
        /// <returns>True if user has requested.</returns>
        public bool HasUserRequestedKeep(Guid itemId, Guid userId)
        {
            lock (_lock)
            {
                return _keepRequests.Any(r => r.ItemId == itemId && r.UserId == userId);
            }
        }

        /// <summary>
        /// Removes all keep requests for an item (when deletion is cancelled or executed).
        /// </summary>
        /// <param name="itemId">The item ID.</param>
        /// <returns>Number of requests removed.</returns>
        public async Task<int> RemoveKeepRequestsForItemAsync(Guid itemId)
        {
            int removed;
            lock (_lock)
            {
                removed = _keepRequests.RemoveAll(r => r.ItemId == itemId);
            }

            if (removed > 0)
            {
                _ = SaveKeepRequestsAsync();
            }

            return removed;
        }

        /// <summary>
        /// Gets keep request counts for multiple items.
        /// </summary>
        /// <returns>Dictionary of item IDs to request counts.</returns>
        public Dictionary<Guid, int> GetAllKeepRequestCounts()
        {
            lock (_lock)
            {
                return _keepRequests
                    .GroupBy(r => r.ItemId)
                    .ToDictionary(g => g.Key, g => g.Count());
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
                    var requests = JsonSerializer.Deserialize<List<DeletionRequest>>(json, ReadOptions);
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
        private Task SaveDeletionRequestsAsync()
        {
            List<DeletionRequest> snapshot;
            lock (_lock)
            {
                snapshot = _deletionRequests.Values.ToList();
            }

            return WriteJsonAtomicAsync("deletion_requests.json", snapshot, _deletionRequestsWriteLock, "deletion requests");
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
                    var bans = JsonSerializer.Deserialize<List<UserBan>>(json, ReadOptions);
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
        private Task SaveUserBansAsync()
        {
            List<UserBan> snapshot;
            lock (_lock)
            {
                snapshot = _userBans.Values.ToList();
            }

            return WriteJsonAtomicAsync("user_bans.json", snapshot, _userBansWriteLock, "user bans");
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
                    var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json, ReadOptions);
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
        private Task SaveChatMessagesAsync()
        {
            List<ChatMessage> snapshot;
            lock (_lock)
            {
                snapshot = _chatMessages.ToList();
            }

            return WriteJsonAtomicAsync("chat_messages.json", snapshot, _chatMessagesWriteLock, "chat messages");
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
                    var users = JsonSerializer.Deserialize<List<ChatUser>>(json, ReadOptions);
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
        private Task SaveChatUsersAsync()
        {
            List<ChatUser> snapshot;
            lock (_lock)
            {
                snapshot = _chatUsers.Values.ToList();
            }

            return WriteJsonAtomicAsync("chat_users.json", snapshot, _chatUsersWriteLock, "chat users");
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
                    var mods = JsonSerializer.Deserialize<List<ChatModerator>>(json, ReadOptions);
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
        private Task SaveChatModeratorsAsync()
        {
            List<ChatModerator> snapshot;
            lock (_lock)
            {
                snapshot = _chatModerators.Values.ToList();
            }

            return WriteJsonAtomicAsync("chat_moderators.json", snapshot, _chatModeratorsWriteLock, "chat moderators");
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
                    var bans = JsonSerializer.Deserialize<List<ChatBan>>(json, ReadOptions);
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
        private Task SaveChatBansAsync()
        {
            List<ChatBan> snapshot;
            lock (_lock)
            {
                snapshot = _chatBans.Values.ToList();
            }

            return WriteJsonAtomicAsync("chat_bans.json", snapshot, _chatBansWriteLock, "chat bans");
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
                    var messages = JsonSerializer.Deserialize<List<PrivateMessage>>(json, ReadOptions);
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
        private Task SavePrivateMessagesAsync()
        {
            List<PrivateMessage> snapshot;
            lock (_lock)
            {
                snapshot = _privateMessages.ToList();
            }

            return WriteJsonAtomicAsync("private_messages.json", snapshot, _privateMessagesWriteLock, "private messages");
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
                    var data = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json, ReadOptions);
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
        private Task SavePublicChatLastSeenAsync()
        {
            Dictionary<string, DateTime> snapshot;
            lock (_lock)
            {
                snapshot = _publicChatLastSeen.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value);
            }

            return WriteJsonAtomicAsync("public_chat_last_seen.json", snapshot, _publicChatLastSeenWriteLock, "public chat last seen");
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
        /// Deletes entire DM conversation between two users (admin only).
        /// Used to clean up conversations with deleted accounts.
        /// </summary>
        public async Task<int> DeleteDMConversationAsync(Guid userId1, Guid userId2)
        {
            int deletedCount = 0;
            lock (_lock)
            {
                // Find all messages between these two users (both directions)
                var messagesToDelete = _privateMessages.Where(m =>
                    !m.IsDeleted &&
                    ((m.SenderId == userId1 && m.RecipientId == userId2) ||
                     (m.SenderId == userId2 && m.RecipientId == userId1))
                ).ToList();

                foreach (var msg in messagesToDelete)
                {
                    msg.IsDeleted = true;
                    msg.DeletedAt = DateTime.UtcNow;
                    deletedCount++;
                }

                if (deletedCount > 0)
                {
                    _ = SavePrivateMessagesAsync();
                }
            }
            return deletedCount;
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
                    var actions = JsonSerializer.Deserialize<List<ModeratorAction>>(json, ReadOptions);
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
        private Task SaveModeratorActionsAsync()
        {
            List<ModeratorAction> snapshot;
            lock (_lock)
            {
                snapshot = _moderatorActions.ToList();
            }

            return WriteJsonAtomicAsync("moderator_actions.json", snapshot, _moderatorActionsWriteLock, "moderator actions");
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
                    var styles = JsonSerializer.Deserialize<List<UserStyleOverride>>(json, ReadOptions);
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
        private Task SaveUserStyleOverridesAsync()
        {
            List<UserStyleOverride> snapshot;
            lock (_lock)
            {
                snapshot = _userStyleOverrides.Values.ToList();
            }

            return WriteJsonAtomicAsync("user_style_overrides.json", snapshot, _userStyleOverridesWriteLock, "user style overrides");
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
                    var quotas = JsonSerializer.Deserialize<List<MediaQuota>>(json, ReadOptions);
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
        private Task SaveMediaQuotasAsync()
        {
            List<MediaQuota> snapshot;
            lock (_lock)
            {
                snapshot = _mediaQuotas.Values.ToList();
            }

            return WriteJsonAtomicAsync("media_quotas.json", snapshot, _mediaQuotasWriteLock, "media quotas");
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

        /// <summary>
        /// Gets recent ratings sorted by timestamp.
        /// </summary>
        /// <param name="limit">Maximum number of ratings to return.</param>
        /// <returns>List of recent ratings.</returns>
        public List<UserRating> GetRecentRatings(int limit = 10)
        {
            lock (_lock)
            {
                return _ratings.Values
                    .OrderByDescending(r => r.UpdatedAt)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets all unique user IDs who have rated items.
        /// </summary>
        /// <returns>Set of user IDs.</returns>
        public HashSet<Guid> GetAllUserIds()
        {
            lock (_lock)
            {
                return _ratings.Values
                    .Select(r => r.UserId)
                    .Distinct()
                    .ToHashSet();
            }
        }

        /// <summary>
        /// Gets the count of ratings that have review text.
        /// </summary>
        /// <returns>Number of reviews.</returns>
        public int GetReviewCount()
        {
            lock (_lock)
            {
                return _ratings.Values
                    .Count(r => !string.IsNullOrWhiteSpace(r.ReviewText));
            }
        }

        /// <summary>
        /// Gets recent media requests ordered by creation date.
        /// </summary>
        /// <param name="limit">Maximum number of requests to return.</param>
        /// <returns>List of recent media requests.</returns>
        public List<MediaRequest> GetRecentMediaRequests(int limit = 10)
        {
            lock (_lock)
            {
                return _mediaRequests.Values
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <summary>
        /// Gets recent review comments ordered by creation date.
        /// </summary>
        /// <param name="limit">Maximum number of comments to return.</param>
        /// <returns>List of recent review comments.</returns>
        public List<ReviewComment> GetRecentReviewComments(int limit = 10)
        {
            lock (_lock)
            {
                return _reviewComments.Values
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(limit)
                    .ToList();
            }
        }
    }
}
