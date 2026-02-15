using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a chat ban or snooze for a user.
    /// </summary>
    public class ChatBan
    {
        /// <summary>
        /// Gets or sets the unique ban ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the banned user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the banned username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ban type: "chat" (can't send messages), "snooze" (temporary mute), "media" (can't watch media).
        /// </summary>
        public string BanType { get; set; } = "chat";

        /// <summary>
        /// Gets or sets the reason for the ban.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets who issued the ban.
        /// </summary>
        public Guid BannedBy { get; set; }

        /// <summary>
        /// Gets or sets the name of who issued the ban.
        /// </summary>
        public string BannedByName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the ban was issued.
        /// </summary>
        public DateTime BannedAt { get; set; }

        /// <summary>
        /// Gets or sets when the ban expires (null = permanent).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets whether this is a permanent ban.
        /// </summary>
        public bool IsPermanent { get; set; }

        /// <summary>
        /// Gets or sets whether the ban is currently active.
        /// </summary>
        public bool IsActive => IsPermanent || (ExpiresAt.HasValue && ExpiresAt.Value > DateTime.UtcNow);
    }
}
