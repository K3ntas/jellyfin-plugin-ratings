using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a comment on a user review.
    /// </summary>
    public class ReviewComment
    {
        /// <summary>
        /// Gets or sets the unique identifier for this comment.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the reviewer (owner of the review being commented on).
        /// </summary>
        [JsonPropertyName("reviewerUserId")]
        public Guid ReviewerUserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the review.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who wrote this comment.
        /// </summary>
        [JsonPropertyName("commenterId")]
        public Guid CommenterId { get; set; }

        /// <summary>
        /// Gets or sets the comment text.
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the comment was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReviewComment"/> class.
        /// </summary>
        public ReviewComment()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
