using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID of who sent the request.
        /// </summary>
        [JsonPropertyName("fromUserId")]
        public Guid FromUserId { get; set; }

        /// <summary>
        /// Gets or sets the username of who sent the request.
        /// </summary>
        [JsonPropertyName("fromUsername")]
        public string FromUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID of who received the request.
        /// </summary>
        [JsonPropertyName("toUserId")]
        public Guid ToUserId { get; set; }

        /// <summary>
        /// Gets or sets the username of who received the request.
        /// </summary>
        [JsonPropertyName("toUsername")]
        public string ToUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request status. Values: pending, accepted, rejected.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Gets or sets when the request was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the request was resolved (accepted/rejected).
        /// </summary>
        [JsonPropertyName("resolvedAt")]
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
