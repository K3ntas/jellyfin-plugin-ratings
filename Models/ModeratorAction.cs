using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a logged moderator action for audit trail.
    /// </summary>
    public class ModeratorAction
    {
        /// <summary>
        /// Gets or sets the unique ID.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the moderator's user ID.
        /// </summary>
        public Guid ModeratorId { get; set; }

        /// <summary>
        /// Gets or sets the moderator's username.
        /// </summary>
        public string ModeratorName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the moderator's level at time of action.
        /// </summary>
        public int ModeratorLevel { get; set; }

        /// <summary>
        /// Gets or sets the action type.
        /// Values: "delete_message", "snooze", "temp_ban", "perm_ban", "media_ban", "add_mod", "remove_mod", "set_quota", "change_style".
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the target user ID.
        /// </summary>
        public Guid TargetUserId { get; set; }

        /// <summary>
        /// Gets or sets the target username.
        /// </summary>
        public string TargetUserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional details as JSON.
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// Gets or sets when the action was performed.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
