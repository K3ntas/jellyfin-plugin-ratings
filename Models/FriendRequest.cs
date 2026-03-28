using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a friend request between two users.
    /// </summary>
    public class FriendRequest
    {
        /// <summary>
        /// Gets or sets the unique request ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of who sent the request.
        /// </summary>
        public Guid FromUserId { get; set; }

        /// <summary>
        /// Gets or sets the username of who sent the request.
        /// </summary>
        public string FromUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID of who received the request.
        /// </summary>
        public Guid ToUserId { get; set; }

        /// <summary>
        /// Gets or sets the username of who received the request.
        /// </summary>
        public string ToUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request status. Values: pending, accepted, rejected.
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Gets or sets when the request was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the request was resolved (accepted/rejected).
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FriendRequest"/> class.
        /// </summary>
        public FriendRequest()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
