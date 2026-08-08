using System;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the banned user ID.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the banned user's username.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ban type: "media_request" or "deletion_request".
        /// </summary>
        [JsonPropertyName("banType")]
        public string BanType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the ban was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the ban expires. Null means permanent.
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the admin who issued the ban.
        /// </summary>
        [JsonPropertyName("bannedByUsername")]
        public string BannedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the ban has been manually lifted.
        /// </summary>
        [JsonPropertyName("isLifted")]
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
