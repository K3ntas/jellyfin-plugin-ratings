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
            ShowLanguageSwitch = true;
            ShowSearchButton = true;
            CustomRequestFields = string.Empty;
            RequestWindowTitle = string.Empty;
            RequestWindowDescription = string.Empty;
            RequestTitleLabel = string.Empty;
            RequestTitlePlaceholder = string.Empty;
            RequestTypeLabel = string.Empty;
            RequestNotesLabel = string.Empty;
            RequestNotesPlaceholder = string.Empty;
            RequestSubmitButtonText = string.Empty;
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

        /// <summary>
        /// Gets or sets a value indicating whether the language switch (EN/LT) is shown.
        /// </summary>
        public bool ShowLanguageSwitch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the search button is shown in the header.
        /// </summary>
        public bool ShowSearchButton { get; set; }

        /// <summary>
        /// Gets or sets the custom fields for request form as JSON.
        /// Format: [{"name": "IMDB Link", "placeholder": "https://imdb.com/...", "required": true}]
        /// </summary>
        public string CustomRequestFields { get; set; }

        /// <summary>
        /// Gets or sets the custom title for the request window. Empty = use default.
        /// </summary>
        public string RequestWindowTitle { get; set; }

        /// <summary>
        /// Gets or sets the custom description for the request window. Empty = use default.
        /// </summary>
        public string RequestWindowDescription { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the media title field. Empty = use default.
        /// </summary>
        public string RequestTitleLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the media title field. Empty = use default.
        /// </summary>
        public string RequestTitlePlaceholder { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the type field. Empty = use default.
        /// </summary>
        public string RequestTypeLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the notes field. Empty = use default.
        /// </summary>
        public string RequestNotesLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the notes field. Empty = use default.
        /// </summary>
        public string RequestNotesPlaceholder { get; set; }

        /// <summary>
        /// Gets or sets the custom text for the submit button. Empty = use default.
        /// </summary>
        public string RequestSubmitButtonText { get; set; }
    }
}
