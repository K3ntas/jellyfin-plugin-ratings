using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents cached metadata for an IMDB item.
    /// </summary>
    public class ImdbCacheItem
    {
        /// <summary>
        /// Gets or sets the IMDB ID (e.g., tt1234567).
        /// </summary>
        public string ImdbId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original title (if different).
        /// </summary>
        public string? OriginalTitle { get; set; }

        /// <summary>
        /// Gets or sets the overview/description.
        /// </summary>
        public string? Overview { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the media type (Movie, Series).
        /// </summary>
        public string MediaType { get; set; } = "Movie";

        /// <summary>
        /// Gets or sets the poster image URL.
        /// </summary>
        public string? PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets the backdrop image URL.
        /// </summary>
        public string? BackdropUrl { get; set; }

        /// <summary>
        /// Gets or sets the genres.
        /// </summary>
        public List<string> Genres { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the runtime in minutes.
        /// </summary>
        public int? RuntimeMinutes { get; set; }

        /// <summary>
        /// Gets or sets the IMDB rating.
        /// </summary>
        public double? ImdbRating { get; set; }

        /// <summary>
        /// Gets or sets the content rating (e.g., PG-13, R).
        /// </summary>
        public string? ContentRating { get; set; }

        /// <summary>
        /// Gets or sets the directors.
        /// </summary>
        public List<string> Directors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the main cast.
        /// </summary>
        public List<string> Cast { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets when this cache entry was created.
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Gets or sets whether the fetch was successful.
        /// </summary>
        public bool FetchSuccess { get; set; }

        /// <summary>
        /// Gets or sets the error message if fetch failed.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImdbCacheItem"/> class.
        /// </summary>
        public ImdbCacheItem()
        {
            CachedAt = DateTime.UtcNow;
        }
    }
}
