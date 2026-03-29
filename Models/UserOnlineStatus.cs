using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents a user's online status.
    /// </summary>
    public class UserOnlineStatus
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the current status.
        /// Values: Online, Away, DoNotDisturb, Invisible, Offline.
        /// </summary>
        public string Status { get; set; } = "Offline";

        /// <summary>
        /// Gets or sets the manually set status (user override).
        /// Null means automatic status based on heartbeat.
        /// </summary>
        public string? ManualStatus { get; set; }

        /// <summary>
        /// Gets or sets when the user was last seen active.
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Gets or sets when the last heartbeat was received.
        /// </summary>
        public DateTime LastHeartbeat { get; set; }

        /// <summary>
        /// Gets or sets what the user is currently watching.
        /// </summary>
        public CurrentlyWatching? Watching { get; set; }

        /// <summary>
        /// Gets or sets whether the user explicitly went offline (browser closed, logout).
        /// This flag takes priority over heartbeat-based status.
        /// </summary>
        public bool ForceOffline { get; set; }

        /// <summary>
        /// Calculates the effective status based on heartbeat and manual override.
        /// </summary>
        /// <returns>The effective status string.</returns>
        public string GetEffectiveStatus()
        {
            // Force offline takes highest priority (browser closed, logout)
            if (ForceOffline)
            {
                return "Offline";
            }

            // Manual status overrides automatic
            if (!string.IsNullOrEmpty(ManualStatus))
            {
                // Invisible appears as Offline to others
                return ManualStatus;
            }

            var secondsSinceHeartbeat = (DateTime.UtcNow - LastHeartbeat).TotalSeconds;

            // Online: heartbeat within 60 seconds
            if (secondsSinceHeartbeat < 60)
            {
                return "Online";
            }

            // Away: heartbeat 1-5 minutes ago
            if (secondsSinceHeartbeat < 300)
            {
                return "Away";
            }

            // Offline: no heartbeat for 5+ minutes
            return "Offline";
        }
    }

    /// <summary>
    /// Represents what a user is currently watching.
    /// </summary>
    public class CurrentlyWatching
    {
        /// <summary>
        /// Gets or sets the media item ID.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the media title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media type (Movie, Episode, etc.).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the series name (for episodes).
        /// </summary>
        public string? SeriesName { get; set; }

        /// <summary>
        /// Gets or sets the season and episode info (e.g., "S01E05").
        /// </summary>
        public string? EpisodeInfo { get; set; }

        /// <summary>
        /// Gets or sets the current position in ticks.
        /// </summary>
        public long PositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the total duration in ticks.
        /// </summary>
        public long DurationTicks { get; set; }

        /// <summary>
        /// Gets or sets when playback started.
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// Gets the formatted position string (e.g., "45:30").
        /// </summary>
        public string FormattedPosition => FormatTicks(PositionTicks);

        /// <summary>
        /// Gets the formatted duration string (e.g., "1:30:00").
        /// </summary>
        public string FormattedDuration => FormatTicks(DurationTicks);

        /// <summary>
        /// Gets the progress percentage (0-100).
        /// </summary>
        public int ProgressPercent => DurationTicks > 0 ? (int)((PositionTicks * 100) / DurationTicks) : 0;

        private static string FormatTicks(long ticks)
        {
            var span = TimeSpan.FromTicks(ticks);
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}";
            }

            return $"{span.Minutes}:{span.Seconds:D2}";
        }
    }

    /// <summary>
    /// Request model for heartbeat endpoint with optional watching info.
    /// </summary>
    public class HeartbeatRequest
    {
        /// <summary>
        /// Gets or sets the watching info sent from client for instant updates.
        /// </summary>
        public WatchingInfo? Watching { get; set; }

        /// <summary>
        /// Gets or sets whether playback was explicitly stopped.
        /// When true, watching status will be cleared immediately.
        /// </summary>
        public bool Stopped { get; set; }
    }

    /// <summary>
    /// Watching info sent from client (simpler than CurrentlyWatching).
    /// </summary>
    public class WatchingInfo
    {
        /// <summary>
        /// Gets or sets the media item ID.
        /// </summary>
        public Guid ItemId { get; set; }

        /// <summary>
        /// Gets or sets the media title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the media type.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Gets or sets the series name (for episodes).
        /// </summary>
        public string? SeriesName { get; set; }

        /// <summary>
        /// Gets or sets the season/episode info.
        /// </summary>
        public string? EpisodeInfo { get; set; }

        /// <summary>
        /// Gets or sets the current position in ticks.
        /// </summary>
        public long PositionTicks { get; set; }

        /// <summary>
        /// Gets or sets the total duration in ticks.
        /// </summary>
        public long DurationTicks { get; set; }
    }
}
