using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user's custom media list (e.g., "Top 10 Movies", "Favorites").
    /// </summary>
    public class UserMediaList
    {
        /// <summary>
        /// Gets or sets the unique list ID.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who owns this list.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the list title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list type (Movies, Series, Mixed).
        /// </summary>
        [JsonPropertyName("listType")]
        public string ListType { get; set; } = "Mixed";

        /// <summary>
        /// Gets or sets whether this list is visible to regular (non-friend) users.
        /// </summary>
        [JsonPropertyName("visibleToRegularUsers")]
        public bool VisibleToRegularUsers { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this list is visible to friends.
        /// </summary>
        [JsonPropertyName("visibleToFriends")]
        public bool VisibleToFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of items in this list (up to 50).
        /// </summary>
        [JsonPropertyName("maxItems")]
        public int MaxItems { get; set; } = 10;

        /// <summary>
        /// Gets or sets the sort order for displaying lists.
        /// </summary>
        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets whether this is a special "Favorites" list.
        /// </summary>
        [JsonPropertyName("isFavorites")]
        public bool IsFavorites { get; set; }

        /// <summary>
        /// Gets or sets whether this is a special "Watchlist" list.
        /// </summary>
        [JsonPropertyName("isWatchlist")]
        public bool IsWatchlist { get; set; }

        /// <summary>
        /// Gets or sets the user ID this list was cloned from (if any).
        /// </summary>
        [JsonPropertyName("clonedFromUserId")]
        public Guid? ClonedFromUserId { get; set; }

        /// <summary>
        /// Gets or sets the username this list was cloned from (cached).
        /// </summary>
        [JsonPropertyName("clonedFromUsername")]
        public string? ClonedFromUsername { get; set; }

        /// <summary>
        /// Gets or sets whether this list is deleted (soft delete).
        /// </summary>
        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets when the list was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the list was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMediaList"/> class.
        /// </summary>
        public UserMediaList()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
