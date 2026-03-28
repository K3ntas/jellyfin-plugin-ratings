using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user's request to keep a media item scheduled for deletion.
    /// </summary>
    public class KeepRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this keep request.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the item ID of the media scheduled for deletion.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the user ID who made the request.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username who made the request.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the timestamp when the request was made.
        /// </summary>
        public DateTime RequestedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeepRequest"/> class.
        /// </summary>
        public KeepRequest()
        {
            Id = Guid.NewGuid();
            RequestedAt = DateTime.UtcNow;
        }
    }
}
