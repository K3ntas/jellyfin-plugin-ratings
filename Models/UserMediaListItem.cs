using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents an item in a user's media list.
    /// </summary>
    public class UserMediaListItem
    {
        /// <summary>
        /// Gets or sets the unique item ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the list ID this item belongs to.
        /// </summary>
        public Guid ListId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin item ID (null if external item).
        /// </summary>
        public Guid? ItemId { get; set; }

        /// <summary>
        /// Gets or sets the IMDB ID for external items not on server.
        /// </summary>
        public string? ImdbId { get; set; }

        /// <summary>
        /// Gets or sets the cached title.
        /// </summary>
        public string CachedTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cached image URL.
        /// </summary>
        public string? CachedImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the cached overview/description.
        /// </summary>
        public string? CachedOverview { get; set; }

        /// <summary>
        /// Gets or sets the cached release year.
        /// </summary>
        public int? CachedYear { get; set; }

        /// <summary>
        /// Gets or sets the cached genres as JSON array.
        /// </summary>
        public string? CachedGenres { get; set; }

        /// <summary>
        /// Gets or sets the cached media type (Movie, Series).
        /// </summary>
        public string? CachedMediaType { get; set; }

        /// <summary>
        /// Gets or sets when the metadata was cached.
        /// </summary>
        public DateTime? CachedAt { get; set; }

        /// <summary>
        /// Gets or sets an optional note/mini-review for this item.
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// Gets or sets the position/order in the list.
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Gets or sets when this item was added to the list.
        /// </summary>
        public DateTime AddedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMediaListItem"/> class.
        /// </summary>
        public UserMediaListItem()
        {
            Id = Guid.NewGuid();
            AddedAt = DateTime.UtcNow;
        }
    }
}
