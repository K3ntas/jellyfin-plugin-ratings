using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a friendship between two users.
    /// </summary>
    public class Friendship
    {
        /// <summary>
        /// Gets or sets the unique friendship ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the first user's ID.
        /// </summary>
        public Guid UserId1 { get; set; }

        /// <summary>
        /// Gets or sets the second user's ID.
        /// </summary>
        public Guid UserId2 { get; set; }

        /// <summary>
        /// Gets or sets when the friendship was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Friendship"/> class.
        /// </summary>
        public Friendship()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
