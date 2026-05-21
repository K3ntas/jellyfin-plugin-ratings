using System;

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
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who owns this list.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the list title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list type (Movies, Series, Mixed).
        /// </summary>
        public string ListType { get; set; } = "Mixed";

        /// <summary>
        /// Gets or sets whether this list is visible to regular (non-friend) users.
        /// </summary>
        public bool VisibleToRegularUsers { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this list is visible to friends.
        /// </summary>
        public bool VisibleToFriends { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of items in this list (up to 50).
        /// </summary>
        public int MaxItems { get; set; } = 10;

        /// <summary>
        /// Gets or sets the sort order for displaying lists.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets whether this is a special "Favorites" list.
        /// </summary>
        public bool IsFavorites { get; set; }

        /// <summary>
        /// Gets or sets whether this is a special "Watchlist" list.
        /// </summary>
        public bool IsWatchlist { get; set; }

        /// <summary>
        /// Gets or sets the user ID this list was cloned from (if any).
        /// </summary>
        public Guid? ClonedFromUserId { get; set; }

        /// <summary>
        /// Gets or sets the username this list was cloned from (cached).
        /// </summary>
        public string? ClonedFromUsername { get; set; }

        /// <summary>
        /// Gets or sets whether this list is deleted (soft delete).
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets when the list was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the list was last updated.
        /// </summary>
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
