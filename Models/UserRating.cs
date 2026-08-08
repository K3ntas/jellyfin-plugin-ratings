using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user rating for a media item.
    /// </summary>
    public class UserRating
    {
        /// <summary>
        /// Gets or sets the unique identifier for this rating.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who made the rating.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the item ID being rated.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the TMDB ID for fallback lookup when item ID changes.
        /// </summary>
        [JsonPropertyName("tmdbId")]
        public string? TmdbId { get; set; }

        /// <summary>
        /// Gets or sets the IMDB ID for fallback lookup when item ID changes.
        /// </summary>
        [JsonPropertyName("imdbId")]
        public string? ImdbId { get; set; }

        /// <summary>
        /// Gets or sets the AniDB ID for fallback lookup when item ID changes (anime libraries).
        /// </summary>
        [JsonPropertyName("aniDbId")]
        public string? AniDbId { get; set; }

        /// <summary>
        /// Gets or sets the rating value (1-10).
        /// </summary>
        [JsonPropertyName("rating")]
        public int Rating { get; set; }

        /// <summary>
        /// Gets or sets the optional review text.
        /// </summary>
        [JsonPropertyName("reviewText")]
        public string? ReviewText { get; set; }

        // ---- Snapshot of what was rated, captured when the rating is made ----
        // A rating used to carry nothing but IDs, so when the film or show was later removed from
        // the library there was nothing left to show: the entry survived as a row of stars with no
        // title and no poster. These fields keep the rating meaningful on its own, and they are
        // also what makes it possible to rate something that is not in the library at all.
        // All are nullable so ratings saved by older versions load unchanged and get backfilled
        // the next time their item is seen.

        /// <summary>
        /// Gets or sets the title as it was when rated.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the release year as it was when rated.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the media type ("Movie" or "Series").
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets a poster URL that survives the item leaving the library.
        /// </summary>
        /// <remarks>
        /// Jellyfin's own image URL dies with the item, so for library items this is filled in
        /// from TMDB in the background after the rating is saved (when a TMDB token is configured
        /// and the item has a TMDB id). Ratings created directly from a TMDB search already carry
        /// the poster the search returned.
        /// </remarks>
        [JsonPropertyName("posterUrl")]
        public string? PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this rating was made against a TMDB title that
        /// is not on the server.
        /// </summary>
        /// <remarks>
        /// The rating is filed under a deterministic ItemId derived from the TMDB id, so if the
        /// title is later added to the library the existing provider-id fallback migrates the
        /// rating onto the real item automatically.
        /// </remarks>
        [JsonPropertyName("isExternal")]
        public bool IsExternal { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rating was created.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the rating was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserRating"/> class.
        /// </summary>
        public UserRating()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
