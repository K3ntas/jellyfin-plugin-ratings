using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Data transfer object for creating deletion requests.
    /// </summary>
    public class DeletionRequestDto
    {
        /// <summary>
        /// Gets or sets the original media request ID.
        /// </summary>
        public Guid MediaRequestId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin item ID of the media.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the title of the media.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of media.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media link.
        /// </summary>
        public string MediaLink { get; set; } = string.Empty;
    }
}
