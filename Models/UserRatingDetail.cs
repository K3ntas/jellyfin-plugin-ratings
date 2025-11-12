using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a detailed user rating including username.
    /// </summary>
    public class UserRatingDetail
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rating value.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rating was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
