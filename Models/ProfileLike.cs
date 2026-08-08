using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the profile being liked.
        /// </summary>
        [JsonPropertyName("profileUserId")]
        public Guid ProfileUserId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who liked the profile.
        /// </summary>
        [JsonPropertyName("likerUserId")]
        public Guid LikerUserId { get; set; }

        /// <summary>
        /// Gets or sets when the like was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
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
