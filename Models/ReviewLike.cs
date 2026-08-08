using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the reviewer (owner of the review being liked).
        /// </summary>
        [JsonPropertyName("reviewerUserId")]
        public Guid ReviewerUserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the review.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who liked/disliked.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a like (true) or dislike (false).
        /// </summary>
        [JsonPropertyName("isLike")]
        public bool IsLike { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the like was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
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
