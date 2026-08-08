using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Ratings.Models
{
    /// <summary>
    /// Represents customization settings for a user's profile appearance.
    /// </summary>
    public class UserProfileStyle
    {
        /// <summary>
        /// Gets or sets the user ID this style belongs to.
        /// </summary>
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        // ============ BACKGROUND ============

        /// <summary>
        /// Gets or sets the background type (solid, gradient, image).
        /// </summary>
        [JsonPropertyName("backgroundType")]
        public string BackgroundType { get; set; } = "solid";

        /// <summary>
        /// Gets or sets the solid background color (hex).
        /// </summary>
        [JsonPropertyName("backgroundColor")]
        public string BackgroundColor { get; set; } = "#1a1a2e";

        /// <summary>
        /// Gets or sets the gradient CSS value.
        /// </summary>
        [JsonPropertyName("backgroundGradient")]
        public string? BackgroundGradient { get; set; }

        /// <summary>
        /// Gets or sets the background image URL.
        /// </summary>
        [JsonPropertyName("backgroundImageUrl")]
        public string? BackgroundImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the background blur amount (0-20px).
        /// </summary>
        [JsonPropertyName("backgroundBlur")]
        public int BackgroundBlur { get; set; }

        /// <summary>
        /// Gets or sets the background overlay opacity (0-100).
        /// </summary>
        [JsonPropertyName("backgroundOverlayOpacity")]
        public int BackgroundOverlayOpacity { get; set; } = 50;

        // ============ THEME ============

        /// <summary>
        /// Gets or sets the theme (light, dark, custom).
        /// </summary>
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// Gets or sets the primary accent color (hex).
        /// </summary>
        [JsonPropertyName("accentColor")]
        public string AccentColor { get; set; } = "#00d4ff";

        // ============ FONTS ============

        /// <summary>
        /// Gets or sets the font family.
        /// </summary>
        [JsonPropertyName("fontFamily")]
        public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";

        /// <summary>
        /// Gets or sets the username color (hex).
        /// </summary>
        [JsonPropertyName("usernameColor")]
        public string UsernameColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the bio text color (hex).
        /// </summary>
        [JsonPropertyName("bioColor")]
        public string BioColor { get; set; } = "#a0a0a0";

        /// <summary>
        /// Gets or sets the stats number color (hex).
        /// </summary>
        [JsonPropertyName("statsNumberColor")]
        public string StatsNumberColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the stats label color (hex).
        /// </summary>
        [JsonPropertyName("statsLabelColor")]
        public string StatsLabelColor { get; set; } = "#808080";

        /// <summary>
        /// Gets or sets the active tab color (hex).
        /// </summary>
        [JsonPropertyName("tabActiveColor")]
        public string TabActiveColor { get; set; } = "#00d4ff";

        /// <summary>
        /// Gets or sets the inactive tab color (hex).
        /// </summary>
        [JsonPropertyName("tabInactiveColor")]
        public string TabInactiveColor { get; set; } = "#808080";

        /// <summary>
        /// Gets or sets the section header color (hex).
        /// </summary>
        [JsonPropertyName("sectionHeaderColor")]
        public string SectionHeaderColor { get; set; } = "#a0a0a0";

        // ============ CARDS ============

        /// <summary>
        /// Gets or sets the card background color (hex).
        /// </summary>
        [JsonPropertyName("cardBackgroundColor")]
        public string CardBackgroundColor { get; set; } = "#2a2a3e";

        /// <summary>
        /// Gets or sets the card border color (hex).
        /// </summary>
        [JsonPropertyName("cardBorderColor")]
        public string CardBorderColor { get; set; } = "#3a3a4e";

        /// <summary>
        /// Gets or sets the card text color (hex).
        /// </summary>
        [JsonPropertyName("cardTextColor")]
        public string CardTextColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the card border radius (0-20px).
        /// </summary>
        [JsonPropertyName("cardBorderRadius")]
        public int CardBorderRadius { get; set; } = 8;

        /// <summary>
        /// Gets or sets the card shadow (CSS value or preset).
        /// </summary>
        [JsonPropertyName("cardShadow")]
        public string CardShadow { get; set; } = "0 2px 8px rgba(0,0,0,0.3)";

        /// <summary>
        /// Gets or sets the review text color (hex).
        /// </summary>
        [JsonPropertyName("reviewTextColor")]
        public string ReviewTextColor { get; set; } = "#d0d0d0";

        // ============ POSTERS ============

        /// <summary>
        /// Gets or sets the poster border color (hex).
        /// </summary>
        [JsonPropertyName("posterBorderColor")]
        public string PosterBorderColor { get; set; } = "#3a3a4e";

        /// <summary>
        /// Gets or sets the poster shadow (CSS value).
        /// </summary>
        [JsonPropertyName("posterShadow")]
        public string PosterShadow { get; set; } = "0 4px 12px rgba(0,0,0,0.4)";

        /// <summary>
        /// Gets or sets the poster hover effect (scale, glow, border, none).
        /// </summary>
        [JsonPropertyName("posterHoverEffect")]
        public string PosterHoverEffect { get; set; } = "scale";

        // ============ OTHER ============

        /// <summary>
        /// Gets or sets the rating stars color (hex).
        /// </summary>
        [JsonPropertyName("ratingStarsColor")]
        public string RatingStarsColor { get; set; } = "#ffd700";

        /// <summary>
        /// Gets or sets the link color (hex).
        /// </summary>
        [JsonPropertyName("linkColor")]
        public string LinkColor { get; set; } = "#00d4ff";

        /// <summary>
        /// Gets or sets the like button color (hex).
        /// </summary>
        [JsonPropertyName("likeColor")]
        public string LikeColor { get; set; } = "#ff6b6b";

        /// <summary>
        /// Gets or sets when the style was last updated.
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserProfileStyle"/> class.
        /// </summary>
        public UserProfileStyle()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
