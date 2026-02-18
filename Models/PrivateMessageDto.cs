namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// DTO for sending a private message.
    /// </summary>
    public class PrivateMessageDto
    {
        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Gets or sets the GIF URL.
        /// </summary>
        public string? GifUrl { get; set; }
    }
}
