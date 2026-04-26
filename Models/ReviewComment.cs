using System;

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
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of the reviewer (owner of the review being commented on).
        /// </summary>
        public Guid ReviewerUserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the review.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who wrote this comment.
        /// </summary>
        public Guid CommenterId { get; set; }

        /// <summary>
        /// Gets or sets the comment text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the comment was created.
        /// </summary>
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
