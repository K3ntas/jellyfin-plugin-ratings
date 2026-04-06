using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a like or dislike on a review.
    /// </summary>
    public class ReviewLike
    {
        /// <summary>
        /// Gets or sets the unique identifier for this like.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the reviewer (owner of the review being liked).
        /// </summary>
        public Guid ReviewerUserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the review.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who liked/disliked.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a like (true) or dislike (false).
        /// </summary>
        public bool IsLike { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the like was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewLike"/> class.
        /// </summary>
        public ReviewLike()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
