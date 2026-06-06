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

        /// <summary>
        /// Gets or sets a value indicating whether this title is NOT on the server (added from the
        /// external catalog). Such items show a "Request" action on hover instead of opening.
        /// </summary>
        public bool NotInLibrary { get; set; }

        /// <summary>
        /// Gets or sets the TMDB id for a not-in-library item (used to request it).
        /// </summary>
        public string TmdbId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the release year for a not-in-library item.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the media type ("Movie"/"Series") for a not-in-library item.
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a short description/overview (shown in the hover info popup).
        /// </summary>
        public string Overview { get; set; } = string.Empty;
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
        /// Gets or sets the uploaded header background media URL (a looping GIF or video shown
        /// behind the username/picture). Empty when none is set.
        /// </summary>
        public string HeaderMediaUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the header media kind: "gif", "video", or empty.
        /// </summary>
        public string HeaderMediaType { get; set; } = string.Empty;

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
