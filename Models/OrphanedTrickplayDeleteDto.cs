using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Which orphaned trickplay folders to remove.
    /// </summary>
    public class OrphanedTrickplayDeleteDto
    {
        /// <summary>
        /// Gets or sets the item ids whose folders should be removed. Empty or absent means every
        /// folder the server identifies as orphaned. Ids only - never paths, so a request cannot
        /// aim the delete at somewhere else on disk.
        /// </summary>
        [JsonPropertyName("itemIds")]
        public List<string>? ItemIds { get; set; }
    }
}
