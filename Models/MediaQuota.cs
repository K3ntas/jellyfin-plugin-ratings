using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents media watching quota limits for a user.
    /// </summary>
    public class MediaQuota
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the daily playback limit (0 = unlimited).
        /// </summary>
        public int DailyLimit { get; set; }

        /// <summary>
        /// Gets or sets the weekly playback limit (0 = unlimited).
        /// </summary>
        public int WeeklyLimit { get; set; }

        /// <summary>
        /// Gets or sets the monthly playback limit (0 = unlimited).
        /// </summary>
        public int MonthlyLimit { get; set; }

        /// <summary>
        /// Gets or sets the daily usage count.
        /// </summary>
        public int DailyUsed { get; set; }

        /// <summary>
        /// Gets or sets the weekly usage count.
        /// </summary>
        public int WeeklyUsed { get; set; }

        /// <summary>
        /// Gets or sets the monthly usage count.
        /// </summary>
        public int MonthlyUsed { get; set; }

        /// <summary>
        /// Gets or sets when the daily counter resets.
        /// </summary>
        public DateTime DailyReset { get; set; }

        /// <summary>
        /// Gets or sets when the weekly counter resets.
        /// </summary>
        public DateTime WeeklyReset { get; set; }

        /// <summary>
        /// Gets or sets when the monthly counter resets.
        /// </summary>
        public DateTime MonthlyReset { get; set; }

        /// <summary>
        /// Gets or sets who set this quota.
        /// </summary>
        public Guid SetBy { get; set; }

        /// <summary>
        /// Gets or sets the name of who set this quota.
        /// </summary>
        public string SetByName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when this quota was set.
        /// </summary>
        public DateTime SetAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Checks if any quota limit is exceeded.
        /// </summary>
        public bool IsQuotaExceeded()
        {
            var now = DateTime.UtcNow;

            // Reset expired counters
            if (now >= DailyReset)
            {
                DailyUsed = 0;
                DailyReset = now.Date.AddDays(1);
            }

            if (now >= WeeklyReset)
            {
                WeeklyUsed = 0;
                // Reset to next Monday
                var daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilMonday == 0) daysUntilMonday = 7;
                WeeklyReset = now.Date.AddDays(daysUntilMonday);
            }

            if (now >= MonthlyReset)
            {
                MonthlyUsed = 0;
                MonthlyReset = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            }

            // Check limits (0 = unlimited)
            if (DailyLimit > 0 && DailyUsed >= DailyLimit) return true;
            if (WeeklyLimit > 0 && WeeklyUsed >= WeeklyLimit) return true;
            if (MonthlyLimit > 0 && MonthlyUsed >= MonthlyLimit) return true;

            return false;
        }

        /// <summary>
        /// Increments usage counters.
        /// </summary>
        public void IncrementUsage()
        {
            DailyUsed++;
            WeeklyUsed++;
            MonthlyUsed++;
        }
    }
}
