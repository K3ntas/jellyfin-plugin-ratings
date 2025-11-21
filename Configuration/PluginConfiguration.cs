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
    }
}
