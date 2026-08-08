using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who owns this featured review.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the reviewed media.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the position (1-3 for up to 3 featured reviews).
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        /// <summary>
        /// Gets or sets when this was featured.
        /// </summary>
        [JsonPropertyName("featuredAt")]
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
