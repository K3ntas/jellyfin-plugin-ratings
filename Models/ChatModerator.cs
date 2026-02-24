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

        /// <summary>
        /// Gets or sets the moderator level (1, 2, or 3).
        /// Level 1: Snooze, delete messages, change styles
        /// Level 2: All Level 1 + temp ban
        /// Level 3: All Level 2 + perm ban, media ban, manage mods 1-2
        /// </summary>
        public int Level { get; set; } = 1;

        /// <summary>
        /// Gets or sets the daily delete count (for rate limiting).
        /// </summary>
        public int DailyDeleteCount { get; set; }

        /// <summary>
        /// Gets or sets when the daily delete counter resets.
        /// </summary>
        public DateTime DailyDeleteReset { get; set; } = DateTime.UtcNow.Date.AddDays(1);
    }
}
