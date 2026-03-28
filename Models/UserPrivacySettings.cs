using System;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Privacy settings for a user profile.
    /// </summary>
    public class UserPrivacySettings
    {
        /// <summary>
        /// Gets or sets profile visibility. Values: Public, Friends, Private.
        /// </summary>
        public string ProfileVisibility { get; set; } = "Public";

        /// <summary>
        /// Gets or sets who can see online status. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string ShowOnlineStatus { get; set; } = "Friends";

        /// <summary>
        /// Gets or sets who can see watched history. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string ShowWatchedHistory { get; set; } = "Friends";

        /// <summary>
        /// Gets or sets who can see friends list. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string ShowFriendsList { get; set; } = "Friends";

        /// <summary>
        /// Gets or sets who can see currently watching. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string ShowCurrentlyWatching { get; set; } = "Friends";

        /// <summary>
        /// Gets or sets who can send friend requests. Values: Everyone, Nobody.
        /// </summary>
        public string AllowFriendRequests { get; set; } = "Everyone";

        /// <summary>
        /// Gets or sets who can send messages. Values: Everyone, Friends, Nobody.
        /// </summary>
        public string AllowMessages { get; set; } = "Friends";

        /// <summary>
        /// Initializes a new instance of the <see cref="UserPrivacySettings"/> class with secure defaults.
        /// </summary>
        public UserPrivacySettings()
        {
        }
    }
}
