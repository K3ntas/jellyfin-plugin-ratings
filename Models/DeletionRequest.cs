using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user's request to delete media.
    /// </summary>
    public class DeletionRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this deletion request.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who made the request.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username who made the request.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the original media request ID this deletion request is linked to.
        /// </summary>
        public Guid MediaRequestId { get; set; }

        /// <summary>
        /// Gets or sets the Jellyfin item ID of the media to be deleted.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the title of the media.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of media (e.g., Movie, TV Series).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media link (URL to the media in Jellyfin).
        /// </summary>
        public string MediaLink { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the deletion type: "request" (delete the request) or "media" (delete the media from library).
        /// </summary>
        public string DeletionType { get; set; } = "media";

        /// <summary>
        /// Gets or sets the status of the deletion request (pending, approved, rejected).
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Gets or sets the timestamp when the request was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the request was resolved.
        /// </summary>
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Gets or sets the username of the admin who resolved the request.
        /// </summary>
        public string ResolvedByUsername { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeletionRequest"/> class.
        /// </summary>
        public DeletionRequest()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
