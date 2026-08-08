using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a social notification for a user.
    /// </summary>
    public class SocialNotification
    {
        /// <summary>
        /// Gets or sets the unique notification ID.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who receives this notification.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the notification type (FriendRequest, FriendAccepted, FriendRemoved).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notification message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional data for the notification.
        /// </summary>
        [JsonPropertyName("data")]
        public Dictionary<string, string> Data { get; set; }

        /// <summary>
        /// Gets or sets whether the notification has been read.
        /// </summary>
        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; }

        /// <summary>
        /// Gets or sets when the notification was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialNotification"/> class.
        /// </summary>
        public SocialNotification()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            Data = new Dictionary<string, string>();
            IsRead = false;
        }
    }
}
