using System;

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
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the title of the media.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the production year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the type of media (Movie, Series, etc.).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the primary image URL.
        /// </summary>
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of times this item has been played.
        /// </summary>
        public long PlayCount { get; set; }

        /// <summary>
        /// Gets or sets the total watch time in minutes across all users.
        /// </summary>
        public long TotalWatchTimeMinutes { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the average rating from the plugin's rating system.
        /// </summary>
        public double? AverageRating { get; set; }

        /// <summary>
        /// Gets or sets the total number of ratings.
        /// </summary>
        public int RatingCount { get; set; }

        /// <summary>
        /// Gets or sets the date when the item was added to the library.
        /// </summary>
        public DateTime? DateAdded { get; set; }

        /// <summary>
        /// Gets or sets scheduled deletion information if the item is scheduled for deletion.
        /// </summary>
        public ScheduledDeletion? ScheduledDeletion { get; set; }
    }
}
