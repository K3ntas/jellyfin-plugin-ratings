using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a chat moderator assignment.
    /// </summary>
    public class ChatModerator
    {
        /// <summary>
        /// Gets or sets the unique ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who is a moderator.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets who assigned this moderator.
        /// </summary>
        public Guid AssignedBy { get; set; }

        /// <summary>
        /// Gets or sets when the moderator was assigned.
        /// </summary>
        public DateTime AssignedAt { get; set; }

        /// <summary>
        /// Gets or sets whether moderator can delete messages.
        /// </summary>
        public bool CanDeleteMessages { get; set; } = true;

        /// <summary>
        /// Gets or sets whether moderator can snooze users.
        /// </summary>
        public bool CanSnoozeUsers { get; set; } = true;

        /// <summary>
        /// Gets or sets whether moderator can temp ban users.
        /// </summary>
        public bool CanTempBan { get; set; } = true;
    }
}
