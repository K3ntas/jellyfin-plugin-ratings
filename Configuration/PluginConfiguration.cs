using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Ratings.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            EnableRatings = true;
            MaxRating = 10;
            MinRating = 1;
            AllowGuestRatings = false;
            EnableNetflixView = false;
            EnableRequestButton = true;
            EnableNewMediaNotifications = true;
            EnableEpisodeGrouping = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether ratings are enabled.
        /// </summary>
        public bool EnableRatings { get; set; }

        /// <summary>
        /// Gets or sets the maximum rating value.
        /// </summary>
        public int MaxRating { get; set; }

        /// <summary>
        /// Gets or sets the minimum rating value.
        /// </summary>
        public int MinRating { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether guest users can rate items.
        /// </summary>
        public bool AllowGuestRatings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether Netflix-style genre view is enabled.
        /// </summary>
        public bool EnableNetflixView { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Request Media button is shown.
        /// </summary>
        public bool EnableRequestButton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether new media notifications are enabled.
        /// </summary>
        public bool EnableNewMediaNotifications { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether episode notifications should be grouped.
        /// When enabled, multiple episodes added at once show as "Episodes 4-8" instead of individual notifications.
        /// </summary>
        public bool EnableEpisodeGrouping { get; set; }
    }
}
