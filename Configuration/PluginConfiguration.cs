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
            // Rating settings
            EnableRatings = true;
            MaxRating = 10;
            MinRating = 1;

            // Feature toggles
            EnableNetflixView = false;
            EnableRequestButton = true;
            EnableNewMediaNotifications = true;
            EnableEpisodeGrouping = true;
            ShowLanguageSwitch = true;
            ShowSearchButton = true;
            ShowNotificationToggle = true;
            ShowLatestMediaButton = true;

            // Media management settings
            EnableMediaManagement = true;
            DefaultDeletionDelayDays = 7;

            // Request system settings
            EnableAdminRequests = false;
            AutoDeleteRejectedDays = 0;
            MaxRequestsPerMonth = 0;

            // Custom fields for request form
            CustomRequestFields = string.Empty;

            // Request window customization
            RequestWindowTitle = string.Empty;
            RequestWindowDescription = string.Empty;
            RequestSubmitButtonText = string.Empty;

            // Title field (always shown, always required)
            RequestTitleLabel = string.Empty;
            RequestTitlePlaceholder = string.Empty;

            // Type field settings
            RequestTypeEnabled = true;
            RequestTypeRequired = false;
            RequestTypeLabel = string.Empty;

            // Notes field settings
            RequestNotesEnabled = true;
            RequestNotesRequired = false;
            RequestNotesLabel = string.Empty;
            RequestNotesPlaceholder = string.Empty;

            // IMDB Code field settings
            RequestImdbCodeEnabled = true;
            RequestImdbCodeRequired = false;
            RequestImdbCodeLabel = string.Empty;
            RequestImdbCodePlaceholder = string.Empty;

            // IMDB Link field settings
            RequestImdbLinkEnabled = true;
            RequestImdbLinkRequired = false;
            RequestImdbLinkLabel = string.Empty;
            RequestImdbLinkPlaceholder = string.Empty;

            // Badge display profiles (JSON array of resolution-based profiles)
            BadgeDisplayProfiles = string.Empty;

            // Language settings
            DefaultLanguage = "en";

            // Chat settings
            EnableChat = false;
            ChatMessageRetentionDays = 7;
            ChatRateLimitPerMinute = 10;
            ChatMaxMessageLength = 500;
            ChatAllowGifs = true;
            ChatAllowEmojis = true;
            TenorApiKey = string.Empty;
            KlipyApiKey = string.Empty;

            // Chat notification settings
            ChatNotifyPublic = true;
            ChatNotifyPrivate = true;
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
        /// Gets or sets a value indicating whether the notification toggle is shown in the header.
        /// </summary>
        public bool ShowNotificationToggle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Latest Media button is shown in the header.
        /// Replaces the Sync Play button with a dropdown showing 50 most recently added media items.
        /// </summary>
        public bool ShowLatestMediaButton { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Media Management feature is enabled for admins.
        /// </summary>
        public bool EnableMediaManagement { get; set; }

        /// <summary>
        /// Gets or sets the default delay in days for scheduled deletions.
        /// </summary>
        public int DefaultDeletionDelayDays { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether admins can create requests like normal users.
        /// </summary>
        public bool EnableAdminRequests { get; set; }

        /// <summary>
        /// Gets or sets the number of days after which rejected requests are automatically deleted.
        /// Set to 0 to disable auto-deletion.
        /// </summary>
        public int AutoDeleteRejectedDays { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of requests a user can make per month.
        /// Set to 0 for unlimited requests.
        /// </summary>
        public int MaxRequestsPerMonth { get; set; }

        /// <summary>
        /// Gets or sets the custom fields for request form as JSON.
        /// Format: [{"name": "Field Name", "placeholder": "placeholder text", "required": true}]
        /// </summary>
        public string CustomRequestFields { get; set; }

        /// <summary>
        /// Gets or sets the custom title for the request window. Empty = use default.
        /// </summary>
        public string RequestWindowTitle { get; set; }

        /// <summary>
        /// Gets or sets the custom description for the request window. Empty = hide description.
        /// </summary>
        public string RequestWindowDescription { get; set; }

        /// <summary>
        /// Gets or sets the custom text for the submit button. Empty = use default.
        /// </summary>
        public string RequestSubmitButtonText { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the media title field. Empty = use default.
        /// </summary>
        public string RequestTitleLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the media title field. Empty = use default.
        /// </summary>
        public string RequestTitlePlaceholder { get; set; }

        // Type field settings

        /// <summary>
        /// Gets or sets a value indicating whether the Type field is shown.
        /// </summary>
        public bool RequestTypeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Type field is required.
        /// </summary>
        public bool RequestTypeRequired { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the type field. Empty = use default.
        /// </summary>
        public string RequestTypeLabel { get; set; }

        // Notes field settings

        /// <summary>
        /// Gets or sets a value indicating whether the Notes field is shown.
        /// </summary>
        public bool RequestNotesEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the Notes field is required.
        /// </summary>
        public bool RequestNotesRequired { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the notes field. Empty = use default.
        /// </summary>
        public string RequestNotesLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the notes field. Empty = use default.
        /// </summary>
        public string RequestNotesPlaceholder { get; set; }

        // IMDB Code field settings

        /// <summary>
        /// Gets or sets a value indicating whether the IMDB Code field is shown.
        /// </summary>
        public bool RequestImdbCodeEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the IMDB Code field is required.
        /// </summary>
        public bool RequestImdbCodeRequired { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the IMDB Code field. Empty = use default.
        /// </summary>
        public string RequestImdbCodeLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the IMDB Code field. Empty = use default.
        /// </summary>
        public string RequestImdbCodePlaceholder { get; set; }

        // IMDB Link field settings

        /// <summary>
        /// Gets or sets a value indicating whether the IMDB Link field is shown.
        /// </summary>
        public bool RequestImdbLinkEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the IMDB Link field is required.
        /// </summary>
        public bool RequestImdbLinkRequired { get; set; }

        /// <summary>
        /// Gets or sets the custom label for the IMDB Link field. Empty = use default.
        /// </summary>
        public string RequestImdbLinkLabel { get; set; }

        /// <summary>
        /// Gets or sets the custom placeholder for the IMDB Link field. Empty = use default.
        /// </summary>
        public string RequestImdbLinkPlaceholder { get; set; }

        /// <summary>
        /// Gets or sets the badge display profiles as JSON.
        /// Each profile defines display settings for a specific resolution range.
        /// Format: [{"minWidth":0,"maxWidth":1920,"offsetX":0,"offsetY":0,"hideText":false,"sizePercent":0,"removeBackground":false}]
        /// </summary>
        public string BadgeDisplayProfiles { get; set; }

        /// <summary>
        /// Gets or sets the default language for the plugin UI.
        /// Supported: en, es, zh, pt, ru, ja, de, fr, ko, it, tr, pl, nl, ar, hi, lt
        /// </summary>
        public string DefaultLanguage { get; set; }

        // Chat settings

        /// <summary>
        /// Gets or sets a value indicating whether the live chat feature is enabled.
        /// </summary>
        public bool EnableChat { get; set; }

        /// <summary>
        /// Gets or sets the number of days to retain chat messages.
        /// </summary>
        public int ChatMessageRetentionDays { get; set; }

        /// <summary>
        /// Gets or sets the maximum messages per minute per user (rate limiting).
        /// </summary>
        public int ChatRateLimitPerMinute { get; set; }

        /// <summary>
        /// Gets or sets the maximum message length in characters.
        /// </summary>
        public int ChatMaxMessageLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether GIFs are allowed in chat.
        /// </summary>
        public bool ChatAllowGifs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether emojis are allowed in chat.
        /// </summary>
        public bool ChatAllowEmojis { get; set; }

        /// <summary>
        /// Gets or sets the Tenor API key for GIF search (deprecated, use KlipyApiKey).
        /// </summary>
        public string TenorApiKey { get; set; }

        /// <summary>
        /// Gets or sets the Klipy API key for GIF search.
        /// Get a free key at https://klipy.com
        /// </summary>
        public string KlipyApiKey { get; set; }

        /// <summary>
        /// Gets or sets the last backup date (ISO 8601 format).
        /// Used to show backup reminder warning.
        /// </summary>
        public string LastBackupDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to show notifications for public chat messages.
        /// </summary>
        public bool ChatNotifyPublic { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to show notifications for private messages.
        /// </summary>
        public bool ChatNotifyPrivate { get; set; }
    }
}
