using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// A request to improve the quality of something already in the library.
    /// </summary>
    /// <remarks>
    /// Deliberately kept apart from <see cref="MediaRequest"/>. A media request asks for a title
    /// the server does not have and finishes when it arrives; this one points at an item that is
    /// already there and finishes when the file behind it is replaced, so the two carry different
    /// fields and resolve through different work.
    /// </remarks>
    public class QualityRequest
    {
        /// <summary>
        /// Gets or sets the unique identifier for this request.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who raised the request.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username who raised the request.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the library item the request is about.
        /// </summary>
        [JsonPropertyName("itemId")]
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item's name, stored so the request still reads sensibly after the
        /// item is replaced or removed from the library.
        /// </summary>
        [JsonPropertyName("itemName")]
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item's production year, if known.
        /// </summary>
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        /// <summary>
        /// Gets or sets the item's type (Movie, Series, and so on).
        /// </summary>
        [JsonPropertyName("itemType")]
        public string ItemType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets what is wrong with the current copy, in the user's words.
        /// </summary>
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolution of the current file, captured when the request is raised so
        /// an admin can see what is being complained about without opening the item.
        /// </summary>
        [JsonPropertyName("currentQuality")]
        public string CurrentQuality { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status: open, reviewing, solved or rejected.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "open";

        /// <summary>
        /// Gets or sets the administrator's reply, shown back to the user who raised it.
        /// </summary>
        [JsonPropertyName("adminResponse")]
        public string AdminResponse { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the request was raised.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the request was last touched by anyone.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the request was closed, if it has been.
        /// </summary>
        [JsonPropertyName("resolvedAt")]
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Gets or sets the name of whoever closed it.
        /// </summary>
        [JsonPropertyName("resolvedBy")]
        public string ResolvedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this request is still awaiting an administrator.
        /// </summary>
        [JsonIgnore]
        public bool IsOpen => Status == "open" || Status == "reviewing";
    }
}
