using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a favorite item on user profile.
    /// </summary>
    public class FavoriteItem
    {
        /// <summary>
        /// Gets or sets the Jellyfin item ID.
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image URL.
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a row of favorite items on user profile.
    /// </summary>
    public class FavoriteRow
    {
        /// <summary>
        /// Gets or sets the row title (e.g., "Favorite Movies", "Top Anime").
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the items in this row (up to 5).
        /// </summary>
        public List<FavoriteItem> Items { get; set; } = new List<FavoriteItem>();
    }

    /// <summary>
    /// Represents a user's social profile.
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// Gets or sets the unique profile ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin user ID this profile belongs to.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's bio/description.
        /// </summary>
        public string Bio { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the avatar URL.
        /// </summary>
        public string AvatarUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the profile was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the profile was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the user's privacy settings.
        /// </summary>
        public UserPrivacySettings Privacy { get; set; }

        /// <summary>
        /// Gets or sets the user's favorite items (legacy, for backwards compatibility).
        /// </summary>
        public List<FavoriteItem> Favorites { get; set; }

        /// <summary>
        /// Gets or sets the user's favorite rows (up to 5 rows, each with up to 5 items).
        /// </summary>
        public List<FavoriteRow> FavoriteRows { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProfile"/> class.
        /// </summary>
        public UserProfile()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            Privacy = new UserPrivacySettings();
            Favorites = new List<FavoriteItem>();
            FavoriteRows = new List<FavoriteRow>();
        }
    }
}
