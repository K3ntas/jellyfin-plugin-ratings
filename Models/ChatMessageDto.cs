using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// DTO for sending a chat message.
    /// </summary>
    public class ChatMessageDto
    {
        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GIF URL (optional).
        /// </summary>
        public string? GifUrl { get; set; }

        /// <summary>
        /// Gets or sets the ID of message being replied to (optional).
        /// </summary>
        public Guid? ReplyToId { get; set; }
    }
}
