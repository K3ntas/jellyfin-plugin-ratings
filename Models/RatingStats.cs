using System;

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
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the average rating.
        /// </summary>
        public double AverageRating { get; set; }

        /// <summary>
        /// Gets or sets the total number of ratings.
        /// </summary>
        public int TotalRatings { get; set; }

        /// <summary>
        /// Gets or sets the user's rating (if applicable).
        /// </summary>
        public int? UserRating { get; set; }

        /// <summary>
        /// Gets or sets the rating distribution (count for each rating value 1-10).
        /// </summary>
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
