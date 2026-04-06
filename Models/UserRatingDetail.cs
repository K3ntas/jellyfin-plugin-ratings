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

        /// <summary>
        /// Gets or sets the review text.
        /// </summary>
        public string? ReviewText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this rating has a review.
        /// </summary>
        public bool HasReview { get; set; }

        /// <summary>
        /// Gets or sets the number of likes on the review.
        /// </summary>
        public int LikeCount { get; set; }

        /// <summary>
        /// Gets or sets the number of dislikes on the review.
        /// </summary>
        public int DislikeCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the current user liked this review.
        /// </summary>
        public bool? UserLiked { get; set; }
    }
}
