using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user ban from submitting requests.
    /// </summary>
    public class UserBan
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the banned user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the banned user's username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ban type: "media_request" or "deletion_request".
        /// </summary>
        public string BanType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the ban was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the ban expires. Null means permanent.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the admin who issued the ban.
        /// </summary>
        public string BannedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the ban has been manually lifted.
        /// </summary>
        public bool IsLifted { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserBan"/> class.
        /// </summary>
        public UserBan()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
