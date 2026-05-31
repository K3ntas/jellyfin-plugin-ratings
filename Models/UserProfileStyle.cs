using System;

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
        public Guid UserId { get; set; }

        // ============ BACKGROUND ============

        /// <summary>
        /// Gets or sets the background type (solid, gradient, image).
        /// </summary>
        public string BackgroundType { get; set; } = "solid";

        /// <summary>
        /// Gets or sets the solid background color (hex).
        /// </summary>
        public string BackgroundColor { get; set; } = "#1a1a2e";

        /// <summary>
        /// Gets or sets the gradient CSS value.
        /// </summary>
        public string? BackgroundGradient { get; set; }

        /// <summary>
        /// Gets or sets the background image URL.
        /// </summary>
        public string? BackgroundImageUrl { get; set; }

        /// <summary>
        /// Gets or sets the background blur amount (0-20px).
        /// </summary>
        public int BackgroundBlur { get; set; }

        /// <summary>
        /// Gets or sets the background overlay opacity (0-100).
        /// </summary>
        public int BackgroundOverlayOpacity { get; set; } = 50;

        // ============ THEME ============

        /// <summary>
        /// Gets or sets the theme (light, dark, custom).
        /// </summary>
        public string Theme { get; set; } = "dark";

        /// <summary>
        /// Gets or sets the primary accent color (hex).
        /// </summary>
        public string AccentColor { get; set; } = "#00d4ff";

        // ============ FONTS ============

        /// <summary>
        /// Gets or sets the font family.
        /// </summary>
        public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";

        /// <summary>
        /// Gets or sets the username color (hex).
        /// </summary>
        public string UsernameColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the bio text color (hex).
        /// </summary>
        public string BioColor { get; set; } = "#a0a0a0";

        /// <summary>
        /// Gets or sets the stats number color (hex).
        /// </summary>
        public string StatsNumberColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the stats label color (hex).
        /// </summary>
        public string StatsLabelColor { get; set; } = "#808080";

        /// <summary>
        /// Gets or sets the active tab color (hex).
        /// </summary>
        public string TabActiveColor { get; set; } = "#00d4ff";

        /// <summary>
        /// Gets or sets the inactive tab color (hex).
        /// </summary>
        public string TabInactiveColor { get; set; } = "#808080";

        /// <summary>
        /// Gets or sets the section header color (hex).
        /// </summary>
        public string SectionHeaderColor { get; set; } = "#a0a0a0";

        // ============ CARDS ============

        /// <summary>
        /// Gets or sets the card background color (hex).
        /// </summary>
        public string CardBackgroundColor { get; set; } = "#2a2a3e";

        /// <summary>
        /// Gets or sets the card border color (hex).
        /// </summary>
        public string CardBorderColor { get; set; } = "#3a3a4e";

        /// <summary>
        /// Gets or sets the card text color (hex).
        /// </summary>
        public string CardTextColor { get; set; } = "#ffffff";

        /// <summary>
        /// Gets or sets the card border radius (0-20px).
        /// </summary>
        public int CardBorderRadius { get; set; } = 8;

        /// <summary>
        /// Gets or sets the card shadow (CSS value or preset).
        /// </summary>
        public string CardShadow { get; set; } = "0 2px 8px rgba(0,0,0,0.3)";

        /// <summary>
        /// Gets or sets the review text color (hex).
        /// </summary>
        public string ReviewTextColor { get; set; } = "#d0d0d0";

        // ============ POSTERS ============

        /// <summary>
        /// Gets or sets the poster border color (hex).
        /// </summary>
        public string PosterBorderColor { get; set; } = "#3a3a4e";

        /// <summary>
        /// Gets or sets the poster shadow (CSS value).
        /// </summary>
        public string PosterShadow { get; set; } = "0 4px 12px rgba(0,0,0,0.4)";

        /// <summary>
        /// Gets or sets the poster hover effect (scale, glow, border, none).
        /// </summary>
        public string PosterHoverEffect { get; set; } = "scale";

        // ============ OTHER ============

        /// <summary>
        /// Gets or sets the rating stars color (hex).
        /// </summary>
        public string RatingStarsColor { get; set; } = "#ffd700";

        /// <summary>
        /// Gets or sets the link color (hex).
        /// </summary>
        public string LinkColor { get; set; } = "#00d4ff";

        /// <summary>
        /// Gets or sets the like button color (hex).
        /// </summary>
        public string LikeColor { get; set; } = "#ff6b6b";

        /// <summary>
        /// Gets or sets when the style was last updated.
        /// </summary>
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
