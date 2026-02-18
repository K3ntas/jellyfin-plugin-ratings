using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a private (direct) message between two users.
    /// </summary>
    public class PrivateMessage
    {
        /// <summary>
        /// Gets or sets the unique message ID.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the sender's user ID.
        /// </summary>
        public Guid SenderId { get; set; }

        /// <summary>
        /// Gets or sets the sender's username.
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sender's avatar URL.
        /// </summary>
        public string? SenderAvatar { get; set; }

        /// <summary>
        /// Gets or sets the recipient's user ID.
        /// </summary>
        public Guid RecipientId { get; set; }

        /// <summary>
        /// Gets or sets the recipient's username.
        /// </summary>
        public string RecipientName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the message content (sanitized).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GIF URL if message contains a GIF.
        /// </summary>
        public string? GifUrl { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when message was sent.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether the message has been read by recipient.
        /// </summary>
        public bool IsRead { get; set; }

        /// <summary>
        /// Gets or sets whether the message has been deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets when the message was deleted.
        /// </summary>
        public DateTime? DeletedAt { get; set; }
    }
}
