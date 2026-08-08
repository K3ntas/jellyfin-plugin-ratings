using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Request body for rating a TMDB title that is not on the server.
    /// </summary>
    public class ExternalRatingDto
    {
        /// <summary>
        /// Gets or sets the TMDB numeric id of the title.
        /// </summary>
        [Required]
        public string TmdbId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media type ("Movie" or "Series"). Anything else is treated as Movie.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the title, stored with the rating so it displays without a library item.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the poster URL from the TMDB search result. Only TMDB image URLs are kept.
        /// </summary>
        public string? PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets the rating value.
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// Gets or sets an optional review.
        /// </summary>
        public string? Review { get; set; }
    }
}
