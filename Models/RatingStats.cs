using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents rating statistics for a media item.
    /// </summary>
    public class RatingStats
    {
        /// <summary>
        /// Gets or sets the item ID.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the average rating.
        /// </summary>
        [JsonPropertyName("averageRating")]
        public double AverageRating { get; set; }

        /// <summary>
        /// Gets or sets the total number of ratings.
        /// </summary>
        [JsonPropertyName("totalRatings")]
        public int TotalRatings { get; set; }

        /// <summary>
        /// Gets or sets the user's rating (if applicable).
        /// </summary>
        [JsonPropertyName("userRating")]
        public int? UserRating { get; set; }

        /// <summary>
        /// Gets or sets the rating distribution (count for each rating value 1-10).
        /// </summary>
        [JsonPropertyName("distribution")]
        public int[] Distribution { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RatingStats"/> class.
        /// </summary>
        public RatingStats()
        {
            Distribution = new int[10];
        }
    }
}
