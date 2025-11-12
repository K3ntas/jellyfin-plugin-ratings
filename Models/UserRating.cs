using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user rating for a media item.
    /// </summary>
    public class UserRating
    {
        /// <summary>
        /// Gets or sets the unique identifier for this rating.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who made the rating.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID being rated.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the rating value (1-10).
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rating was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rating was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRating"/> class.
        /// </summary>
        public UserRating()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
