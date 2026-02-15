using System;

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
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's avatar URL.
        /// </summary>
        public string? Avatar { get; set; }

        /// <summary>
        /// Gets or sets when the user was last seen.
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Gets or sets whether the user is currently typing.
        /// </summary>
        public bool IsTyping { get; set; }

        /// <summary>
        /// Gets or sets when the user started typing.
        /// </summary>
        public DateTime? TypingStarted { get; set; }

        /// <summary>
        /// Gets or sets the last message ID the user has seen.
        /// </summary>
        public Guid? LastSeenMessageId { get; set; }

        /// <summary>
        /// Gets or sets whether this user is a moderator.
        /// </summary>
        public bool IsModerator { get; set; }

        /// <summary>
        /// Gets or sets whether this user is an admin.
        /// </summary>
        public bool IsAdmin { get; set; }
    }
}
