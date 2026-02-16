using System.ComponentModel.DataAnnotations;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Data transfer object for creating media requests.
    /// </summary>
    public class MediaRequestDto
    {
        /// <summary>
        /// Gets or sets the title of the requested media.
        /// </summary>
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of media (e.g., Movie, TV Series, Anime).
        /// </summary>
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional notes or details about the request.
        /// </summary>
        [MaxLength(2000)]
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets custom fields as JSON string (key-value pairs).
        /// </summary>
        [MaxLength(5000)]
        public string CustomFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMDB code (e.g., tt0448134).
        /// </summary>
        [MaxLength(50)]
        public string ImdbCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full IMDB link.
        /// </summary>
        [MaxLength(500)]
        public string ImdbLink { get; set; } = string.Empty;
    }
}
