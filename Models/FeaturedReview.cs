using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a pinned/featured review on a user's profile.
    /// </summary>
    public class FeaturedReview
    {
        /// <summary>
        /// Gets or sets the unique ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who owns this featured review.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the reviewed media.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the position (1-3 for up to 3 featured reviews).
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Gets or sets when this was featured.
        /// </summary>
        public DateTime FeaturedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeaturedReview"/> class.
        /// </summary>
        public FeaturedReview()
        {
            Id = Guid.NewGuid();
            FeaturedAt = DateTime.UtcNow;
        }
    }
}
