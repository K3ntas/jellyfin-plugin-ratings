using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a moderator-set style override for a user in chat.
    /// </summary>
    public class UserStyleOverride
    {
        /// <summary>
        /// Gets or sets the target user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the target username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the nickname/username color (hex format, e.g., "#FF5733").
        /// </summary>
        public string? NicknameColor { get; set; }

        /// <summary>
        /// Gets or sets the message text color (hex format, e.g., "#FFFFFF").
        /// </summary>
        public string? MessageColor { get; set; }

        /// <summary>
        /// Gets or sets the text style.
        /// Values: "", "bold", "italic", "bold-italic".
        /// </summary>
        public string TextStyle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets who set this override.
        /// </summary>
        public Guid SetBy { get; set; }

        /// <summary>
        /// Gets or sets the name of who set this override.
        /// </summary>
        public string SetByName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when this override was set.
        /// </summary>
        public DateTime SetAt { get; set; } = DateTime.UtcNow;
    }
}
