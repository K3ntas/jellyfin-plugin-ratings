using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a blocked user relationship.
    /// </summary>
    public class BlockedUser
    {
        /// <summary>
        /// Gets or sets the unique identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who blocked.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who is blocked.
        /// </summary>
        public Guid BlockedUserId { get; set; }

        /// <summary>
        /// Gets or sets when the block was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
