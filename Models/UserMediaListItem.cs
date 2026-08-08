using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents an item in a user's media list.
    /// </summary>
    public class UserMediaListItem
    {
        /// <summary>
        /// Gets or sets the unique item ID.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the list ID this item belongs to.
        /// </summary>
        [JsonPropertyName("listId")]
        public Guid ListId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin item ID (null if external item).
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid? ItemId { get; set; }

        /// <summary>
        /// Gets or sets the IMDB ID for external items not on server.
        /// </summary>
        [JsonPropertyName("imdbId")]
        public string? ImdbId { get; set; }

        /// <summary>
        /// Gets or sets the cached title.
        /// </summary>
        [JsonPropertyName("cachedTitle")]
        public string CachedTitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cached image URL.
        /// </summary>
        [JsonPropertyName("cachedImageUrl")]
        public string? CachedImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the cached overview/description.
        /// </summary>
        [JsonPropertyName("cachedOverview")]
        public string? CachedOverview { get; set; }

        /// <summary>
        /// Gets or sets the cached release year.
        /// </summary>
        [JsonPropertyName("cachedYear")]
        public int? CachedYear { get; set; }

        /// <summary>
        /// Gets or sets the cached genres as JSON array.
        /// </summary>
        [JsonPropertyName("cachedGenres")]
        public string? CachedGenres { get; set; }

        /// <summary>
        /// Gets or sets the cached media type (Movie, Series).
        /// </summary>
        [JsonPropertyName("cachedMediaType")]
        public string? CachedMediaType { get; set; }

        /// <summary>
        /// Gets or sets when the metadata was cached.
        /// </summary>
        [JsonPropertyName("cachedAt")]
        public DateTime? CachedAt { get; set; }

        /// <summary>
        /// Gets or sets an optional note/mini-review for this item.
        /// </summary>
        [JsonPropertyName("note")]
        public string? Note { get; set; }

        /// <summary>
        /// Gets or sets the position/order in the list.
        /// </summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        /// <summary>
        /// Gets or sets when this item was added to the list.
        /// </summary>
        [JsonPropertyName("addedAt")]
        public DateTime AddedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserMediaListItem"/> class.
        /// </summary>
        public UserMediaListItem()
        {
            Id = Guid.NewGuid();
            AddedAt = DateTime.UtcNow;
        }
    }
}
