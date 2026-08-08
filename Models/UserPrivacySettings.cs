using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Privacy settings for a user profile with granular visibility controls.
    /// </summary>
    public class UserPrivacySettings
    {
        // ============ LEGACY SETTINGS (for backward compatibility) ============

        /// <summary>
        /// Gets or sets profile visibility. Values: Public, Friends, Private.
        /// </summary>
        [JsonPropertyName("profileVisibility")]
        public string ProfileVisibility { get; set; } = "Public";

        /// <summary>
        /// Gets or sets who can see online status. Values: Everyone, Friends, Nobody.
        /// </summary>
        [JsonPropertyName("showOnlineStatus")]
        public string ShowOnlineStatus { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can see watched history. Values: Everyone, Friends, Nobody.
        /// </summary>
        [JsonPropertyName("showWatchedHistory")]
        public string ShowWatchedHistory { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can see friends list. Values: Everyone, Friends, Nobody.
        /// </summary>
        [JsonPropertyName("showFriendsList")]
        public string ShowFriendsList { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can see currently watching. Values: Everyone, Friends, Nobody.
        /// </summary>
        [JsonPropertyName("showCurrentlyWatching")]
        public string ShowCurrentlyWatching { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can send friend requests. Values: Everyone, Nobody.
        /// </summary>
        [JsonPropertyName("allowFriendRequests")]
        public string AllowFriendRequests { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can send messages. Values: Everyone, Friends, Nobody.
        /// </summary>
        [JsonPropertyName("allowMessages")]
        public string AllowMessages { get; set; } = "Everyone";

        // ============ GRANULAR VISIBILITY (two-column: Regular Users / Friends) ============

        /// <summary>
        /// Gets or sets whether ratings are visible to regular users.
        /// </summary>
        [JsonPropertyName("ratingsVisibleRegular")]
        public bool RatingsVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether ratings are visible to friends.
        /// </summary>
        [JsonPropertyName("ratingsVisibleFriends")]
        public bool RatingsVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether reviews are visible to regular users.
        /// </summary>
        [JsonPropertyName("reviewsVisibleRegular")]
        public bool ReviewsVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether reviews are visible to friends.
        /// </summary>
        [JsonPropertyName("reviewsVisibleFriends")]
        public bool ReviewsVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether lists are visible to regular users.
        /// </summary>
        [JsonPropertyName("listsVisibleRegular")]
        public bool ListsVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether lists are visible to friends.
        /// </summary>
        [JsonPropertyName("listsVisibleFriends")]
        public bool ListsVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether diary is visible to regular users.
        /// </summary>
        [JsonPropertyName("diaryVisibleRegular")]
        public bool DiaryVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether diary is visible to friends.
        /// </summary>
        [JsonPropertyName("diaryVisibleFriends")]
        public bool DiaryVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether stats are visible to regular users.
        /// </summary>
        [JsonPropertyName("statsVisibleRegular")]
        public bool StatsVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether stats are visible to friends.
        /// </summary>
        [JsonPropertyName("statsVisibleFriends")]
        public bool StatsVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether followers list is visible to regular users.
        /// </summary>
        [JsonPropertyName("followersVisibleRegular")]
        public bool FollowersVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether followers list is visible to friends.
        /// </summary>
        [JsonPropertyName("followersVisibleFriends")]
        public bool FollowersVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether following list is visible to regular users.
        /// </summary>
        [JsonPropertyName("followingVisibleRegular")]
        public bool FollowingVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether following list is visible to friends.
        /// </summary>
        [JsonPropertyName("followingVisibleFriends")]
        public bool FollowingVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether profile likers are visible to regular users.
        /// </summary>
        [JsonPropertyName("profileLikersVisibleRegular")]
        public bool ProfileLikersVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether profile likers are visible to friends.
        /// </summary>
        [JsonPropertyName("profileLikersVisibleFriends")]
        public bool ProfileLikersVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether online status is visible to regular users.
        /// </summary>
        [JsonPropertyName("onlineStatusVisibleRegular")]
        public bool OnlineStatusVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether online status is visible to friends.
        /// </summary>
        [JsonPropertyName("onlineStatusVisibleFriends")]
        public bool OnlineStatusVisibleFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets whether watch history is visible to regular users.
        /// </summary>
        [JsonPropertyName("watchHistoryVisibleRegular")]
        public bool WatchHistoryVisibleRegular { get; set; } = true;

        /// <summary>
        /// Gets or sets whether watch history is visible to friends.
        /// </summary>
        [JsonPropertyName("watchHistoryVisibleFriends")]
        public bool WatchHistoryVisibleFriends { get; set; } = true;

        // ============ NOTIFICATION PREFERENCES ============

        /// <summary>
        /// Gets or sets whether to notify on profile like.
        /// </summary>
        [JsonPropertyName("notifyOnProfileLike")]
        public bool NotifyOnProfileLike { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to notify on new follower.
        /// </summary>
        [JsonPropertyName("notifyOnNewFollower")]
        public bool NotifyOnNewFollower { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to notify on review like.
        /// </summary>
        [JsonPropertyName("notifyOnReviewLike")]
        public bool NotifyOnReviewLike { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to notify on review comment.
        /// </summary>
        [JsonPropertyName("notifyOnReviewComment")]
        public bool NotifyOnReviewComment { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserPrivacySettings"/> class with secure defaults.
        /// </summary>
        public UserPrivacySettings()
        {
        }
    }
}
