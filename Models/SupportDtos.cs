using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Payload for raising a quality request against a library item.
    /// </summary>
    public class QualityRequestDto
    {
        /// <summary>
        /// Gets or sets the library item the request is about.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets what is wrong with the current copy.
        /// </summary>
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current quality as the client already knows it (for example
        /// "1080p H264"). Supplied by the client because it has the media info on screen; treated
        /// as a display string only.
        /// </summary>
        [JsonPropertyName("currentQuality")]
        public string CurrentQuality { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payload for an administrator changing the state of a quality request or bug report.
    /// </summary>
    public class SupportStatusDto
    {
        /// <summary>
        /// Gets or sets the new status: open, reviewing, solved or rejected.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reply shown back to whoever raised it. Null leaves the existing reply
        /// alone; an empty string clears it.
        /// </summary>
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
