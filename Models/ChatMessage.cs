using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who sent the message.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username of the sender.
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's avatar URL.
        /// </summary>
        [JsonPropertyName("userAvatar")]
        public string? UserAvatar { get; set; }

        /// <summary>
        /// Gets or sets the message content (sanitized).
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GIF URL if message contains a GIF.
        /// </summary>
        [JsonPropertyName("gifUrl")]
        public string? GifUrl { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when message was sent.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets whether the message has been deleted.
        /// </summary>
        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets the ID of message being replied to.
        /// </summary>
        [JsonPropertyName("replyToId")]
        public Guid? ReplyToId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who deleted this message (if deleted).
        /// </summary>
        [JsonPropertyName("deletedBy")]
        public Guid? DeletedBy { get; set; }

        /// <summary>
        /// Gets or sets when the message was deleted.
        /// </summary>
        [JsonPropertyName("deletedAt")]
        public DateTime? DeletedAt { get; set; }
    }
}
