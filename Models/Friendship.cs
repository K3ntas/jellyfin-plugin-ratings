using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the first user's ID.
        /// </summary>
        [JsonPropertyName("userId1")]
        public Guid UserId1 { get; set; }

        /// <summary>
        /// Gets or sets the second user's ID.
        /// </summary>
        [JsonPropertyName("userId2")]
        public Guid UserId2 { get; set; }

        /// <summary>
        /// Gets or sets when the friendship was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
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
