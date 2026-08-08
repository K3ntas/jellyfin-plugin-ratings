using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents media item statistics for the admin management view.
    /// </summary>
    public class MediaItemStats
    {
        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the title of the media.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the production year.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the type of media (Movie, Series, etc.).
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the primary image URL.
        /// </summary>
        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of times this item has been played.
        /// </summary>
        [JsonPropertyName("playCount")]
        public long PlayCount { get; set; }

        /// <summary>
        /// Gets or sets the total watch time in minutes across all users.
        /// </summary>
        [JsonPropertyName("totalWatchTimeMinutes")]
        public long TotalWatchTimeMinutes { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        [JsonPropertyName("fileSizeBytes")]
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the average rating from the plugin's rating system.
        /// </summary>
        [JsonPropertyName("averageRating")]
        public double? AverageRating { get; set; }

        /// <summary>
        /// Gets or sets the total number of ratings.
        /// </summary>
        [JsonPropertyName("ratingCount")]
        public int RatingCount { get; set; }

        /// <summary>
        /// Gets or sets the date when the item was added to the library.
        /// </summary>
        [JsonPropertyName("dateAdded")]
        public DateTime? DateAdded { get; set; }

        /// <summary>
        /// Gets or sets scheduled deletion information if the item is scheduled for deletion.
        /// </summary>
        [JsonPropertyName("scheduledDeletion")]
        public ScheduledDeletion? ScheduledDeletion { get; set; }
    }
}
