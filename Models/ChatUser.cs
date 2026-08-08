using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a chat user's presence and status.
    /// </summary>
    public class ChatUser
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's avatar URL.
        /// </summary>
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }

        /// <summary>
        /// Gets or sets when the user was last seen.
        /// </summary>
        [JsonPropertyName("lastSeen")]
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Gets or sets whether the user is currently typing.
        /// </summary>
        [JsonPropertyName("isTyping")]
        public bool IsTyping { get; set; }

        /// <summary>
        /// Gets or sets when the user started typing.
        /// </summary>
        [JsonPropertyName("typingStarted")]
        public DateTime? TypingStarted { get; set; }

        /// <summary>
        /// Gets or sets the last message ID the user has seen.
        /// </summary>
        [JsonPropertyName("lastSeenMessageId")]
        public Guid? LastSeenMessageId { get; set; }

        /// <summary>
        /// Gets or sets whether this user is a moderator.
        /// </summary>
        [JsonPropertyName("isModerator")]
        public bool IsModerator { get; set; }

        /// <summary>
        /// Gets or sets whether this user is an admin.
        /// </summary>
        [JsonPropertyName("isAdmin")]
        public bool IsAdmin { get; set; }
    }
}
