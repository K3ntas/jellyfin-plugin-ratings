using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a notification about new media being added.
    /// </summary>
    public class NewMediaNotification
    {
        /// <summary>
        /// Gets or sets the unique identifier for this notification.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the media item ID.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the media title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media type (Movie, Series, Episode, etc.).
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the year of the media.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the series name (for episodes).
        /// </summary>
        public string? SeriesName { get; set; }

        /// <summary>
        /// Gets or sets the season number (for episodes).
        /// </summary>
        public int? SeasonNumber { get; set; }

        /// <summary>
        /// Gets or sets the episode number (for single episodes).
        /// </summary>
        public int? EpisodeNumber { get; set; }

        /// <summary>
        /// Gets or sets the list of episode numbers (for grouped episode notifications).
        /// </summary>
        public List<int>? EpisodeNumbers { get; set; }

        /// <summary>
        /// Gets or sets the image URL for the media.
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Gets or sets when this notification was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether this is a test notification.
        /// </summary>
        public bool IsTest { get; set; }

        /// <summary>
        /// Gets or sets the custom message for admin test notifications.
        /// </summary>
        public string? Message { get; set; }
    }
}
