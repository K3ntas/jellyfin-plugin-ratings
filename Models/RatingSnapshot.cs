using System.Text.Json.Serialization;
namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// What was rated, as it looked at the moment of rating.
    /// </summary>
    /// <remarks>
    /// A rating used to be nothing but identifiers, so once the film or show left the library there
    /// was nothing left to display - the entry survived as a row of stars with no title and no
    /// poster. Carrying this alongside the rating keeps it meaningful on its own, and is also what
    /// allows a title that is not on the server at all to be rated.
    /// Every field is optional: whatever is supplied is filled in, and anything already stored is
    /// never replaced by a blank.
    /// </remarks>
    public class RatingSnapshot
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the release year.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the media type ("Movie" or "Series").
        /// </summary>
        [JsonPropertyName("mediaType")]
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets a poster URL that outlives the item.
        /// </summary>
        [JsonPropertyName("posterUrl")]
        public string? PosterUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the rated title is not in the library.
        /// </summary>
        [JsonPropertyName("isExternal")]
        public bool? IsExternal { get; set; }
    }
}
