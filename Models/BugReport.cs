using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// One screenshot attached to a bug report.
    /// </summary>
    /// <remarks>
    /// The file itself lives on disk under the plugin's data directory, named after
    /// <see cref="Id"/> - never after anything the uploader supplied. <see cref="FileName"/> is
    /// kept only to show the user what they attached and is never used to build a path.
    /// </remarks>
    public class BugReportAttachment
    {
        /// <summary>
        /// Gets or sets the attachment id, which is also its filename on disk.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the name the uploader's browser reported, for display only.
        /// </summary>
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image type, as determined by inspecting the bytes rather than by
        /// trusting the upload's declared content type.
        /// </summary>
        [JsonPropertyName("contentType")]
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file extension used on disk.
        /// </summary>
        [JsonPropertyName("extension")]
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size in bytes.
        /// </summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }
    }

    /// <summary>
    /// A problem report raised by a user, optionally with screenshots.
    /// </summary>
    public class BugReport
    {
        /// <summary>
        /// Gets or sets the unique identifier for this report.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the user ID who raised the report.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username who raised the report.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the problem.
        /// </summary>
        [JsonPropertyName("comment")]
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets where the user was and what they were using, captured by the client so an
        /// admin does not have to ask. Truncated, and never trusted for anything but display.
        /// </summary>
        [JsonPropertyName("context")]
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// Gets the attached screenshots.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<BugReportAttachment> Attachments { get; init; } = new();

        /// <summary>
        /// Gets or sets the status: open, reviewing, solved or rejected.
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "open";

        /// <summary>
        /// Gets or sets the administrator's reply, shown back to the reporter.
        /// </summary>
        [JsonPropertyName("adminResponse")]
        public string AdminResponse { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when the report was raised.
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the report was last touched by anyone.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the report was closed, if it has been.
        /// </summary>
        [JsonPropertyName("resolvedAt")]
        public DateTime? ResolvedAt { get; set; }

        /// <summary>
        /// Gets or sets the name of whoever closed it.
        /// </summary>
        [JsonPropertyName("resolvedBy")]
        public string ResolvedBy { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether this report is still awaiting an administrator.
        /// </summary>
        [JsonIgnore]
        public bool IsOpen => Status == "open" || Status == "reviewing";
    }
}
