using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a chat message.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Gets or sets the unique message ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who sent the message.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username of the sender.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's avatar URL.
        /// </summary>
        public string? UserAvatar { get; set; }

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
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets whether the message has been deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the ID of message being replied to.
        /// </summary>
        public Guid? ReplyToId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who deleted this message (if deleted).
        /// </summary>
        public Guid? DeletedBy { get; set; }

        /// <summary>
        /// Gets or sets when the message was deleted.
        /// </summary>
        public DateTime? DeletedAt { get; set; }
    }
}
