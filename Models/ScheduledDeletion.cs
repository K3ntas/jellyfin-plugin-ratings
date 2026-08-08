using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a scheduled deletion for a media item.
    /// </summary>
    public class ScheduledDeletion
    {
        /// <summary>
        /// Gets or sets the unique identifier for this scheduled deletion.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the media to be deleted.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the title of the media item.
        /// </summary>
        [JsonPropertyName("itemTitle")]
        public string ItemTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of the media item (Movie, Series, etc.).
        /// </summary>
        [JsonPropertyName("itemType")]
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user ID who scheduled the deletion.
        /// </summary>
        [JsonPropertyName("scheduledByUserId")]
        public Guid ScheduledByUserId { get; set; }

        /// <summary>
        /// Gets or sets the username who scheduled the deletion.
        /// </summary>
        [JsonPropertyName("scheduledByUsername")]
        public string ScheduledByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the deletion was scheduled.
        /// </summary>
        [JsonPropertyName("scheduledAt")]
        public DateTime ScheduledAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the item should be deleted.
        /// </summary>
        [JsonPropertyName("deleteAt")]
        public DateTime DeleteAt { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the deletion has been cancelled.
        /// </summary>
        [JsonPropertyName("isCancelled")]
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the deletion was cancelled.
        /// </summary>
        [JsonPropertyName("cancelledAt")]
        public DateTime? CancelledAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledDeletion"/> class.
        /// </summary>
        public ScheduledDeletion()
        {
            Id = Guid.NewGuid();
            ScheduledAt = DateTime.UtcNow;
        }
    }
}
