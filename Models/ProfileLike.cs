using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a like on a user's profile.
    /// </summary>
    public class ProfileLike
    {
        /// <summary>
        /// Gets or sets the unique like ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the profile being liked.
        /// </summary>
        public Guid ProfileUserId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who liked the profile.
        /// </summary>
        public Guid LikerUserId { get; set; }

        /// <summary>
        /// Gets or sets when the like was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProfileLike"/> class.
        /// </summary>
        public ProfileLike()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
