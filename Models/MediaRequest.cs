using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a media request from a user.
    /// </summary>
    public class MediaRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this request.
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
        /// Gets or sets the title of the requested media.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type of media (e.g., Movie, TV Series, Anime).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets additional notes or details about the request.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the request (pending, processing, done).
        /// </summary>
        public string Status { get; set; } = "pending";

        /// <summary>
        /// Gets or sets the timestamp when the request was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the request was completed (marked as done).
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets or sets the media link (URL to the media in Jellyfin) when request is fulfilled.
        /// </summary>
        public string MediaLink { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rejection reason when request is rejected.
        /// </summary>
        public string RejectionReason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets custom fields as JSON string (key-value pairs).
        /// </summary>
        public string CustomFields { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the IMDB code (e.g., tt0448134).
        /// </summary>
        public string ImdbCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full IMDB link.
        /// </summary>
        public string ImdbLink { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date until which this request is snoozed.
        /// Null means the request is not snoozed.
        /// </summary>
        public DateTime? SnoozedUntil { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaRequest"/> class.
        /// </summary>
        public MediaRequest()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
