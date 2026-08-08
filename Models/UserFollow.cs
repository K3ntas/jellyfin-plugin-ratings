using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a one-way follow relationship between users.
    /// </summary>
    public class UserFollow
    {
        /// <summary>
        /// Gets or sets the unique follow ID.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user who is following.
        /// </summary>
        [JsonPropertyName("followerId")]
        public Guid FollowerId { get; set; }

        /// <summary>
        /// Gets or sets the ID of the user being followed.
        /// </summary>
        [JsonPropertyName("followingId")]
        public Guid FollowingId { get; set; }

        /// <summary>
        /// Gets or sets when the follow was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserFollow"/> class.
        /// </summary>
        public UserFollow()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
