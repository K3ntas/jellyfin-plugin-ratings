/**
 * Jellyfin Ratings Plugin - Client-side component
 */

(function () {
    'use strict';

    const RatingsPlugin = {
        pluginId: 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d',
        ratingsCache: {}, // Cache for card ratings to avoid duplicate API calls

        /**
         * Initialize the ratings plugin
         */
        init: function () {
            this.injectStyles();
            this.observeDetailPages();
            this.observeHomePageCards();

            // Initialize request button with multiple attempts for reliability
            this.initRequestButtonWithRetry();

            // Initialize search field in header
            this.initSearchField();

            // Initialize responsive scaling
            this.updateResponsiveScaling();

            // Initialize Netflix view if enabled
            this.initNetflixView();

            // Initialize new media notifications
            this.initNotifications();
        },

        /**
         * Initialize request button with retry logic for SPA navigation
         */
        initRequestButtonWithRetry: function () {
            const self = this;
            let attempts = 0;
            const maxAttempts = 10;

            const tryInit = () => {
                attempts++;
                try {
                    // Check if button already exists
                    if (document.getElementById('requestMediaBtn')) {
                        return; // Already initialized
                    }

                    // Check if ApiClient is ready
                    if (!window.ApiClient) {
                        if (attempts < maxAttempts) {
                            setTimeout(tryInit, 1000);
                        }
                        return;
                    }

                    // Check if request button is enabled in config
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.EnableRequestButton === true) {
                                self.initRequestButton();
                            }
                        })
                        .catch(() => {
                            // Default to showing button if config fails
                            self.initRequestButton();
                        });
                } catch (err) {
                    console.error('Request button init attempt failed:', err);
                    if (attempts < maxAttempts) {
                        setTimeout(tryInit, 1000);
                    }
                }
            };

            // Initial attempt after short delay
            setTimeout(tryInit, 1500);

            // Also try on page visibility change (when user returns to tab)
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible' && !document.getElementById('requestMediaBtn')) {
                    setTimeout(tryInit, 500);
                }
            });

            // Listen for Jellyfin navigation events if available
            try {
                if (window.Emby && window.Emby.Page && typeof Emby.Page.addEventListener === 'function') {
                    Emby.Page.addEventListener('pageshow', () => {
                        if (!document.getElementById('requestMediaBtn')) {
                            setTimeout(tryInit, 500);
                        }
                    });
                }
            } catch (e) {
                // Emby.Page.addEventListener not available in this version
            }
        },

        /**
         * Inject CSS styles for the rating component
         */
        injectStyles: function () {
            if (document.getElementById('ratingsPluginStyles')) {
                return;
            }

            const styles = `
                .ratings-plugin-container {
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    max-width: 800px;
                    text-align: center;
                    z-index: 9999;
                }

                /* Desktop styles */
                @media (min-width: 1313px) {
                    .ratings-plugin-container {
                        margin: -12em 40em 8em;
                        padding: 0em;
                    }

                    .ratings-plugin-star {
                        font-size: 2em;
                    }
                }

                /* Mobile styles */
                @media (max-width: 1312px) {
                    .ratings-plugin-container {
                        margin-left: 136px;
                        padding: 20px;
                    }

                    .ratings-plugin-star {
                        font-size: 1.5em;
                    }
                }

                .ratings-plugin-title {
                    font-size: 1.2em;
                    font-weight: 500;
                    margin-bottom: 0.5em;
                    color: #fff;
                }

                .ratings-plugin-stars {
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    gap: 0.3em;
                    margin-bottom: 0.5em;
                    position: relative;
                }

                .ratings-plugin-star {
                    cursor: pointer;
                    color: #555;
                    transition: all 0.2s ease;
                    user-select: none;
                }

                .ratings-plugin-star:hover,
                .ratings-plugin-star.hover {
                    color: #ffd700;
                    transform: scale(1.1);
                }

                .ratings-plugin-star.filled {
                    color: #ffd700;
                }

                .ratings-plugin-stats {
                    font-size: 0.9em;
                    color: #bbb;
                    margin-top: 0.5em;
                }

                .ratings-plugin-average {
                    font-size: 1.1em;
                    font-weight: 600;
                    color: #ffd700;
                    margin-left: 0.5em;
                }

                .ratings-plugin-popup {
                    position: absolute;
                    bottom: 100%;
                    left: 0;
                    background: rgba(20, 20, 20, 0.98);
                    border: 1px solid #444;
                    border-radius: 8px;
                    padding: 1em;
                    min-width: 250px;
                    max-width: 400px;
                    max-height: 400px;
                    overflow-y: auto;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
                    z-index: 10000;
                    margin-bottom: 0.5em;
                    display: none;
                }

                .ratings-plugin-popup.visible {
                    display: block;
                }

                .ratings-plugin-popup-title {
                    font-size: 1em;
                    font-weight: 600;
                    margin-bottom: 0.8em;
                    color: #fff;
                    border-bottom: 1px solid #444;
                    padding-bottom: 0.5em;
                }

                .ratings-plugin-popup-list {
                    list-style: none;
                    padding: 0;
                    margin: 0;
                }

                .ratings-plugin-popup-item {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 0.5em 0;
                    border-bottom: 1px solid #333;
                }

                .ratings-plugin-popup-item:last-child {
                    border-bottom: none;
                }

                .ratings-plugin-popup-username {
                    color: #fff;
                    font-weight: 500;
                    flex: 1;
                }

                .ratings-plugin-popup-rating {
                    color: #ffd700;
                    font-weight: 600;
                    font-size: 1.1em;
                    margin-left: 1em;
                }

                .ratings-plugin-popup-empty {
                    color: #999;
                    text-align: center;
                    padding: 1em;
                    font-style: italic;
                }

                .ratings-plugin-your-rating {
                    font-size: 0.85em;
                    color: #4CAF50;
                    margin-top: 0.3em;
                }

                .ratings-plugin-loading {
                    color: #999;
                    font-style: italic;
                }

                /* Card overlay ratings */
                .cardImageContainer.has-rating::after,
                .cardContent.has-rating::after,
                .card-imageContainer.has-rating::after {
                    content: attr(data-rating);
                    position: absolute;
                    top: 5px;
                    left: 5px;
                    background: rgba(0, 0, 0, 0.85);
                    color: #fff;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.85em;
                    z-index: 1000;
                    pointer-events: none;
                    font-weight: 600;
                }

                .ratings-plugin-card-star {
                    color: #ffd700;
                    font-size: 1em;
                }

                .ratings-plugin-card-rating {
                    color: #fff;
                    font-weight: 600;
                }

                /* Request Media Button - Aligned with Header */
                #requestMediaBtn {
                    position: absolute !important;
                    top: 8px;
                    right: 240px !important;
                    background: rgba(60, 60, 60, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    padding: 12px 48px !important;
                    border-radius: 25px !important;
                    font-size: 16px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    z-index: 999999 !important;
                    transition: transform 0.3s ease, background 0.3s ease, border-color 0.3s ease !important;
                    font-family: "Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                    -webkit-animation: pulseButton 2s ease-in-out infinite !important;
                    -moz-animation: pulseButton 2s ease-in-out infinite !important;
                    -o-animation: pulseButton 2s ease-in-out infinite !important;
                    animation: pulseButton 2s ease-in-out infinite !important;
                }

                #requestMediaBtn .btn-text {
                    background: linear-gradient(to right, #9f9f9f 0%, #fff 10%, #868686 20%) !important;
                    background-size: 200% auto !important;
                    -webkit-background-clip: text !important;
                    -webkit-text-fill-color: transparent !important;
                    background-clip: text !important;
                    -webkit-text-size-adjust: none !important;
                    display: inline-block !important;
                    -webkit-animation: shine 3s linear infinite !important;
                    -moz-animation: shine 3s linear infinite !important;
                    -o-animation: shine 3s linear infinite !important;
                    animation: shine 3s linear infinite !important;
                }

                @keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-webkit-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-moz-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-o-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @keyframes pulseButton {
                    0%, 100% {
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5), 0 0 0 0 rgba(102, 126, 234, 0.7);
                    }
                    50% {
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5), 0 0 0 8px rgba(102, 126, 234, 0);
                    }
                }

                #requestMediaBtn:hover {
                    background: rgba(70, 70, 70, 0.95) !important;
                    border-color: rgba(255, 255, 255, 0.3) !important;
                    transform: scale(1.05) !important;
                }

                #requestMediaBtn.hidden {
                    display: none !important;
                }

                /* Mobile Responsive - Dynamic scaling handled by JavaScript */
                @media screen and (max-width: 925px) {
                    #requestMediaBtn {
                        padding: 8px 16px !important;
                        font-size: 16px !important;
                        border-radius: 55px !important;
                        right: 6px !important;
                    }

                    #requestMediaBtn .btn-text {
                        font-size: 16px !important;
                    }

                    .request-badge {
                        width: 16px !important;
                        height: 16px !important;
                        font-size: 9px !important;
                        top: -5px !important;
                        right: -5px !important;
                    }
                }

                /* Notification Badge */
                .request-badge {
                    position: absolute !important;
                    top: -8px !important;
                    right: -8px !important;
                    background: #ff4444 !important;
                    color: white !important;
                    border-radius: 50% !important;
                    width: 22px !important;
                    height: 22px !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-size: 11px !important;
                    font-weight: 700 !important;
                    border: 2px solid #1e1e1e !important;
                    animation: badgePulse 1.5s ease-in-out infinite !important;
                }

                @keyframes badgePulse {
                    0%, 100% {
                        transform: scale(1);
                    }
                    50% {
                        transform: scale(1.1);
                    }
                }

                /* Button Tooltip */
                #requestMediaBtn::after {
                    content: attr(data-tooltip) !important;
                    position: absolute !important;
                    bottom: -45px !important;
                    left: 50% !important;
                    transform: translateX(-50%) !important;
                    background: rgba(0, 0, 0, 0.95) !important;
                    color: #fff !important;
                    padding: 8px 12px !important;
                    border-radius: 6px !important;
                    font-size: 12px !important;
                    white-space: nowrap !important;
                    opacity: 0 !important;
                    pointer-events: none !important;
                    transition: opacity 0.3s ease !important;
                    z-index: 10000000 !important;
                }

                #requestMediaBtn:hover::after {
                    opacity: 1 !important;
                }

                /* Search Field in Header */
                #headerSearchField {
                    position: absolute !important;
                    top: 8px;
                    right: 480px !important;
                    z-index: 999998 !important;
                    display: flex !important;
                    align-items: center !important;
                    background: rgba(60, 60, 60, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    border-radius: 25px !important;
                    padding: 8px 16px !important;
                    transition: all 0.3s ease !important;
                }

                #headerSearchField:hover {
                    background: rgba(70, 70, 70, 0.95) !important;
                    border-color: rgba(255, 255, 255, 0.4) !important;
                }

                #headerSearchField.hidden {
                    display: none !important;
                }

                #headerSearchIcon {
                    font-size: 18px !important;
                    margin-right: 8px !important;
                    cursor: pointer !important;
                    opacity: 0.8 !important;
                    transition: opacity 0.3s ease !important;
                }

                #headerSearchIcon:hover {
                    opacity: 1 !important;
                }

                #headerSearchInput {
                    background: transparent !important;
                    border: none !important;
                    outline: none !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                    width: 200px !important;
                    padding: 4px 0 !important;
                }

                #headerSearchInput::placeholder {
                    color: rgba(255, 255, 255, 0.5) !important;
                }

                /* Mobile Responsive for Search Field - Dynamic scaling handled by JavaScript */
                @media screen and (max-width: 925px) {
                    #headerSearchField {
                        left: 6px !important;
                        right: auto !important;
                        padding: 8px 16px !important;
                    }

                    #headerSearchInput {
                        width: 100px !important;
                        font-size: 14px !important;
                    }

                    #headerSearchIcon {
                        font-size: 18px !important;
                        margin-right: 8px !important;
                    }
                }

                /* Request Modal - Completely Isolated */
                #requestMediaModal {
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    width: 100% !important;
                    height: 100% !important;
                    background: rgba(0, 0, 0, 0.8) !important;
                    z-index: 9999999 !important;
                    display: none !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                }

                #requestMediaModal.show {
                    display: flex !important;
                }

                #requestMediaModalContent {
                    background: #1e1e1e !important;
                    padding: 30px !important;
                    border-radius: 15px !important;
                    max-width: 900px !important;
                    width: 90% !important;
                    max-height: 80vh !important;
                    overflow-y: auto !important;
                    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5) !important;
                    position: relative !important;
                }

                #requestMediaModalClose {
                    position: absolute !important;
                    top: 15px !important;
                    right: 15px !important;
                    font-size: 28px !important;
                    color: #999 !important;
                    cursor: pointer !important;
                    background: none !important;
                    border: none !important;
                    line-height: 1 !important;
                }

                #requestMediaModalClose:hover {
                    color: #fff !important;
                }

                #requestMediaModalTitle {
                    font-size: 24px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    margin-bottom: 20px !important;
                }

                #requestMediaModalBody {
                    color: #ccc !important;
                    font-size: 16px !important;
                }

                /* User Request Form */
                .request-input-group {
                    margin-bottom: 20px !important;
                }

                .request-input-group label {
                    display: block !important;
                    margin-bottom: 8px !important;
                    color: #fff !important;
                    font-weight: 500 !important;
                }

                .request-input-group input,
                .request-input-group textarea {
                    width: 100% !important;
                    padding: 12px !important;
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 8px !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: inherit !important;
                    box-sizing: border-box !important;
                }

                .request-input-group textarea {
                    min-height: 100px !important;
                    resize: vertical !important;
                }

                .request-input-group select {
                    width: 100% !important;
                    padding: 12px !important;
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 8px !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: inherit !important;
                    box-sizing: border-box !important;
                    cursor: pointer !important;
                }

                .request-input-group select option {
                    background: #2a2a2a !important;
                    color: #fff !important;
                }

                .request-description {
                    background: #2a2a2a !important;
                    border: 1px solid #667eea !important;
                    border-radius: 8px !important;
                    padding: 15px !important;
                    margin-bottom: 25px !important;
                    color: #ccc !important;
                    font-size: 14px !important;
                    line-height: 1.6 !important;
                }

                .request-description strong {
                    color: #fff !important;
                }

                .request-submit-btn {
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    color: white !important;
                    border: none !important;
                    padding: 12px 30px !important;
                    border-radius: 25px !important;
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.3s ease !important;
                    width: 100% !important;
                }

                .request-submit-btn:hover {
                    transform: translateY(-2px) !important;
                    box-shadow: 0 6px 20px rgba(0, 0, 0, 0.4) !important;
                }

                /* Admin Request List - Compact Table Style */
                .admin-request-list {
                    list-style: none !important;
                    padding: 0 !important;
                    margin: 0 !important;
                }

                .admin-request-item {
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 6px !important;
                    padding: 10px 15px !important;
                    margin-bottom: 8px !important;
                    display: grid !important;
                    grid-template-columns: 2fr 1fr 1.5fr 100px auto !important;
                    gap: 15px !important;
                    align-items: center !important;
                }

                .admin-request-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 14px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .admin-request-user {
                    color: #999 !important;
                    font-size: 12px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .admin-request-details {
                    color: #aaa !important;
                    font-size: 11px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .admin-request-status-badge {
                    padding: 4px 10px !important;
                    border-radius: 10px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    text-align: center !important;
                    width: fit-content !important;
                }

                .admin-request-status-badge.pending {
                    background: #ff9800 !important;
                    color: #000 !important;
                }

                .admin-request-status-badge.processing {
                    background: #2196F3 !important;
                    color: #fff !important;
                }

                .admin-request-status-badge.done {
                    background: #4CAF50 !important;
                    color: #fff !important;
                }

                .admin-request-actions {
                    display: flex !important;
                    gap: 6px !important;
                }

                .admin-status-btn {
                    padding: 5px 12px !important;
                    border: none !important;
                    border-radius: 12px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    white-space: nowrap !important;
                }

                .admin-status-btn.pending {
                    background: rgba(255, 152, 0, 0.2) !important;
                    color: #ff9800 !important;
                    border: 1px solid #ff9800 !important;
                }

                .admin-status-btn.processing {
                    background: rgba(33, 150, 243, 0.2) !important;
                    color: #2196F3 !important;
                    border: 1px solid #2196F3 !important;
                }

                .admin-status-btn.done {
                    background: rgba(76, 175, 80, 0.2) !important;
                    color: #4CAF50 !important;
                    border: 1px solid #4CAF50 !important;
                }

                .admin-status-btn:hover {
                    transform: scale(1.05) !important;
                    opacity: 1 !important;
                }

                .admin-status-btn.pending:hover {
                    background: #ff9800 !important;
                    color: #000 !important;
                }

                .admin-status-btn.processing:hover {
                    background: #2196F3 !important;
                    color: #fff !important;
                }

                .admin-status-btn.done:hover {
                    background: #4CAF50 !important;
                    color: #fff !important;
                }

                .admin-request-empty {
                    text-align: center !important;
                    color: #999 !important;
                    padding: 40px 20px !important;
                    font-style: italic !important;
                }

                /* Delete Button */
                .admin-delete-btn {
                    padding: 5px 10px !important;
                    border: 1px solid #f44336 !important;
                    border-radius: 12px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    background: rgba(244, 67, 54, 0.2) !important;
                    color: #f44336 !important;
                    transition: all 0.2s ease !important;
                }

                .admin-delete-btn:hover {
                    background: #f44336 !important;
                    color: #fff !important;
                    transform: scale(1.05) !important;
                }

                /* Hide mobile delete button on desktop */
                .admin-delete-btn.mobile-delete {
                    display: none !important;
                }

                /* Timestamps */
                .admin-request-time {
                    color: #777 !important;
                    font-size: 10px !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 2px !important;
                }

                .admin-request-time span {
                    white-space: nowrap !important;
                }

                /* Media Link Input */
                .admin-link-input {
                    padding: 6px 10px !important;
                    border: 1px solid #555 !important;
                    border-radius: 6px !important;
                    background: #2a2a2a !important;
                    color: #fff !important;
                    font-size: 11px !important;
                    width: 100% !important;
                    margin-top: 8px !important;
                }

                .admin-link-input:focus {
                    outline: none !important;
                    border-color: #4CAF50 !important;
                }

                .admin-link-input::placeholder {
                    color: #777 !important;
                }

                /* Media Link Display */
                .request-media-link {
                    display: inline-block !important;
                    margin-top: 5px !important;
                    padding: 4px 10px !important;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    color: #fff !important;
                    text-decoration: none !important;
                    border-radius: 12px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    transition: all 0.2s ease !important;
                }

                .request-media-link:hover {
                    transform: scale(1.05) !important;
                    box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4) !important;
                }

                /* Admin Status Dropdown (for mobile) */
                .admin-status-select {
                    display: none !important;
                    padding: 6px 10px !important;
                    border-radius: 8px !important;
                    border: 1px solid #555 !important;
                    background: #333 !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    min-width: 100px !important;
                }

                .admin-status-select:focus {
                    outline: none !important;
                    border-color: #667eea !important;
                }

                .admin-status-select option {
                    background: #333 !important;
                    color: #fff !important;
                    padding: 8px !important;
                }

                /* Mobile Admin Table - Card Layout */
                @media screen and (max-width: 768px) {
                    .admin-request-item {
                        grid-template-columns: 1fr !important;
                        gap: 8px !important;
                        padding: 12px !important;
                    }

                    .admin-request-title {
                        font-size: 13px !important;
                        white-space: normal !important;
                        line-height: 1.3 !important;
                    }

                    .admin-request-user {
                        font-size: 11px !important;
                    }

                    .admin-request-details {
                        font-size: 10px !important;
                        white-space: normal !important;
                        line-height: 1.3 !important;
                    }

                    .admin-request-status-badge {
                        font-size: 10px !important;
                        padding: 3px 8px !important;
                    }

                    /* Hide buttons, show dropdown on mobile */
                    .admin-request-actions {
                        display: none !important;
                    }

                    .admin-status-select {
                        display: block !important;
                        width: 100% !important;
                    }

                    /* Show mobile delete button */
                    .admin-delete-btn.mobile-delete {
                        display: block !important;
                        width: 100% !important;
                        margin-top: 8px !important;
                    }

                    /* Hide desktop delete (inside actions) */
                    .admin-request-actions .admin-delete-btn {
                        display: none !important;
                    }

                    /* Timestamps on mobile */
                    .admin-request-time {
                        font-size: 9px !important;
                    }

                    /* Link input on mobile */
                    .admin-link-input {
                        font-size: 12px !important;
                    }

                    /* Request Modal - Full width on mobile */
                    .request-media-modal .modal-content {
                        width: 95% !important;
                        max-width: none !important;
                        margin: 10px !important;
                    }

                    .request-media-modal .modal-body {
                        max-height: 70vh !important;
                        padding: 15px !important;
                    }

                    /* User request list on mobile */
                    .user-request-item {
                        flex-direction: column !important;
                        align-items: flex-start !important;
                        gap: 8px !important;
                    }

                    .user-request-time {
                        font-size: 9px !important;
                    }

                    .request-media-link {
                        font-size: 10px !important;
                        padding: 3px 8px !important;
                    }
                }

                /* User Request List - Compact Style */
                .user-request-list {
                    list-style: none !important;
                    padding: 0 !important;
                    margin: 15px 0 0 0 !important;
                    max-height: 250px !important;
                    overflow-y: auto !important;
                }

                .user-request-item {
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 6px !important;
                    padding: 8px 12px !important;
                    margin-bottom: 6px !important;
                    display: flex !important;
                    justify-content: space-between !important;
                    align-items: center !important;
                    gap: 10px !important;
                }

                .user-request-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .user-request-item-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 13px !important;
                    margin-bottom: 2px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .user-request-item-type {
                    color: #999 !important;
                    font-size: 11px !important;
                }

                .user-request-time {
                    color: #777 !important;
                    font-size: 10px !important;
                    margin-top: 3px !important;
                }

                .user-request-status {
                    padding: 4px 10px !important;
                    border-radius: 10px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                    white-space: nowrap !important;
                    flex-shrink: 0 !important;
                }

                .user-request-status.pending {
                    background: #ff9800 !important;
                    color: #000 !important;
                }

                .user-request-status.processing {
                    background: #2196F3 !important;
                    color: #fff !important;
                }

                .user-request-status.done {
                    background: #4CAF50 !important;
                    color: #fff !important;
                }

                .user-requests-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 15px !important;
                    margin-top: 20px !important;
                    margin-bottom: 8px !important;
                    padding-top: 15px !important;
                    border-top: 1px solid #444 !important;
                }

                /* Netflix-Style View Styles */
                .netflix-view-container {
                    padding: 20px 0 !important;
                    background: #141414 !important;
                    position: fixed !important;
                    top: 56px !important;
                    left: 0 !important;
                    right: 0 !important;
                    bottom: 0 !important;
                    width: 100% !important;
                    overflow-y: auto !important;
                    z-index: 100 !important;
                    display: block !important;
                    visibility: visible !important;
                    opacity: 1 !important;
                }

                .netflix-genre-row {
                    margin-bottom: 30px;
                    position: relative;
                }

                .netflix-genre-title {
                    color: #fff;
                    font-size: 1.4em;
                    font-weight: 700;
                    margin-bottom: 12px;
                    padding-left: 4%;
                    text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
                }

                .netflix-row-wrapper {
                    position: relative;
                    overflow: hidden;
                }

                .netflix-row-content {
                    display: flex;
                    overflow-x: auto;
                    scroll-behavior: smooth;
                    gap: 8px;
                    padding: 10px 4%;
                    scrollbar-width: none;
                    -ms-overflow-style: none;
                }

                .netflix-row-content::-webkit-scrollbar {
                    display: none;
                }

                .netflix-card {
                    flex: 0 0 auto;
                    width: 200px;
                    height: 300px;
                    border-radius: 4px;
                    overflow: hidden;
                    position: relative;
                    cursor: pointer;
                    transition: transform 0.3s ease;
                    background: #2a2a2a;
                }

                .netflix-card:hover {
                    transform: scale(1.08);
                    z-index: 50;
                }

                .netflix-card img {
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                }

                .netflix-card-overlay {
                    position: absolute;
                    bottom: 0;
                    left: 0;
                    right: 0;
                    background: linear-gradient(transparent, rgba(0, 0, 0, 0.9));
                    padding: 40px 10px 10px;
                    opacity: 0;
                    transition: opacity 0.3s ease;
                }

                .netflix-card:hover .netflix-card-overlay {
                    opacity: 1;
                }

                .netflix-card-title {
                    color: #fff;
                    font-size: 14px;
                    font-weight: 600;
                    white-space: nowrap;
                    overflow: hidden;
                    text-overflow: ellipsis;
                }

                .netflix-card-rating {
                    color: #ffd700;
                    font-size: 12px;
                    margin-top: 4px;
                }

                /* Rating badge on Netflix cards */
                .netflix-card.has-rating::after {
                    content: attr(data-rating);
                    position: absolute;
                    top: 8px;
                    left: 8px;
                    background: rgba(0, 0, 0, 0.85);
                    color: #fff;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.85em;
                    z-index: 10;
                    pointer-events: none;
                    font-weight: 600;
                }

                .netflix-scroll-btn {
                    position: absolute;
                    top: 50%;
                    transform: translateY(-50%);
                    width: 50px;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.6);
                    border: none;
                    color: #fff;
                    font-size: 24px;
                    cursor: pointer;
                    z-index: 200;
                    opacity: 0;
                    transition: opacity 0.3s ease;
                }

                .netflix-row-wrapper:hover .netflix-scroll-btn {
                    opacity: 1;
                }

                .netflix-scroll-btn:hover {
                    background: rgba(0, 0, 0, 0.8);
                }

                .netflix-scroll-btn.left {
                    left: 0;
                }

                .netflix-scroll-btn.right {
                    right: 0;
                }

                .netflix-loading {
                    text-align: center;
                    color: #999;
                    padding: 40px;
                    font-size: 16px;
                }

                /* Mobile responsive */
                @media screen and (max-width: 768px) {
                    .netflix-card {
                        width: 140px;
                        height: 210px;
                    }

                    .netflix-genre-title {
                        font-size: 1.1em;
                    }

                    .netflix-scroll-btn {
                        display: none;
                    }
                }

                /* New Media Notifications - Bottom Left Corner */
                .ratings-notification-container {
                    position: fixed !important;
                    bottom: 20px !important;
                    left: 20px !important;
                    z-index: 9999999 !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 10px !important;
                    max-width: 350px !important;
                    pointer-events: none !important;
                }

                .ratings-notification {
                    background: linear-gradient(135deg, rgba(30, 30, 30, 0.98) 0%, rgba(45, 45, 45, 0.98) 100%) !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-left: 4px solid #4CAF50 !important;
                    border-radius: 12px !important;
                    padding: 16px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255, 255, 255, 0.05) !important;
                    animation: notificationSlideIn 0.4s cubic-bezier(0.4, 0, 0.2, 1) forwards !important;
                    pointer-events: auto !important;
                    display: flex !important;
                    gap: 12px !important;
                    align-items: flex-start !important;
                    backdrop-filter: blur(10px) !important;
                    -webkit-backdrop-filter: blur(10px) !important;
                }

                .ratings-notification.test-notification {
                    border-left-color: #2196F3 !important;
                }

                .ratings-notification.hiding {
                    animation: notificationSlideOut 0.3s cubic-bezier(0.4, 0, 0.2, 1) forwards !important;
                }

                @keyframes notificationSlideIn {
                    from {
                        opacity: 0;
                        transform: translateX(-100%);
                    }
                    to {
                        opacity: 1;
                        transform: translateX(0);
                    }
                }

                @keyframes notificationSlideOut {
                    from {
                        opacity: 1;
                        transform: translateX(0);
                    }
                    to {
                        opacity: 0;
                        transform: translateX(-100%);
                    }
                }

                .ratings-notification-image {
                    width: 50px !important;
                    height: 75px !important;
                    border-radius: 6px !important;
                    object-fit: cover !important;
                    flex-shrink: 0 !important;
                    background: #333 !important;
                }

                .ratings-notification-content {
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .ratings-notification-header {
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    margin-bottom: 4px !important;
                }

                .ratings-notification-icon {
                    font-size: 14px !important;
                }

                .ratings-notification-label {
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    color: #4CAF50 !important;
                    text-transform: uppercase !important;
                    letter-spacing: 0.5px !important;
                }

                .test-notification .ratings-notification-label {
                    color: #2196F3 !important;
                }

                .ratings-notification-title {
                    font-size: 15px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    margin-bottom: 4px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .ratings-notification-meta {
                    font-size: 12px !important;
                    color: #aaa !important;
                }

                .ratings-notification-message {
                    font-size: 13px !important;
                    color: #ccc !important;
                    line-height: 1.4 !important;
                }

                .ratings-notification-close {
                    position: absolute !important;
                    top: 8px !important;
                    right: 8px !important;
                    background: none !important;
                    border: none !important;
                    color: #666 !important;
                    font-size: 18px !important;
                    cursor: pointer !important;
                    padding: 4px !important;
                    line-height: 1 !important;
                    transition: color 0.2s ease !important;
                }

                .ratings-notification-close:hover {
                    color: #fff !important;
                }

                .ratings-notification {
                    position: relative !important;
                }

                /* Admin Test Notification Button */
                #testNotificationBtn {
                    position: absolute !important;
                    top: 8px !important;
                    right: 700px !important;
                    background: rgba(33, 150, 243, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    padding: 10px 20px !important;
                    border-radius: 20px !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    z-index: 999999 !important;
                    transition: all 0.3s ease !important;
                    color: #fff !important;
                    font-family: "Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                }

                #testNotificationBtn:hover {
                    background: rgba(33, 150, 243, 1) !important;
                    transform: scale(1.05) !important;
                }

                #testNotificationBtn.hidden {
                    display: none !important;
                }

                @media screen and (max-width: 925px) {
                    #testNotificationBtn {
                        display: none !important;
                    }

                    .ratings-notification-container {
                        left: 10px !important;
                        right: 10px !important;
                        max-width: none !important;
                    }

                    .ratings-notification {
                        padding: 12px !important;
                    }

                    .ratings-notification-image {
                        width: 40px !important;
                        height: 60px !important;
                    }

                    .ratings-notification-title {
                        font-size: 14px !important;
                    }
                }
            `;

            const styleSheet = document.createElement('style');
            styleSheet.id = 'ratingsPluginStyles';
            styleSheet.textContent = styles;
            document.head.appendChild(styleSheet);
        },

        /**
         * Observe detail pages for item changes
         */
        observeDetailPages: function () {
            const self = this;
            let lastUrl = location.href;
            let lastItemId = null;
            let checkTimer = null;

            const checkForPageChange = function () {
                // Debounce rapid checks
                if (checkTimer) {
                    clearTimeout(checkTimer);
                }

                checkTimer = setTimeout(() => {
                    const url = location.href;
                    const itemId = self.getItemIdFromUrl();

                    // Only trigger if URL changed or item ID changed
                    if (url !== lastUrl || itemId !== lastItemId) {
                        lastUrl = url;
                        lastItemId = itemId;

                        // Remove old component if it exists
                        const oldComponent = document.getElementById('ratingsPluginComponent');
                        if (oldComponent) {
                            oldComponent.remove();
                        }

                        self.onPageChange();
                    }
                }, 100); // Small debounce to prevent multiple rapid fires
            };

            // Listen for hash changes (Jellyfin uses hash-based routing)
            window.addEventListener('hashchange', checkForPageChange);

            // Listen for popstate (back/forward navigation)
            window.addEventListener('popstate', checkForPageChange);

            // Use setInterval as more aggressive polling for SPA navigation detection
            setInterval(() => {
                const url = location.href;
                const itemId = self.getItemIdFromUrl();

                if (url !== lastUrl || itemId !== lastItemId) {
                    lastUrl = url;
                    lastItemId = itemId;

                    // Remove old component if it exists
                    const oldComponent = document.getElementById('ratingsPluginComponent');
                    if (oldComponent) {
                        oldComponent.remove();
                    }

                    self.onPageChange();
                }
            }, 500); // Check every 500ms for URL changes

            // Initial check
            this.onPageChange();
        },

        /**
         * Handle page change
         */
        onPageChange: function () {
            const itemId = this.getItemIdFromUrl();
            if (itemId) {
                this.waitForElementAndInject(itemId);
            }
        },

        /**
         * Wait for page elements to load before injecting
         */
        waitForElementAndInject: function (itemId) {
            const self = this;
            let attempts = 0;
            const maxAttempts = 100; // Try for ~10 seconds max

            const checkInterval = setInterval(() => {
                attempts++;

                // Check if the detailLogo element exists
                const detailLogo = document.querySelector('.detailLogo');

                // If we found detailLogo, inject
                if (detailLogo) {
                    clearInterval(checkInterval);
                    self.injectRatingComponent(itemId);
                } else if (attempts >= maxAttempts) {
                    // Give up after max attempts
                    clearInterval(checkInterval);
                }
            }, 100); // Check every 100ms for faster detection
        },

        /**
         * Get item ID from URL
         */
        getItemIdFromUrl: function () {
            // Try multiple URL patterns
            const url = window.location.href;
            const pathname = window.location.pathname;
            const hash = window.location.hash;
            const search = window.location.search;


            // Pattern 1: Hash-based routing (#!/details?id=...)
            let match = hash.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 2: Query string (?id=...)
            match = search.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 3: Path-based (/item/id or /details/id)
            match = pathname.match(/\/(?:item|details)\/([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 4: Anywhere in URL
            match = url.match(/id=([a-f0-9]{32})/i);
            if (match) {
                return match[1];
            }

            return null;
        },

        /**
         * Inject rating component into the page
         */
        injectRatingComponent: function (itemId) {
            if (document.getElementById('ratingsPluginComponent')) {
                return; // Already injected
            }

            const container = document.createElement('div');
            container.id = 'ratingsPluginComponent';
            container.className = 'ratings-plugin-container';

            container.innerHTML = `
                <div class="ratings-plugin-stars" id="ratingsPluginStars">
                    ${this.generateStars()}
                    <div class="ratings-plugin-popup" id="ratingsPluginPopup">
                        <div class="ratings-plugin-popup-title">User Ratings</div>
                        <ul class="ratings-plugin-popup-list" id="ratingsPluginPopupList">
                            <li class="ratings-plugin-popup-empty">Loading...</li>
                        </ul>
                    </div>
                </div>
                <div class="ratings-plugin-stats" id="ratingsPluginStats">
                    <span class="ratings-plugin-loading">Loading ratings...</span>
                </div>
            `;

            // Search for detailLogo globally (not inside a specific container)
            const detailLogo = document.querySelector('.detailLogo');

            if (detailLogo) {
                // Use insertAdjacentElement to insert immediately after detailLogo
                detailLogo.insertAdjacentElement('afterend', container);
            } else {
                // Fallback: try to find any suitable container
                const detailPageContent = document.querySelector('.detailPageContent') ||
                                         document.querySelector('.itemDetailPage') ||
                                         document.querySelector('.detailPage-content');
                if (detailPageContent) {
                    detailPageContent.insertBefore(container, detailPageContent.firstChild);
                }
            }

            this.attachEventListeners(itemId);
            this.loadRatings(itemId);
        },

        /**
         * Generate star HTML
         */
        generateStars: function () {
            let html = '';
            for (let i = 1; i <= 10; i++) {
                html += `<span class="ratings-plugin-star" data-rating="${i}">★</span>`;
            }
            return html;
        },

        /**
         * Attach event listeners
         */
        attachEventListeners: function (itemId) {
            const stars = document.querySelectorAll('.ratings-plugin-star');
            const popup = document.getElementById('ratingsPluginPopup');
            const starsContainer = document.getElementById('ratingsPluginStars');

            stars.forEach(star => {
                star.addEventListener('click', () => {
                    const rating = parseInt(star.getAttribute('data-rating'));
                    this.submitRating(itemId, rating);
                });

                star.addEventListener('mouseenter', () => {
                    const rating = parseInt(star.getAttribute('data-rating'));
                    this.highlightStars(rating);
                });
            });

            starsContainer.addEventListener('mouseleave', () => {
                this.loadRatings(itemId); // Refresh to show actual rating
            });

            // Show popup on hover over stars container
            starsContainer.addEventListener('mouseenter', () => {
                this.showDetailedRatings(itemId);
            });

            starsContainer.addEventListener('mouseleave', () => {
                popup.classList.remove('visible');
            });
        },

        /**
         * Highlight stars up to rating
         */
        highlightStars: function (rating) {
            const stars = document.querySelectorAll('.ratings-plugin-star');
            stars.forEach((star, index) => {
                if (index < rating) {
                    star.classList.add('hover');
                } else {
                    star.classList.remove('hover');
                }
            });
        },

        /**
         * Load ratings for item
         */
        loadRatings: function (itemId) {
            const self = this;
            const statsElement = document.getElementById('ratingsPluginStats');


            // Build URL with authentication
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

            // Get deviceId from localStorage or generate one
            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            // Build proper X-Emby-Authorization header
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;


            fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(stats => {
                    self.updateStarDisplay(stats.UserRating || 0);

                    let statsHtml = '';
                    if (stats.TotalRatings > 0) {
                        statsHtml = `<span class="ratings-plugin-average">${stats.AverageRating.toFixed(1)}/10</span> - ${stats.TotalRatings} rating${stats.TotalRatings !== 1 ? 's' : ''}`;
                        if (stats.UserRating) {
                            statsHtml += `<div class="ratings-plugin-your-rating">Your rating: ${stats.UserRating}/10</div>`;
                        }
                    } else {
                        statsHtml = 'No ratings yet. Be the first to rate!';
                    }

                    if (statsElement) {
                        statsElement.innerHTML = statsHtml;
                    }
                })
                .catch(err => {
                    if (statsElement) {
                        statsElement.innerHTML = 'Error loading ratings';
                    }
                });
        },

        /**
         * Update star display
         */
        updateStarDisplay: function (rating) {
            const stars = document.querySelectorAll('.ratings-plugin-star');

            stars.forEach((star, index) => {
                star.classList.remove('filled', 'hover');
                if (index < rating) {
                    star.classList.add('filled');
                }
            });
        },

        /**
         * Submit rating
         */
        submitRating: function (itemId, rating) {
            const self = this;


            if (!window.ApiClient) {
                return;
            }

            // Gather all authentication info
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Rating?rating=${rating}`;


            // Build proper X-Emby-Authorization header (Jellyfin's dedicated auth header)
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestOptions = {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            };

            fetch(url, requestOptions)
                .then(function(response) {

                    if (!response.ok) {
                        return response.text().then(function(errorText) {
                            throw new Error('HTTP ' + response.status + ': ' + errorText);
                        });
                    }
                    return response.text().then(function(text) {
                        return text ? JSON.parse(text) : {};
                    });
                })
                .then(function(data) {

                    // Immediately update the star display for instant feedback
                    self.updateStarDisplay(rating);

                    // Then reload full stats from server
                    self.loadRatings(itemId);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Rated ' + rating + '/10');
                        });
                    }
                })
                .catch(function(err) {

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error submitting rating: ' + err.message);
                        });
                    }
                });
        },

        /**
         * Show detailed ratings popup
         */
        showDetailedRatings: function (itemId) {
            const popup = document.getElementById('ratingsPluginPopup');
            const popupList = document.getElementById('ratingsPluginPopupList');

            popup.classList.add('visible');
            popupList.innerHTML = '<li class="ratings-plugin-popup-empty">Loading...</li>';

            ApiClient.getJSON(ApiClient.getUrl(`Ratings/Items/${itemId}/DetailedRatings`))
                .then(ratings => {
                    if (ratings && ratings.length > 0) {
                        let html = '';
                        ratings.forEach(rating => {
                            html += `
                                <li class="ratings-plugin-popup-item">
                                    <span class="ratings-plugin-popup-username">${this.escapeHtml(rating.Username)}</span>
                                    <span class="ratings-plugin-popup-rating">${rating.Rating}/10</span>
                                </li>
                            `;
                        });
                        popupList.innerHTML = html;
                    } else {
                        popupList.innerHTML = '<li class="ratings-plugin-popup-empty">No ratings yet</li>';
                    }
                })
                .catch(err => {
                    popupList.innerHTML = '<li class="ratings-plugin-popup-empty">Error loading ratings</li>';
                });
        },

        /**
         * Escape HTML to prevent XSS
         */
        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        /**
         * Observe home page cards and add rating overlays
         */
        observeHomePageCards: function () {
            const self = this;

            // Use IntersectionObserver to only load ratings for visible cards
            const intersectionObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const card = entry.target;

                        // Find the image container within this card
                        const imageContainer = card.querySelector('.cardImageContainer, .cardContent, .card-imageContainer');
                        if (!imageContainer) {
                            return;
                        }

                        // Skip if already has rating overlay
                        if (imageContainer.classList.contains('has-rating')) {
                            return;
                        }

                        // Get item ID from the card
                        const itemId = self.getItemIdFromCard(card);
                        if (!itemId) {
                            return;
                        }

                        // Fetch rating for this item (with caching)
                        self.addCardRating(imageContainer, itemId);

                        // Stop observing this card once we've processed it
                        intersectionObserver.unobserve(card);
                    }
                });
            }, {
                rootMargin: '50px' // Start loading slightly before card comes into view
            });

            // Create MutationObserver to watch for new cards being added to DOM
            const mutationObserver = new MutationObserver(() => {
                // Find all cards that aren't being observed yet
                const cards = document.querySelectorAll('.card:not(.card .card)');
                cards.forEach(card => {
                    // Only observe if not already being watched
                    if (!card.dataset.ratingsObserved) {
                        card.dataset.ratingsObserved = 'true';
                        intersectionObserver.observe(card);
                    }
                });
            });

            // Start observing DOM for new cards
            mutationObserver.observe(document.body, {
                childList: true,
                subtree: true
            });

            // Initial scan for existing cards
            setTimeout(() => {
                const cards = document.querySelectorAll('.card:not(.card .card)');
                cards.forEach(card => {
                    card.dataset.ratingsObserved = 'true';
                    intersectionObserver.observe(card);
                });
            }, 2000);
        },

        /**
         * Get item ID from a card element
         */
        getItemIdFromCard: function (card) {
            // Try data-id attribute first (most reliable)
            const dataId = card.getAttribute('data-id');
            if (dataId && dataId.length === 32) {
                const dataType = card.getAttribute('data-type');
                const isFolder = card.getAttribute('data-isfolder');

                // Check data-type first (most reliable indicator)
                if (dataType === 'CollectionFolder' || dataType === 'UserView') {
                    return null; // Skip folders
                }

                // Allow Series, Movie, Episode, etc. even if data-isfolder="true"
                // (Series items have isfolder=true but are actual media items)
                if (dataType === 'Series' || dataType === 'Movie' || dataType === 'Episode' ||
                    dataType === 'Audio' || dataType === 'MusicAlbum' || dataType === 'Video') {
                    return dataId;
                }

                // If no recognized media type but has isfolder=true, skip it
                if (isFolder === 'true') {
                    return null;
                }

                return dataId;
            }

            // Try to find link with item ID
            const link = card.querySelector('a[href*="id="]');
            if (link) {
                // Skip library/folder navigation links (these have topParentId or parentId)
                if (link.href.includes('topParentId=') || link.href.includes('parentId=')) {
                    return null;
                }

                // Skip list views
                if (link.href.includes('#/list')) {
                    return null;
                }

                // Extract item ID - works for both #/details and #/tv?id= and #/movies?id= formats
                const match = link.href.match(/[?&]id=([a-f0-9]{32})/i);
                if (match) {
                    return match[1];
                }
            }

            // Try parent link
            const parentLink = card.closest('a[href*="id="]');
            if (parentLink) {
                // Skip library/folder navigation links
                if (parentLink.href.includes('topParentId=') || parentLink.href.includes('parentId=')) {
                    return null;
                }

                // Skip list views
                if (parentLink.href.includes('#/list')) {
                    return null;
                }

                // Extract item ID
                const match = parentLink.href.match(/[?&]id=([a-f0-9]{32})/i);
                if (match) {
                    return match[1];
                }
            }

            return null;
        },

        /**
         * Add rating overlay to a specific card
         */
        addCardRating: function (card, itemId) {
            const self = this;

            // Check cache first
            if (self.ratingsCache[itemId] !== undefined) {
                // Use cached data
                if (self.ratingsCache[itemId] !== null) {
                    const stats = self.ratingsCache[itemId];
                    self.createAndPositionOverlay(card, stats);
                }
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(stats => {
                    // Only show if there's at least one rating
                    if (stats.TotalRatings > 0) {
                        // Cache the stats
                        self.ratingsCache[itemId] = stats;
                        self.createAndPositionOverlay(card, stats);
                    } else {
                        // Cache as null (no ratings)
                        self.ratingsCache[itemId] = null;
                    }
                })
                .catch(err => {
                    // Cache as null on error to avoid retrying
                    self.ratingsCache[itemId] = null;
                });
        },

        /**
         * Create and position overlay using CSS ::after pseudo-element
         */
        createAndPositionOverlay: function (imageContainer, stats) {
            // Use CSS ::after pseudo-element by adding class and data attribute
            imageContainer.classList.add('has-rating');
            imageContainer.setAttribute('data-rating', '★ ' + stats.AverageRating.toFixed(1));
        },

        /**
         * Initialize Request Media Button - Completely isolated and safe
         */
        initRequestButton: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('requestMediaBtn')) {
                    return;
                }

                // Create button with position relative for badge
                const btn = document.createElement('button');
                btn.id = 'requestMediaBtn';
                btn.style.position = 'relative';
                btn.innerHTML = '<span class="btn-text">Request Media</span>';
                btn.setAttribute('type', 'button');
                btn.setAttribute('data-tooltip', 'Request movies or TV series from admin');

                // Update badge periodically
                self.updateRequestBadge(btn);
                setInterval(() => self.updateRequestBadge(btn), 30000); // Update every 30 seconds

                // Create modal
                const modal = document.createElement('div');
                modal.id = 'requestMediaModal';
                modal.innerHTML = `
                    <div id="requestMediaModalContent">
                        <button id="requestMediaModalClose" type="button">&times;</button>
                        <div id="requestMediaModalTitle">Request Media</div>
                        <div id="requestMediaModalBody">
                            <p style="text-align: center; color: #999;">Loading...</p>
                        </div>
                    </div>
                `;

                // Add to DOM - append to header container so they scroll with header
                const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                if (headerContainer) {
                    // Make header container position relative so absolute positioning works
                    headerContainer.style.position = 'relative';
                    headerContainer.appendChild(btn);
                } else {
                    document.body.appendChild(btn);
                }
                document.body.appendChild(modal);

                // Button click - wrapped in try-catch
                btn.addEventListener('click', (e) => {
                    try {
                        e.preventDefault();
                        e.stopPropagation();
                        modal.classList.add('show');
                        self.loadRequestInterface();
                    } catch (err) {
                        console.error('Button click error:', err);
                    }
                });

                // Close button - wrapped in try-catch
                const closeBtn = document.getElementById('requestMediaModalClose');
                if (closeBtn) {
                    closeBtn.addEventListener('click', (e) => {
                        try {
                            e.preventDefault();
                            e.stopPropagation();
                            modal.classList.remove('show');
                        } catch (err) {
                            console.error('Close button error:', err);
                        }
                    });
                }

                // Click outside to close - wrapped in try-catch
                modal.addEventListener('click', (e) => {
                    try {
                        if (e.target === modal) {
                            e.preventDefault();
                            e.stopPropagation();
                            modal.classList.remove('show');
                        }
                    } catch (err) {
                        console.error('Modal click error:', err);
                    }
                });

                // Hide during video playback and on login page - wrapped in try-catch
                setInterval(() => {
                    try {
                        const videoPlayer = document.querySelector('.videoPlayerContainer');
                        const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');

                        // Check if on login page
                        const isLoginPage = self.isOnLoginPage();

                        if (isVideoPlaying || isLoginPage) {
                            btn.classList.add('hidden');
                        } else {
                            btn.classList.remove('hidden');
                        }
                    } catch (err) {
                        // Silently fail - don't break anything
                    }
                }, 1000);

                // Listen for user changes to clear cache
                self.setupUserChangeListener();

            } catch (err) {
                console.error('Request button initialization failed:', err);
                // Fail silently - don't break the plugin
            }
        },

        /**
         * Check if currently on login page
         */
        isOnLoginPage: function () {
            try {
                // Check URL for login indicators
                const url = window.location.href.toLowerCase();
                const hash = window.location.hash.toLowerCase();

                // Check URL patterns for login/startup pages
                if (url.includes('/login') ||
                    url.includes('/startup') ||
                    hash.includes('login') ||
                    hash.includes('startup') ||
                    hash === '#' ||
                    hash === '') {

                    // Double check - if there's a login form visible, it's definitely login page
                    const loginForm = document.querySelector('.loginPage, #loginPage, .manualLoginForm, #manualLoginForm, .selectServer');
                    if (loginForm) {
                        return true;
                    }

                    // If URL suggests login but no login form, check if user exists
                    if (window.ApiClient && ApiClient.getCurrentUserId()) {
                        return false; // User is logged in, not a login page
                    }

                    // Only return true for login URL if we're sure
                    if (hash.includes('login') || url.includes('/login')) {
                        return true;
                    }
                }

                // Check if login form is visible (definitive check)
                const loginForm = document.querySelector('.loginPage, #loginPage, .manualLoginForm, #manualLoginForm');
                if (loginForm && loginForm.offsetParent !== null) {
                    return true;
                }

                return false;
            } catch (err) {
                return false;
            }
        },

        /**
         * Setup listener for user changes (login/logout)
         */
        setupUserChangeListener: function () {
            const self = this;
            try {
                // Store current user ID to detect changes
                let lastUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;

                // Check for user changes periodically
                setInterval(() => {
                    try {
                        const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;

                        // User changed (login/logout/switch account)
                        if (currentUserId !== lastUserId) {
                            // Clear all cached data
                            self.clearRequestCache();
                            lastUserId = currentUserId;

                            // Update badge for new user
                            const btn = document.getElementById('requestMediaBtn');
                            if (btn && currentUserId) {
                                self.updateRequestBadge(btn);
                            }

                            // Remove test notification button and re-check admin status
                            const testBtn = document.getElementById('testNotificationBtn');
                            if (testBtn) {
                                testBtn.remove();
                            }
                            // Re-initialize test button (will only show if new user is admin)
                            if (currentUserId) {
                                self.initTestNotificationButton();
                            }

                            // Clear shown notification IDs for new user
                            self.shownNotificationIds = [];
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 2000);

                // Also listen for Jellyfin events if available
                if (window.Events) {
                    Events.on(ApiClient, 'authenticated', () => {
                        self.clearRequestCache();
                        const btn = document.getElementById('requestMediaBtn');
                        if (btn) {
                            self.updateRequestBadge(btn);
                        }
                    });
                }
            } catch (err) {
                console.error('Error setting up user change listener:', err);
            }
        },

        /**
         * Clear all cached request data
         */
        clearRequestCache: function () {
            try {
                // Clear viewed request IDs
                localStorage.removeItem('ratings_viewed_requests');

                // Clear any other cached data related to requests
                const keysToRemove = [];
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    if (key && key.startsWith('ratings_')) {
                        keysToRemove.push(key);
                    }
                }
                keysToRemove.forEach(key => localStorage.removeItem(key));

            } catch (err) {
                console.error('Error clearing request cache:', err);
            }
        },

        /**
         * Initialize search field in header
         */
        initSearchField: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('headerSearchField')) {
                    return;
                }

                // Wait for DOM to be ready
                const createSearchField = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('headerSearchField')) {
                            return;
                        }

                        // Create search container
                        const searchContainer = document.createElement('div');
                        searchContainer.id = 'headerSearchField';

                        // Create search input
                        const searchInput = document.createElement('input');
                        searchInput.type = 'text';
                        searchInput.placeholder = 'Search...';
                        searchInput.id = 'headerSearchInput';

                        // Create search icon
                        const searchIcon = document.createElement('span');
                        searchIcon.id = 'headerSearchIcon';
                        searchIcon.innerHTML = '🔍';

                        // Append elements
                        searchContainer.appendChild(searchIcon);
                        searchContainer.appendChild(searchInput);

                        // Append to header container so it scrolls with header
                        const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                        if (headerContainer) {
                            headerContainer.style.position = 'relative';
                            headerContainer.appendChild(searchContainer);
                        } else {
                            document.body.appendChild(searchContainer);
                        }

                        // Real-time search filtering
                        let searchTimeout;
                        searchInput.addEventListener('input', function() {
                            // Update icon based on input
                            if (searchInput.value.trim()) {
                                searchIcon.innerHTML = '✕';
                                searchIcon.style.fontSize = '20px';
                            } else {
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                            }

                            clearTimeout(searchTimeout);
                            searchTimeout = setTimeout(() => {
                                const query = searchInput.value.trim();
                                const currentUrl = window.location.href;
                                const isHomePage = currentUrl.includes('/home.html') || currentUrl.endsWith('/web/') || currentUrl.endsWith('/web/index.html') || currentUrl.includes('#/home');

                                if (isHomePage && query) {
                                    // Use full library search on homepage
                                    self.searchFullLibrary(query);
                                } else if (isHomePage && !query) {
                                    // Clear full library search and restore homepage
                                    self.clearFullLibrarySearch();
                                } else {
                                    // Use page filtering on other pages
                                    self.filterCurrentPageContent(query);
                                }
                            }, 300); // Debounce for performance
                        });

                        // Handle enter key - also filter/search
                        searchInput.addEventListener('keypress', function(e) {
                            if (e.key === 'Enter') {
                                const query = searchInput.value.trim();
                                const currentUrl = window.location.href;
                                const isHomePage = currentUrl.includes('/home.html') || currentUrl.endsWith('/web/') || currentUrl.endsWith('/web/index.html') || currentUrl.includes('#/home');

                                if (isHomePage && query) {
                                    self.searchFullLibrary(query);
                                } else if (isHomePage && !query) {
                                    self.clearFullLibrarySearch();
                                } else {
                                    self.filterCurrentPageContent(query);
                                }
                            }
                        });

                        // Handle icon click - clear search or focus
                        searchIcon.addEventListener('click', function() {
                            if (searchInput.value.trim()) {
                                searchInput.value = '';
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';

                                const currentUrl = window.location.href;
                                const isHomePage = currentUrl.includes('/home.html') || currentUrl.endsWith('/web/') || currentUrl.endsWith('/web/index.html') || currentUrl.includes('#/home');

                                if (isHomePage) {
                                    self.clearFullLibrarySearch();
                                } else {
                                    self.filterCurrentPageContent('');
                                }
                            } else {
                                searchInput.focus();
                            }
                        });

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    searchContainer.classList.add('hidden');
                                } else {
                                    searchContainer.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating search field:', err);
                    }
                };

                // Try to create immediately
                setTimeout(createSearchField, 1500);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('headerSearchField')) {
                        setTimeout(createSearchField, 500);
                    }
                });

                // Listen for Jellyfin navigation events
                try {
                    if (window.Emby && window.Emby.Page && typeof Emby.Page.addEventListener === 'function') {
                        Emby.Page.addEventListener('pageshow', () => {
                            if (!document.getElementById('headerSearchField')) {
                                setTimeout(createSearchField, 500);
                            } else {
                                // Clear search when navigating to a new page
                                const searchInput = document.getElementById('headerSearchInput');
                                const searchIcon = document.getElementById('headerSearchIcon');
                                if (searchInput && searchInput.value.trim()) {
                                    searchInput.value = '';
                                    if (searchIcon) {
                                        searchIcon.innerHTML = '🔍';
                                        searchIcon.style.fontSize = '18px';
                                    }
                                    self.filterCurrentPageContent('');
                                }
                            }
                        });
                    }
                } catch (e) {
                    // Emby.Page.addEventListener not available
                }

                // Monitor for URL changes to clear search (works for SPA navigation)
                let lastUrl = window.location.href;
                setInterval(() => {
                    try {
                        const currentUrl = window.location.href;
                        if (currentUrl !== lastUrl) {
                            lastUrl = currentUrl;
                            const searchInput = document.getElementById('headerSearchInput');
                            const searchIcon = document.getElementById('headerSearchIcon');
                            if (searchInput && searchInput.value.trim()) {
                                searchInput.value = '';
                                if (searchIcon) {
                                    searchIcon.innerHTML = '🔍';
                                    searchIcon.style.fontSize = '18px';
                                }
                                // Reset filters and clear full library search
                                self.filterCurrentPageContent('');
                                self.clearFullLibrarySearch();
                            } else {
                                // Also clear full library search when URL changes even if search is empty
                                self.clearFullLibrarySearch();
                            }
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 500);

                // Also listen for hash changes (SPA navigation)
                window.addEventListener('hashchange', () => {
                    try {
                        const searchInput = document.getElementById('headerSearchInput');
                        const searchIcon = document.getElementById('headerSearchIcon');
                        if (searchInput && searchInput.value.trim()) {
                            searchInput.value = '';
                            if (searchIcon) {
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                            }
                            self.filterCurrentPageContent('');
                        }
                    } catch (err) {
                        // Silently fail
                    }
                });

            } catch (err) {
                console.error('Search field initialization failed:', err);
            }
        },

        /**
         * Update dynamic responsive scaling based on window width
         */
        updateResponsiveScaling: function () {
            const updateScale = () => {
                const width = window.innerWidth;
                let scale = 1;
                let searchWidth = 200; // Default width
                let btnPaddingH = 16; // Horizontal padding for button

                if (width <= 300) {
                    scale = 0.5;
                    searchWidth = 100;
                    btnPaddingH = 8;
                } else if (width <= 500) {
                    // Extra scaling for search field below 500px
                    scale = 0.5 + ((width - 300) / (500 - 300)) * 0.3; // 0.5 to 0.8
                    searchWidth = 100 + ((width - 300) / (500 - 300)) * 50; // 100px to 150px
                    btnPaddingH = 8 + ((width - 300) / (500 - 300)) * 4; // 8px to 12px
                } else if (width < 925) {
                    // Linear interpolation: scale from 1.0 at 925px to 0.8 at 500px
                    scale = 0.8 + ((width - 500) / (925 - 500)) * 0.2;
                    searchWidth = 150 + ((width - 500) / (925 - 500)) * 50; // 150px to 200px
                    btnPaddingH = 12 + ((width - 500) / (925 - 500)) * 4; // 12px to 16px
                }

                // Detect if on Movies or TV Shows page by URL
                const currentUrl = window.location.href;
                const isMoviesOrTVPage = (currentUrl.includes('#/movies?') || currentUrl.includes('#/tv?')) &&
                                        (currentUrl.includes('collectionType=movies') || currentUrl.includes('collectionType=tvshows'));
                const topPosition = (width <= 925 && isMoviesOrTVPage) ? '105px' : (width <= 925 ? '55px' : '');

                // Extend header height when on Movies/TV pages at ≤925px
                const tabsSlider = document.querySelector('.emby-tabs-slider');
                if (tabsSlider) {
                    if (width <= 925 && isMoviesOrTVPage) {
                        tabsSlider.style.paddingBottom = '50px';
                    } else {
                        tabsSlider.style.paddingBottom = '';
                    }
                }

                // Push content down on Movies/TV pages - try multiple containers
                const contentSelectors = [
                    '.mainAnimatedPage',
                    '.page',
                    '[data-role="page"]',
                    '.itemsContainer',
                    '.verticalSection',
                    '.netflix-view-container',
                    '.netflix-genre-row',
                    '.netflix-genre-title'
                ];

                contentSelectors.forEach(selector => {
                    const elements = document.querySelectorAll(selector);
                    elements.forEach(element => {
                        if (element) {
                            if (width <= 925 && isMoviesOrTVPage) {
                                element.style.paddingTop = '50px';
                            } else {
                                element.style.paddingTop = '';
                            }
                        }
                    });
                });

                const searchField = document.getElementById('headerSearchField');
                const searchInput = document.getElementById('headerSearchInput');
                const requestBtn = document.getElementById('requestMediaBtn');

                if (searchField) {
                    if (width <= 925) {
                        searchField.style.transform = `scale(${scale})`;
                        searchField.style.transformOrigin = 'left center';
                        searchField.style.top = topPosition;
                    } else {
                        searchField.style.transform = '';
                        searchField.style.top = '';
                    }
                }

                if (searchInput) {
                    if (width <= 925) {
                        searchInput.style.width = `${searchWidth}px`;
                    } else {
                        searchInput.style.width = '';
                    }
                }

                if (requestBtn) {
                    if (width <= 925) {
                        requestBtn.style.transform = `scale(${scale})`;
                        requestBtn.style.transformOrigin = 'right center';
                        requestBtn.style.paddingLeft = `${btnPaddingH}px`;
                        requestBtn.style.paddingRight = `${btnPaddingH}px`;
                        requestBtn.style.top = topPosition;
                    } else {
                        requestBtn.style.transform = '';
                        requestBtn.style.paddingLeft = '';
                        requestBtn.style.paddingRight = '';
                        requestBtn.style.top = '';
                    }
                }
            };

            // Update on load
            updateScale();

            // Update on resize with debounce
            let resizeTimeout;
            window.addEventListener('resize', () => {
                clearTimeout(resizeTimeout);
                resizeTimeout = setTimeout(updateScale, 100);
            });

            // Monitor URL changes for SPA navigation
            let lastUrl = window.location.href;
            const urlCheckInterval = setInterval(() => {
                const currentUrl = window.location.href;
                if (currentUrl !== lastUrl) {
                    lastUrl = currentUrl;
                    // URL changed, update positioning
                    setTimeout(updateScale, 100);
                }
            }, 300);

            // Also listen for popstate (back/forward navigation)
            window.addEventListener('popstate', () => {
                setTimeout(updateScale, 100);
            });

            // Listen for hash changes
            window.addEventListener('hashchange', () => {
                setTimeout(updateScale, 100);
            });

            // MutationObserver to detect when Netflix content is loaded dynamically
            const observer = new MutationObserver((mutations) => {
                for (const mutation of mutations) {
                    // Check if any added nodes contain Netflix genre rows or titles
                    for (const node of mutation.addedNodes) {
                        if (node.nodeType === 1) { // Element node
                            if (node.classList && (node.classList.contains('netflix-genre-row') ||
                                                   node.classList.contains('netflix-view-container') ||
                                                   node.classList.contains('netflix-genre-title'))) {
                                // Netflix content added, trigger update
                                setTimeout(updateScale, 50);
                                return;
                            }
                            // Check children too
                            if (node.querySelector && (node.querySelector('.netflix-genre-row') ||
                                                       node.querySelector('.netflix-view-container') ||
                                                       node.querySelector('.netflix-genre-title'))) {
                                setTimeout(updateScale, 50);
                                return;
                            }
                        }
                    }
                }
            });

            // Start observing the main content area for Netflix content
            const observeTarget = document.querySelector('.mainAnimatedPage') || document.body;
            observer.observe(observeTarget, {
                childList: true,
                subtree: true
            });
        },

        /**
         * Search full library using Jellyfin API and display results
         */
        searchFullLibrary: async function (query) {
            try {
                if (!query || !window.ApiClient) {
                    console.log('RatingsPlugin: Search cancelled - no query or ApiClient');
                    return;
                }

                console.log('RatingsPlugin: Searching full library for:', query);

                const userId = ApiClient.getCurrentUserId();
                const baseUrl = ApiClient.serverAddress();

                // Use Jellyfin's search hints API
                const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${encodeURIComponent(query)}&UserId=${userId}&IncludeItemTypes=Movie,Series,Episode&Limit=50`;

                const response = await fetch(searchUrl, {
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Firefox", DeviceId="${ApiClient.deviceId()}", Version="10.11.0", Token="${ApiClient.accessToken()}"`
                    }
                });

                if (!response.ok) {
                    console.error('RatingsPlugin: Search API failed:', response.status);
                    return;
                }

                const data = await response.json();
                const searchItems = data.SearchHints || [];
                console.log('RatingsPlugin: Found', searchItems.length, 'items');

                // Always remove old results container first
                const oldContainer = document.getElementById('fullLibrarySearchResults');
                if (oldContainer) {
                    oldContainer.remove();
                }

                // Create fresh results container - insert directly into body to avoid Jellyfin page transition issues
                const resultsContainer = document.createElement('div');
                resultsContainer.id = 'fullLibrarySearchResults';
                resultsContainer.style.cssText = `
                    position: fixed;
                    top: 60px;
                    left: 0;
                    right: 0;
                    bottom: 0;
                    padding: 20px;
                    overflow-y: auto;
                    display: block !important;
                    visibility: visible !important;
                    opacity: 1 !important;
                    z-index: 9999;
                    background-color: #0b0b0b;
                `;

                // Insert directly into body to avoid being affected by page transitions
                document.body.appendChild(resultsContainer);
                console.log('RatingsPlugin: Results container appended to body');

                // Add MutationObserver to detect if something tries to modify the container
                const observer = new MutationObserver((mutations) => {
                    mutations.forEach((mutation) => {
                        if (mutation.type === 'attributes' && mutation.attributeName === 'style') {
                            console.log('RatingsPlugin: Container style was modified!', resultsContainer.style.cssText);
                        }
                    });
                });
                observer.observe(resultsContainer, { attributes: true, attributeFilter: ['style'] });

                // Hide original homepage content
                const homeSections = document.querySelectorAll('.verticalSection, .section, .homePageSection');
                console.log('RatingsPlugin: Found', homeSections.length, 'homepage sections to hide');
                homeSections.forEach(section => {
                    section.style.display = 'none';
                });

                // Build results HTML
                let html = `
                    <h2 style="color: #fff; margin-bottom: 20px;">Search Results for "${query}" (${searchItems.length} found)</h2>
                    <div class="itemsContainer vertical-wrap" style="display: flex; flex-wrap: wrap; gap: 20px;">
                `;
                console.log('RatingsPlugin: Building HTML for', searchItems.length, 'items');

                searchItems.forEach(item => {
                    const itemId = item.Id;
                    const itemName = item.Name || 'Unknown';
                    const itemType = item.Type;

                    // Build image URL - use Primary image type
                    const imageSrc = `${baseUrl}/Items/${itemId}/Images/Primary?quality=90&maxWidth=400`;

                    html += `
                        <a href="#!/details?id=${itemId}" class="card portraitCard" style="width: 200px;">
                            <div class="cardBox visualCardBox">
                                <div class="cardScalable">
                                    <div class="cardPadder-portrait"></div>
                                    <div class="cardContent">
                                        <div class="cardImageContainer coveredImage">
                                            <div class="cardPadder-portrait"></div>
                                            <div class="cardImageContainerInner">
                                                <img src="${imageSrc}" class="cardImage itemAction" alt="${itemName}" loading="lazy"/>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="cardFooter">
                                    <div class="cardText cardText-first">${itemName}</div>
                                    <div class="cardText cardText-secondary">${itemType}</div>
                                </div>
                            </div>
                        </a>
                    `;
                });

                html += '</div>';
                resultsContainer.innerHTML = html;
                console.log('RatingsPlugin: HTML set immediately, resultsContainer visible:', resultsContainer.offsetHeight > 0);
                console.log('RatingsPlugin: resultsContainer HTML length:', resultsContainer.innerHTML.length);

                // Check visibility after a delay to detect if Jellyfin page rendering affects it
                setTimeout(() => {
                    console.log('RatingsPlugin: After 100ms delay, resultsContainer visible:', resultsContainer.offsetHeight > 0);
                    console.log('RatingsPlugin: After 100ms delay, display:', window.getComputedStyle(resultsContainer).display);
                    console.log('RatingsPlugin: After 100ms delay, visibility:', window.getComputedStyle(resultsContainer).visibility);
                    console.log('RatingsPlugin: After 100ms delay, opacity:', window.getComputedStyle(resultsContainer).opacity);
                }, 100);

            } catch (error) {
                console.error('RatingsPlugin: Full library search error:', error);
            }
        },

        /**
         * Clear full library search results and restore homepage
         */
        clearFullLibrarySearch: function () {
            const resultsContainer = document.getElementById('fullLibrarySearchResults');
            if (resultsContainer) {
                resultsContainer.remove();
            }

            // Show original homepage content
            const homeSections = document.querySelectorAll('.verticalSection, .section, .homePageSection');
            homeSections.forEach(section => {
                section.style.display = '';
            });
        },

        /**
         * Filter current page content based on search query
         */
        filterCurrentPageContent: function (query) {
            try {
                const lowerQuery = query.toLowerCase();

                // Find all media cards on the page - comprehensive selector
                const cards = document.querySelectorAll([
                    '.card',
                    '.itemTile',
                    '.portraitCard',
                    '.squareCard',
                    '.overflowPortraitCard',
                    '.overflowSquareCard',
                    '.overflowBackdropCard',
                    '[data-type="Program"]',
                    '[data-type="Movie"]',
                    '[data-type="Series"]',
                    '[data-type="Episode"]',
                    '.listItem',
                    '.netflix-card'  // Netflix view cards
                ].join(', '));

                let matchCount = 0;
                let hideCount = 0;

                cards.forEach(card => {
                    try {
                        // Get card title from various possible locations
                        let title = '';

                        // Try to find title in card text
                        const cardText = card.querySelector('.cardText, .cardTextCentered, .cardText-first, .itemName, .listItemBodyText, .netflix-card-title');
                        if (cardText) {
                            title = cardText.textContent || cardText.innerText || '';
                        }

                        // Also check data attributes
                        if (!title) {
                            title = card.getAttribute('data-title') ||
                                   card.getAttribute('data-name') ||
                                   card.getAttribute('aria-label') ||
                                   card.getAttribute('data-playername') || '';
                        }

                        // Check if link has title
                        if (!title) {
                            const link = card.querySelector('a');
                            if (link) {
                                title = link.getAttribute('title') ||
                                       link.getAttribute('aria-label') ||
                                       link.textContent || '';
                            }
                        }

                        // Check image alt text
                        if (!title) {
                            const img = card.querySelector('img');
                            if (img) {
                                title = img.getAttribute('alt') || '';
                            }
                        }

                        // Filter based on title
                        if (lowerQuery === '' || title.toLowerCase().includes(lowerQuery)) {
                            // Show card
                            card.style.display = '';
                            card.style.opacity = '1';
                            card.style.visibility = 'visible';
                            matchCount++;

                            // Also show parent containers
                            let parent = card.parentElement;
                            while (parent && parent !== document.body) {
                                if (parent.classList.contains('itemsContainer') ||
                                    parent.classList.contains('scrollSlider') ||
                                    parent.classList.contains('itemsWrapper')) {
                                    parent.style.display = '';
                                }
                                parent = parent.parentElement;
                            }
                        } else {
                            // Hide card
                            card.style.display = 'none';
                            card.style.opacity = '0';
                            card.style.visibility = 'hidden';
                            hideCount++;
                        }
                    } catch (err) {
                        // Skip this card if error
                    }
                });

                // Handle sections/rows - hide empty ones
                const sections = document.querySelectorAll('.verticalSection, .section, .homePageSection, .padded-top, .padded-bottom, .netflix-genre-row');
                sections.forEach(section => {
                    try {
                        const visibleCards = section.querySelectorAll('.card:not([style*="display: none"]):not([style*="display:none"]), .itemTile:not([style*="display: none"]):not([style*="display:none"]), .netflix-card:not([style*="display: none"]):not([style*="display:none"])');
                        if (lowerQuery !== '' && visibleCards.length === 0) {
                            section.style.display = 'none';
                        } else {
                            section.style.display = '';
                        }
                    } catch (err) {
                        // Skip this section if error
                    }
                });

            } catch (err) {
                // Silently fail
            }
        },

        /**
         * Load appropriate interface based on user role
         */
        loadRequestInterface: function () {
            const self = this;
            try {
                // Check if user is admin
                this.checkIfAdmin().then(isAdmin => {
                    if (isAdmin) {
                        self.loadAdminInterface();
                    } else {
                        self.loadUserInterface();
                    }
                }).catch(err => {
                    console.error('Error checking admin status:', err);
                    // Default to user interface on error
                    self.loadUserInterface();
                });
            } catch (err) {
                console.error('Error loading request interface:', err);
            }
        },

        /**
         * Check if current user is admin
         */
        checkIfAdmin: function () {
            return new Promise((resolve, reject) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const userId = ApiClient.getCurrentUserId();
                    const baseUrl = ApiClient.serverAddress();
                    const accessToken = ApiClient.accessToken();

                    if (!userId) {
                        resolve(false);
                        return;
                    }

                    const url = `${baseUrl}/Users/${userId}`;
                    const deviceId = ApiClient.deviceId();
                    const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Emby-Authorization': authHeader
                        }
                    })
                    .then(response => response.json())
                    .then(user => {
                        resolve(user.Policy && user.Policy.IsAdministrator === true);
                    })
                    .catch(err => {
                        console.error('Error fetching user info:', err);
                        resolve(false);
                    });
                } catch (err) {
                    console.error('Error in checkIfAdmin:', err);
                    resolve(false);
                }
            });
        },

        /**
         * Load user interface for making requests
         */
        loadUserInterface: function () {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            // Clear viewed requests when user opens modal
            this.markDoneRequestsAsViewed();

            modalTitle.textContent = 'Request Media';
            modalBody.innerHTML = `
                <div class="request-description">
                    <strong>📬 Request Your Favorite Media!</strong><br>
                    Use this form to request movies or TV series that you'd like to watch. The admin will review your request and add it to the library as soon as possible. You can track the status of all your requests below.
                </div>
                <div class="request-input-group">
                    <label for="requestMediaTitle">Media Title *</label>
                    <input type="text" id="requestMediaTitle" placeholder="e.g., Breaking Bad, The Godfather" required />
                </div>
                <div class="request-input-group">
                    <label for="requestMediaType">Type *</label>
                    <select id="requestMediaType" required>
                        <option value="">-- Select Type --</option>
                        <option value="Movie">Movie</option>
                        <option value="TV Series">TV Series</option>
                        <option value="Anime">Anime</option>
                        <option value="Documentary">Documentary</option>
                        <option value="Other">Other</option>
                    </select>
                </div>
                <div class="request-input-group">
                    <label for="requestMediaNotes">Additional Notes</label>
                    <textarea id="requestMediaNotes" placeholder="Season number, year, specific details, etc."></textarea>
                </div>
                <button class="request-submit-btn" id="submitRequestBtn">Submit Request</button>
                <div class="user-requests-title">Your Requests</div>
                <div id="userRequestsList"><p style="text-align: center; color: #999;">Loading your requests...</p></div>
            `;

            // Attach submit handler
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.addEventListener('click', () => {
                    this.submitMediaRequest();
                });
            }

            // Load user's own requests
            this.loadUserRequests();
        },

        /**
         * Load user's own requests
         */
        loadUserRequests: function () {
            const self = this;
            const listContainer = document.getElementById('userRequestsList');

            if (!listContainer) return;

            listContainer.innerHTML = '<p style="text-align: center; color: #999;">Loading your requests...</p>';

            this.fetchAllRequests().then(requests => {
                // Filter to only current user's requests
                const userId = ApiClient.getCurrentUserId();
                const userRequests = requests.filter(r => r.UserId === userId);

                if (userRequests.length === 0) {
                    listContainer.innerHTML = '<p style="text-align: center; color: #999;">You haven\'t requested any media yet</p>';
                    return;
                }

                let html = '<ul class="user-request-list">';
                userRequests.forEach(request => {
                    // Format timestamps
                    const createdAt = request.CreatedAt ? this.formatDateTime(request.CreatedAt) : '';
                    const completedAt = request.CompletedAt ? this.formatDateTime(request.CompletedAt) : null;
                    const hasLink = request.MediaLink && request.Status === 'done';

                    html += `
                        <li class="user-request-item">
                            <div class="user-request-info">
                                <div class="user-request-item-title">${this.escapeHtml(request.Title)}</div>
                                <div class="user-request-item-type">${request.Type ? this.escapeHtml(request.Type) : 'Not specified'}</div>
                                <div class="user-request-time">📅 ${createdAt}${completedAt ? ` • ✅ ${completedAt}` : ''}</div>
                                ${hasLink ? `<a href="${this.escapeHtml(request.MediaLink)}" class="request-media-link" target="_blank">🎬 Watch Now</a>` : ''}
                            </div>
                            <span class="user-request-status ${request.Status}">${request.Status.toUpperCase()}</span>
                        </li>
                    `;
                });
                html += '</ul>';
                listContainer.innerHTML = html;
            }).catch(err => {
                console.error('Error loading user requests:', err);
                listContainer.innerHTML = '<p style="text-align: center; color: #f44336;">Error loading your requests</p>';
            });
        },

        /**
         * Load admin interface for managing requests
         */
        loadAdminInterface: function () {
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            modalTitle.textContent = 'Manage Media Requests';
            modalBody.innerHTML = '<p style="text-align: center; color: #999;">Loading requests...</p>';

            // Fetch all requests
            this.fetchAllRequests().then(requests => {
                if (requests.length === 0) {
                    modalBody.innerHTML = '<div class="admin-request-empty">No media requests yet</div>';
                    return;
                }

                let html = '<ul class="admin-request-list">';
                requests.forEach(request => {
                    const details = [];
                    if (request.Type) details.push(request.Type);
                    if (request.Notes) details.push(request.Notes);
                    const detailsText = details.join(' • ');

                    // Format timestamps
                    const createdAt = request.CreatedAt ? this.formatDateTime(request.CreatedAt) : 'Unknown';
                    const completedAt = request.CompletedAt ? this.formatDateTime(request.CompletedAt) : null;
                    const hasLink = request.MediaLink && request.Status === 'done';

                    html += `
                        <li class="admin-request-item" data-request-id="${request.Id}">
                            <div class="admin-request-title" title="${this.escapeHtml(request.Title)}">${this.escapeHtml(request.Title)}</div>
                            <div class="admin-request-user" title="${this.escapeHtml(request.Username)}">${this.escapeHtml(request.Username)}</div>
                            <div class="admin-request-details" title="${this.escapeHtml(detailsText)}">${this.escapeHtml(detailsText) || 'No details'}</div>
                            <div class="admin-request-time">
                                <span>📅 ${createdAt}</span>
                                ${completedAt ? `<span>✅ ${completedAt}</span>` : ''}
                                ${hasLink ? `<a href="${this.escapeHtml(request.MediaLink)}" class="request-media-link" target="_blank">🎬 Watch Now</a>` : ''}
                            </div>
                            <span class="admin-request-status-badge ${request.Status}">${request.Status.toUpperCase()}</span>
                            <div class="admin-request-actions">
                                <button class="admin-status-btn pending" data-status="pending" data-request-id="${request.Id}">Pending</button>
                                <button class="admin-status-btn processing" data-status="processing" data-request-id="${request.Id}">Processing</button>
                                <button class="admin-status-btn done" data-status="done" data-request-id="${request.Id}">Done</button>
                                <button class="admin-delete-btn" data-request-id="${request.Id}">🗑️</button>
                            </div>
                            <select class="admin-status-select" data-request-id="${request.Id}">
                                <option value="pending" ${request.Status === 'pending' ? 'selected' : ''}>Pending</option>
                                <option value="processing" ${request.Status === 'processing' ? 'selected' : ''}>Processing</option>
                                <option value="done" ${request.Status === 'done' ? 'selected' : ''}>Done</option>
                            </select>
                            <input type="text" class="admin-link-input" data-request-id="${request.Id}" placeholder="Media link (paste URL when done)" value="${this.escapeHtml(request.MediaLink || '')}">
                            <button class="admin-delete-btn mobile-delete" data-request-id="${request.Id}">🗑️ Delete</button>
                        </li>
                    `;
                });
                html += '</ul>';
                modalBody.innerHTML = html;

                // Attach status change handlers for buttons (desktop)
                const statusBtns = modalBody.querySelectorAll('.admin-status-btn');
                statusBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.getAttribute('data-status');
                        // Get the media link if marking as done
                        const linkInput = modalBody.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        this.updateRequestStatus(requestId, newStatus, mediaLink);
                    });
                });

                // Attach status change handlers for dropdown (mobile)
                const statusSelects = modalBody.querySelectorAll('.admin-status-select');
                statusSelects.forEach(select => {
                    select.addEventListener('change', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.value;
                        // Get the media link if marking as done
                        const linkInput = modalBody.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        this.updateRequestStatus(requestId, newStatus, mediaLink);
                    });
                });

                // Attach delete handlers
                const deleteBtns = modalBody.querySelectorAll('.admin-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm('Are you sure you want to delete this request? This cannot be undone.')) {
                            this.deleteRequest(requestId);
                        }
                    });
                });
            }).catch(err => {
                console.error('Error loading requests:', err);
                modalBody.innerHTML = '<div class="admin-request-empty">Error loading requests</div>';
            });
        },

        /**
         * Submit a new media request
         */
        submitMediaRequest: function () {
            const self = this;
            try {
                const title = document.getElementById('requestMediaTitle').value.trim();
                const type = document.getElementById('requestMediaType').value.trim();
                const notes = document.getElementById('requestMediaNotes').value.trim();

                if (!title) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please enter a media title');
                        });
                    }
                    return;
                }

                if (!type) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please select a media type');
                        });
                    }
                    return;
                }

                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const url = `${baseUrl}/Ratings/Requests`;

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                const requestData = {
                    Title: title,
                    Type: type,
                    Notes: notes
                };

                fetch(url, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    },
                    body: JSON.stringify(requestData)
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to submit request');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Request submitted successfully!');
                        });
                    }

                    // Clear form
                    document.getElementById('requestMediaTitle').value = '';
                    document.getElementById('requestMediaType').value = '';
                    document.getElementById('requestMediaNotes').value = '';

                    // Reload user's request list to show the new request
                    self.loadUserRequests();
                })
                .catch(err => {
                    console.error('Error submitting request:', err);
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error submitting request');
                        });
                    }
                });
            } catch (err) {
                console.error('Error in submitMediaRequest:', err);
            }
        },

        /**
         * Fetch all media requests (admin only)
         */
        fetchAllRequests: function () {
            return new Promise((resolve, reject) => {
                try {
                    const baseUrl = ApiClient.serverAddress();
                    const accessToken = ApiClient.accessToken();
                    const deviceId = ApiClient.deviceId();
                    const url = `${baseUrl}/Ratings/Requests`;

                    const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Emby-Authorization': authHeader
                        }
                    })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error('Failed to fetch requests');
                        }
                        return response.json();
                    })
                    .then(requests => {
                        resolve(requests || []);
                    })
                    .catch(err => {
                        console.error('Error fetching requests:', err);
                        reject(err);
                    });
                } catch (err) {
                    console.error('Error in fetchAllRequests:', err);
                    reject(err);
                }
            });
        },

        /**
         * Format date time for display
         */
        formatDateTime: function (dateString) {
            try {
                const date = new Date(dateString);
                return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            } catch (e) {
                return dateString;
            }
        },

        /**
         * Delete a media request (admin only)
         */
        deleteRequest: function (requestId) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const url = `${baseUrl}/Ratings/Requests/${requestId}`;

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to delete request');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Request deleted');
                        });
                    }

                    // Reload the admin interface
                    self.loadAdminInterface();

                    // Update badge
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                })
                .catch(err => {
                    console.error('Error deleting request:', err);
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error deleting request');
                        });
                    }
                });
            } catch (err) {
                console.error('Error in deleteRequest:', err);
            }
        },

        /**
         * Update request status (admin only)
         */
        updateRequestStatus: function (requestId, newStatus, mediaLink) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                let url = `${baseUrl}/Ratings/Requests/${requestId}/Status?status=${newStatus}`;

                // Add mediaLink if provided and status is done
                if (mediaLink && newStatus === 'done') {
                    url += `&mediaLink=${encodeURIComponent(mediaLink)}`;
                }

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to update status');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Status updated to: ' + newStatus);
                        });
                    }

                    // Reload the admin interface
                    self.loadAdminInterface();

                    // Update badge to reflect new counts
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                })
                .catch(err => {
                    console.error('Error updating status:', err);
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error updating status');
                        });
                    }
                });
            } catch (err) {
                console.error('Error in updateRequestStatus:', err);
            }
        },

        /**
         * Update notification badge on button
         */
        updateRequestBadge: function (btn) {
            const self = this;
            try {
                this.fetchAllRequests().then(requests => {
                    // Check if user is admin
                    this.checkIfAdmin().then(isAdmin => {
                        let count = 0;

                        if (isAdmin) {
                            // For admin: show count of pending requests
                            count = requests.filter(r => r.Status === 'pending').length;
                        } else {
                            // For users: show count of completed (done) requests they haven't seen yet
                            const userId = ApiClient.getCurrentUserId();
                            const userRequests = requests.filter(r => r.UserId === userId);
                            const doneRequests = userRequests.filter(r => r.Status === 'done');

                            // Get viewed request IDs from localStorage
                            const viewedRequests = self.getViewedRequestIds();

                            // Count only done requests that haven't been viewed
                            count = doneRequests.filter(r => !viewedRequests.includes(r.Id)).length;
                        }

                        // Remove existing badge
                        const existingBadge = btn.querySelector('.request-badge');
                        if (existingBadge) {
                            existingBadge.remove();
                        }

                        // Add badge if count > 0
                        if (count > 0) {
                            const badge = document.createElement('span');
                            badge.className = 'request-badge';
                            badge.textContent = count;
                            btn.appendChild(badge);
                        }
                    }).catch(err => {
                        console.error('Error checking admin status for badge:', err);
                    });
                }).catch(err => {
                    console.error('Error updating request badge:', err);
                });
            } catch (err) {
                console.error('Error in updateRequestBadge:', err);
            }
        },

        /**
         * Get list of viewed request IDs from localStorage
         */
        getViewedRequestIds: function () {
            try {
                const stored = localStorage.getItem('ratingsPlugin_viewedRequests');
                return stored ? JSON.parse(stored) : [];
            } catch (err) {
                console.error('Error reading viewed requests:', err);
                return [];
            }
        },

        /**
         * Mark all current done requests as viewed
         */
        markDoneRequestsAsViewed: function () {
            const self = this;
            try {
                this.fetchAllRequests().then(requests => {
                    const userId = ApiClient.getCurrentUserId();
                    const userRequests = requests.filter(r => r.UserId === userId);
                    const doneRequests = userRequests.filter(r => r.Status === 'done');

                    // Get current viewed list
                    const viewedIds = self.getViewedRequestIds();

                    // Add all done request IDs
                    doneRequests.forEach(request => {
                        if (!viewedIds.includes(request.Id)) {
                            viewedIds.push(request.Id);
                        }
                    });

                    // Save back to localStorage
                    localStorage.setItem('ratingsPlugin_viewedRequests', JSON.stringify(viewedIds));

                    // Update badge immediately to reflect changes
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                }).catch(err => {
                    console.error('Error marking requests as viewed:', err);
                });
            } catch (err) {
                console.error('Error in markDoneRequestsAsViewed:', err);
            }
        },

        // ============================================
        // NEW MEDIA NOTIFICATIONS
        // ============================================

        /**
         * Notification state
         */
        notificationsEnabled: false,
        lastNotificationCheck: null,
        notificationPollingInterval: null,
        shownNotificationIds: [],

        /**
         * Initialize notifications system
         */
        initNotifications: function () {
            const self = this;

            // Check if notifications are enabled in config
            this.checkNotificationsEnabled().then(enabled => {
                self.notificationsEnabled = enabled;
                console.log('RatingsPlugin: Notifications enabled:', enabled);
                if (enabled) {
                    // Create notification container
                    self.createNotificationContainer();

                    // Initialize the last check time to 5 minutes ago so we catch recent notifications
                    self.lastNotificationCheck = new Date(Date.now() - 5 * 60 * 1000).toISOString();
                    console.log('RatingsPlugin: Initial lastNotificationCheck:', self.lastNotificationCheck);

                    // Start polling for notifications
                    self.startNotificationPolling();

                    // Initialize admin test button
                    self.initTestNotificationButton();
                }
            });
        },

        /**
         * Check if notifications are enabled
         */
        checkNotificationsEnabled: function () {
            return new Promise((resolve) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, {
                        method: 'GET',
                        credentials: 'include'
                    })
                        .then(response => response.json())
                        .then(config => {
                            resolve(config.EnableNewMediaNotifications === true);
                        })
                        .catch(() => {
                            resolve(false);
                        });
                } catch (err) {
                    resolve(false);
                }
            });
        },

        /**
         * Create notification container
         */
        createNotificationContainer: function () {
            if (document.getElementById('ratingsNotificationContainer')) {
                return;
            }

            const container = document.createElement('div');
            container.id = 'ratingsNotificationContainer';
            container.className = 'ratings-notification-container';
            document.body.appendChild(container);
        },

        /**
         * Start polling for new notifications
         */
        startNotificationPolling: function () {
            const self = this;

            // Poll every 10 seconds
            this.notificationPollingInterval = setInterval(() => {
                self.checkForNewNotifications();
            }, 10000);

            // Also check immediately
            this.checkForNewNotifications();
        },

        /**
         * Check for new notifications from server
         */
        checkForNewNotifications: function () {
            const self = this;

            if (!window.ApiClient) {
                console.log('RatingsPlugin: No ApiClient available for notifications');
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            const since = this.lastNotificationCheck || new Date(Date.now() - 5 * 60 * 1000).toISOString();

            console.log('RatingsPlugin: Checking notifications since:', since);

            fetch(`${baseUrl}/Ratings/Notifications?since=${encodeURIComponent(since)}`, {
                method: 'GET',
                credentials: 'include'
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('HTTP ' + response.status);
                    }
                    return response.json();
                })
                .then(notifications => {
                    console.log('RatingsPlugin: Received notifications:', notifications ? notifications.length : 0, notifications);

                    if (notifications && notifications.length > 0) {
                        notifications.forEach(notification => {
                            // Don't show duplicates
                            if (!self.shownNotificationIds.includes(notification.Id)) {
                                console.log('RatingsPlugin: Showing notification:', notification.Title || notification.Message);
                                self.shownNotificationIds.push(notification.Id);
                                self.showNotification(notification);
                            } else {
                                console.log('RatingsPlugin: Skipping duplicate notification:', notification.Id);
                            }
                        });
                    }

                    // Update last check time
                    self.lastNotificationCheck = new Date().toISOString();
                })
                .catch(err => {
                    console.error('RatingsPlugin: Error checking for notifications:', err);
                });
        },

        /**
         * Show a notification
         */
        showNotification: function (notification) {
            const container = document.getElementById('ratingsNotificationContainer');
            if (!container) return;

            const baseUrl = window.ApiClient ? ApiClient.serverAddress() : '';

            // Create notification element
            const notifEl = document.createElement('div');
            notifEl.className = 'ratings-notification' + (notification.IsTest ? ' test-notification' : '');
            notifEl.setAttribute('data-notification-id', notification.Id);

            // Build image URL
            let imageHtml = '';
            if (notification.ImageUrl && !notification.IsTest) {
                imageHtml = `<img class="ratings-notification-image" src="${baseUrl}${notification.ImageUrl}" alt="" onerror="this.style.display='none'">`;
            }

            // Build content based on notification type
            let contentHtml = '';
            if (notification.IsTest) {
                contentHtml = `
                    <div class="ratings-notification-content">
                        <div class="ratings-notification-header">
                            <span class="ratings-notification-icon">🔔</span>
                            <span class="ratings-notification-label">Test Notification</span>
                        </div>
                        <div class="ratings-notification-message">${this.escapeHtml(notification.Message || 'Test notification')}</div>
                    </div>
                `;
            } else {
                const yearText = notification.Year ? ` (${notification.Year})` : '';
                let typeLabel, titleText, icon;

                if (notification.MediaType === 'Movie') {
                    typeLabel = 'New Movie Available';
                    titleText = this.escapeHtml(notification.Title) + yearText;
                    icon = '🎬';
                } else if (notification.MediaType === 'Episode') {
                    typeLabel = 'New Episode Available';
                    const seasonNum = notification.SeasonNumber ? notification.SeasonNumber.toString().padStart(2, '0') : '00';
                    const episodeNum = notification.EpisodeNumber ? notification.EpisodeNumber.toString().padStart(2, '0') : '00';
                    const seriesName = notification.SeriesName ? this.escapeHtml(notification.SeriesName) : '';
                    titleText = seriesName ? `${seriesName} S${seasonNum}E${episodeNum}` : this.escapeHtml(notification.Title);
                    icon = '📺';
                } else {
                    typeLabel = 'New Series Available';
                    titleText = this.escapeHtml(notification.Title) + yearText;
                    icon = '📺';
                }

                contentHtml = `
                    <div class="ratings-notification-content">
                        <div class="ratings-notification-header">
                            <span class="ratings-notification-icon">${icon}</span>
                            <span class="ratings-notification-label">${typeLabel}</span>
                        </div>
                        <div class="ratings-notification-title">${titleText}</div>
                    </div>
                `;
            }

            notifEl.innerHTML = `
                ${imageHtml}
                ${contentHtml}
                <button class="ratings-notification-close" title="Dismiss">&times;</button>
            `;

            // Add close button handler
            const closeBtn = notifEl.querySelector('.ratings-notification-close');
            closeBtn.addEventListener('click', () => {
                this.hideNotification(notifEl);
            });

            // Add click handler to navigate to item (if not a test)
            if (!notification.IsTest && notification.ItemId && notification.ItemId !== '00000000-0000-0000-0000-000000000000') {
                notifEl.style.cursor = 'pointer';
                notifEl.addEventListener('click', (e) => {
                    if (e.target !== closeBtn && !closeBtn.contains(e.target)) {
                        window.location.hash = `#/details?id=${notification.ItemId}`;
                        this.hideNotification(notifEl);
                    }
                });
            }

            // Add to container
            container.appendChild(notifEl);

            // Auto-hide after 8 seconds
            setTimeout(() => {
                this.hideNotification(notifEl);
            }, 8000);
        },

        /**
         * Hide a notification with animation
         */
        hideNotification: function (notifEl) {
            if (!notifEl || !notifEl.parentNode) return;

            notifEl.classList.add('hiding');
            setTimeout(() => {
                if (notifEl.parentNode) {
                    notifEl.remove();
                }
            }, 300);
        },

        /**
         * Initialize admin test notification button
         */
        initTestNotificationButton: function () {
            const self = this;

            // Don't show on login page
            if (this.isOnLoginPage()) return;

            // Check if user is admin first
            this.checkIfAdmin().then(isAdmin => {
                if (!isAdmin) {
                    // Remove button if exists and user is not admin
                    const existingBtn = document.getElementById('testNotificationBtn');
                    if (existingBtn) {
                        existingBtn.remove();
                    }
                    return;
                }

                // Don't create if already exists
                if (document.getElementById('testNotificationBtn')) return;

                const header = document.querySelector('.skinHeader') || document.querySelector('header');
                if (!header) return;

                const btn = document.createElement('button');
                btn.id = 'testNotificationBtn';
                btn.innerHTML = '🔔 Test';
                btn.title = 'Send a test notification to all users';

                btn.addEventListener('click', () => {
                    self.sendTestNotification();
                });

                header.appendChild(btn);

                // Hide on login page - check periodically
                setInterval(() => {
                    try {
                        const testBtn = document.getElementById('testNotificationBtn');
                        if (!testBtn) return;

                        const isLoginPage = self.isOnLoginPage();
                        const videoPlayer = document.querySelector('.videoPlayerContainer');
                        const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');

                        if (isLoginPage || isVideoPlaying) {
                            testBtn.style.display = 'none';
                        } else {
                            testBtn.style.display = '';
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 500);
            });
        },

        /**
         * Send a test notification
         */
        sendTestNotification: function () {
            const self = this;

            if (!window.ApiClient) return;

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();

            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(`${baseUrl}/Ratings/Notifications/Test`, {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (response.ok) {
                        if (window.require) {
                            require(['toast'], function (toast) {
                                toast('Test notification sent!');
                            });
                        }
                        // Check for notifications immediately
                        setTimeout(() => {
                            self.checkForNewNotifications();
                        }, 500);
                    } else {
                        throw new Error('Failed to send test notification');
                    }
                })
                .catch(err => {
                    console.error('Error sending test notification:', err);
                    if (window.require) {
                        require(['toast'], function (toast) {
                            toast('Error sending test notification');
                        });
                    }
                });
        },

        /**
         * Escape HTML to prevent XSS
         */
        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        /**
         * Netflix View Configuration
         */
        netflixViewEnabled: false,
        netflixViewInitialized: false,

        /**
         * Initialize Netflix-style view
         */
        initNetflixView: function () {
            const self = this;

            // Check if feature is enabled via API
            this.checkNetflixViewEnabled().then(enabled => {
                self.netflixViewEnabled = enabled;
                if (enabled) {
                    self.observeLibraryPages();
                }
            });
        },

        /**
         * Check if Netflix view is enabled in plugin config
         */
        checkNetflixViewEnabled: function () {
            return new Promise((resolve) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const baseUrl = ApiClient.serverAddress();
                    const url = `${baseUrl}/Ratings/Config`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include'
                    })
                    .then(response => response.json())
                    .then(config => {
                        resolve(config.EnableNetflixView === true);
                    })
                    .catch(() => {
                        resolve(false);
                    });
                } catch (err) {
                    resolve(false);
                }
            });
        },

        /**
         * Check if current page should show Netflix view
         */
        isNetflixViewPage: function () {
            const hash = window.location.hash;
            const isMoviesPage = hash.includes('#/movies') || hash.includes('collectionType=movies');
            const isTVPage = hash.includes('#/tv') || hash.includes('collectionType=tvshows');
            const hasTopParentId = hash.includes('topParentId=');
            return hasTopParentId && (isMoviesPage || isTVPage);
        },

        /**
         * Observe library pages for Netflix view
         */
        observeLibraryPages: function () {
            const self = this;
            let lastUrl = '';
            let transformTimeout = null;
            let hideStyleElement = null;

            // Inject CSS to instantly hide default content on Netflix pages
            const injectHideStyles = () => {
                if (self.isNetflixViewPage() && !hideStyleElement) {
                    hideStyleElement = document.createElement('style');
                    hideStyleElement.id = 'netflix-view-hide-default';
                    hideStyleElement.textContent = `
                        .itemsContainer:not(.netflix-view-active),
                        .vertical-list:not(.netflix-view-active) {
                            opacity: 0 !important;
                            pointer-events: none !important;
                        }
                    `;
                    document.head.appendChild(hideStyleElement);
                }
            };

            // Remove hide styles when not on Netflix page
            const removeHideStyles = () => {
                if (hideStyleElement) {
                    hideStyleElement.remove();
                    hideStyleElement = null;
                }
            };

            const checkLibraryPage = () => {
                const url = window.location.href;
                const hash = window.location.hash;
                const shouldTransform = self.isNetflixViewPage();

                // Clean up Netflix view when navigating away
                if (!shouldTransform) {
                    removeHideStyles();
                    const existingNetflix = document.querySelector('.netflix-view-container');
                    if (existingNetflix) {
                        // Show original content again
                        const itemsContainer = document.querySelector('.itemsContainer');
                        if (itemsContainer) {
                            itemsContainer.style.display = '';
                        }
                        existingNetflix.remove();

                        // Reset header and main content styles
                        const skinHeader = document.querySelector('.skinHeader');
                        if (skinHeader) {
                            skinHeader.style.cssText = '';
                        }
                        const mainAnimatedPages = document.querySelector('.mainAnimatedPages, .view');
                        if (mainAnimatedPages) {
                            mainAnimatedPages.style.cssText = '';
                        }

                        // Restore body and html scrolling
                        document.body.style.overflow = '';
                        document.documentElement.style.overflow = '';
                    }
                    lastUrl = url;
                    return;
                }

                // Don't re-process same URL
                if (url === lastUrl && document.querySelector('.netflix-view-container')) {
                    return;
                }
                lastUrl = url;
                // Clear any pending transform
                if (transformTimeout) {
                    clearTimeout(transformTimeout);
                }

                // Inject hide styles immediately
                injectHideStyles();

                // Remove old Netflix view if exists (for re-navigation)
                const existingNetflix = document.querySelector('.netflix-view-container');
                if (existingNetflix) {
                    existingNetflix.remove();
                }

                // Use MutationObserver to detect when itemsContainer appears
                const tryTransform = () => {
                    const itemsContainer = document.querySelector('.itemsContainer') ||
                                           document.querySelector('.vertical-list');

                    if (itemsContainer) {                        removeHideStyles();
                        self.transformToNetflixView();
                        return true;
                    }
                    return false;
                };

                // Try immediately
                if (tryTransform()) return;

                // Watch for DOM changes
                const observer = new MutationObserver((mutations, obs) => {
                    if (tryTransform()) {
                        obs.disconnect();
                    }
                });

                observer.observe(document.body, {
                    childList: true,
                    subtree: true
                });

                // Fallback timeout
                transformTimeout = setTimeout(() => {
                    observer.disconnect();
                    removeHideStyles();
                    if (!document.querySelector('.netflix-view-container')) {
                        self.transformToNetflixView();
                    }
                }, 3000);
            };

            // Listen for hash changes (SPA navigation)
            window.addEventListener('hashchange', () => {                // Small delay to let Jellyfin start loading new page
                setTimeout(checkLibraryPage, 100);
            });

            // Also watch for popstate (back/forward navigation)
            window.addEventListener('popstate', () => {                setTimeout(checkLibraryPage, 100);
            });

            // Periodic check as fallback (less frequent)
            setInterval(checkLibraryPage, 2000);

            // Initial check
            setTimeout(checkLibraryPage, 500);
        },

        /**
         * Transform library page to Netflix-style view
         */
        transformToNetflixView: function () {
            const self = this;
            // Don't transform if already done
            if (document.querySelector('.netflix-view-container')) {                return;
            }

            // Find the main content area - try multiple selectors
            // Jellyfin uses different containers depending on navigation method
            let itemsContainer = document.querySelector('.itemsContainer');
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.vertical-list');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.view-inner');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('[data-role="content"] .padded-left');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.libraryPage');
            }
            if (!itemsContainer) {
                // Try finding any scrollable content area
                itemsContainer = document.querySelector('.page:not(.hide) .content-primary');
            }
            if (!itemsContainer) {                // Retry after a delay - content may still be loading
                setTimeout(() => {
                    if (!document.querySelector('.netflix-view-container')) {
                        self.transformToNetflixView();
                    }
                }, 500);
                return;
            }

            // Get parent library ID from URL
            const parentId = this.getParentIdFromUrl();
            if (!parentId) {                return;
            }
            // Fix the header to stay at top when Netflix view is active
            const skinHeader = document.querySelector('.skinHeader');
            if (skinHeader) {
                skinHeader.style.cssText = `
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    z-index: 1000 !important;
                    background: #101010 !important;
                `;
            }

            // Hide body and html scrollbars - only Netflix container should scroll
            document.body.style.overflow = 'hidden';
            document.documentElement.style.overflow = 'hidden';

            // Also ensure main content area doesn't scroll
            const mainAnimatedPages = document.querySelector('.mainAnimatedPages, .view');
            if (mainAnimatedPages) {
                mainAnimatedPages.style.cssText = `
                    margin-top: 56px !important;
                    overflow: hidden !important;
                `;
            }

            // Create Netflix view container as a FIXED overlay
            const netflixContainer = document.createElement('div');
            netflixContainer.className = 'netflix-view-container';
            // Use fixed positioning to overlay below header - this ensures visibility
            netflixContainer.style.cssText = `
                display: block !important;
                visibility: visible !important;
                opacity: 1 !important;
                position: fixed !important;
                top: 56px !important;
                left: 0 !important;
                right: 0 !important;
                bottom: 0 !important;
                width: 100% !important;
                overflow-y: auto !important;
                background: #141414 !important;
                z-index: 100 !important;
            `;
            netflixContainer.innerHTML = '<div class="netflix-loading" style="color: white; text-align: center; padding: 50px; font-size: 18px;">Loading genres...</div>';

            // Insert directly into body as fixed overlay
            document.body.appendChild(netflixContainer);
            // Fetch genres and build view
            this.fetchGenresAndBuildView(parentId, netflixContainer);
        },

        /**
         * Get parent library ID from URL
         */
        getParentIdFromUrl: function () {
            const hash = window.location.hash;
            // Match various GUID formats (with or without dashes)
            const match = hash.match(/[?&]parentId=([a-f0-9-]+)/i) ||
                          hash.match(/[?&]topParentId=([a-f0-9-]+)/i) ||
                          hash.match(/parentId=([a-f0-9-]+)/i) ||
                          hash.match(/topParentId=([a-f0-9-]+)/i);
            return match ? match[1] : null;
        },

        /**
         * Fetch genres and build Netflix-style view
         */
        fetchGenresAndBuildView: function (parentId, container) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const fetchUrl = `${baseUrl}/Items?ParentId=${parentId}&IncludeItemTypes=Movie,Series&Recursive=true&Fields=Genres,PrimaryImageAspectRatio&EnableTotalRecordCount=true&Limit=500`;
            // Get all items to extract genres
            fetch(fetchUrl, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
            .then(response => {                return response.json();
            })
            .then(data => {                const items = data.Items || [];

                // Extract unique genres
                const genreMap = new Map();
                items.forEach(item => {
                    if (item.Genres) {
                        item.Genres.forEach(genre => {
                            if (!genreMap.has(genre)) {
                                genreMap.set(genre, []);
                            }
                            genreMap.get(genre).push(item);
                        });
                    }
                });

                // Sort genres by number of items (most popular first)
                const sortedGenres = Array.from(genreMap.entries())
                    .sort((a, b) => b[1].length - a[1].length)
                    .slice(0, 15); // Limit to top 15 genres
                if (sortedGenres.length === 0) {
                    container.innerHTML = '<div class="netflix-loading" style="color: white; padding: 50px; text-align: center;">No genres found</div>';
                    return;
                }

                // Shuffle function for randomizing items within each genre
                const shuffleArray = (array) => {
                    const shuffled = [...array];
                    for (let i = shuffled.length - 1; i > 0; i--) {
                        const j = Math.floor(Math.random() * (i + 1));
                        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
                    }
                    return shuffled;
                };

                // Build Netflix view HTML with shuffled items per genre
                let html = '';
                sortedGenres.forEach(([genre, genreItems]) => {
                    const shuffledItems = shuffleArray(genreItems);
                    html += self.buildGenreRow(genre, shuffledItems, baseUrl);
                });                container.innerHTML = html;
                // Make sure container is visible
                container.style.display = 'block';

                // Attach scroll button handlers
                self.attachScrollHandlers(container);

                // Apply rating badges to Netflix cards
                self.applyNetflixRatingBadges(container);            })
            .catch(err => {
                console.error('Error fetching items for Netflix view:', err);
                container.innerHTML = '<div class="netflix-loading">Error loading content</div>';
            });
        },

        /**
         * Build HTML for a genre row
         */
        buildGenreRow: function (genre, items, baseUrl) {
            const self = this;

            // Limit to 20 items per row
            const rowItems = items.slice(0, 20);

            let cardsHtml = '';
            rowItems.forEach(item => {
                const imageUrl = item.ImageTags && item.ImageTags.Primary
                    ? `${baseUrl}/Items/${item.Id}/Images/Primary?fillHeight=450&fillWidth=300&quality=96`
                    : `${baseUrl}/Items/${item.Id}/Images/Primary?fillHeight=450&fillWidth=300`;

                const itemUrl = `#!/details?id=${item.Id}`;

                cardsHtml += `
                    <a href="${itemUrl}" class="netflix-card" data-item-id="${item.Id}">
                        <img src="${imageUrl}" alt="${this.escapeHtml(item.Name)}" loading="lazy" onerror="this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 300 450%22><rect fill=%22%232a2a2a%22 width=%22300%22 height=%22450%22/><text x=%22150%22 y=%22225%22 fill=%22%23666%22 text-anchor=%22middle%22 font-size=%2220%22>No Image</text></svg>'">
                        <div class="netflix-card-overlay">
                            <div class="netflix-card-title">${this.escapeHtml(item.Name)}</div>
                            <div class="netflix-card-rating">${item.CommunityRating ? '★ ' + item.CommunityRating.toFixed(1) : ''}</div>
                        </div>
                    </a>
                `;
            });

            return `
                <div class="netflix-genre-row">
                    <div class="netflix-genre-title">${this.escapeHtml(genre)}</div>
                    <div class="netflix-row-wrapper">
                        <button class="netflix-scroll-btn left" aria-label="Scroll left">‹</button>
                        <div class="netflix-row-content">
                            ${cardsHtml}
                        </div>
                        <button class="netflix-scroll-btn right" aria-label="Scroll right">›</button>
                    </div>
                </div>
            `;
        },

        /**
         * Attach scroll button handlers
         */
        attachScrollHandlers: function (container) {
            const rows = container.querySelectorAll('.netflix-row-wrapper');

            rows.forEach(row => {
                const content = row.querySelector('.netflix-row-content');
                const leftBtn = row.querySelector('.netflix-scroll-btn.left');
                const rightBtn = row.querySelector('.netflix-scroll-btn.right');

                if (leftBtn && content) {
                    leftBtn.addEventListener('click', () => {
                        content.scrollBy({ left: -600, behavior: 'smooth' });
                    });
                }

                if (rightBtn && content) {
                    rightBtn.addEventListener('click', () => {
                        content.scrollBy({ left: 600, behavior: 'smooth' });
                    });
                }
            });
        },

        /**
         * Apply rating badges to Netflix cards
         */
        applyNetflixRatingBadges: function (container) {
            const self = this;
            const cards = container.querySelectorAll('.netflix-card[data-item-id]');
            cards.forEach(card => {
                const itemId = card.getAttribute('data-item-id');
                if (!itemId) return;

                // Check cache first
                if (self.ratingsCache[itemId] !== undefined) {
                    if (self.ratingsCache[itemId] !== null) {
                        const stats = self.ratingsCache[itemId];
                        card.classList.add('has-rating');
                        card.setAttribute('data-rating', '★ ' + stats.AverageRating.toFixed(1));
                    }
                    return;
                }

                // Fetch rating from API
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

                let deviceId = localStorage.getItem('_deviceId2');
                if (!deviceId) {
                    deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                        const r = Math.random() * 16 | 0;
                        const v = c === 'x' ? r : (r & 0x3 | 0x8);
                        return v.toString(16);
                    });
                    localStorage.setItem('_deviceId2', deviceId);
                }

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'GET',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                    .then(response => {
                        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
                        return response.json();
                    })
                    .then(stats => {
                        if (stats.TotalRatings > 0) {
                            self.ratingsCache[itemId] = stats;
                            card.classList.add('has-rating');
                            card.setAttribute('data-rating', '★ ' + stats.AverageRating.toFixed(1));
                        } else {
                            self.ratingsCache[itemId] = null;
                        }
                    })
                    .catch(() => {
                        self.ratingsCache[itemId] = null;
                    });
            });
        }
    };

    // Initialize when DOM is ready

    function initPlugin() {
        RatingsPlugin.init();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPlugin);
    } else {
        // Try immediate init
        initPlugin();
    }

    // Also try after a delay to ensure Jellyfin is fully loaded
    setTimeout(initPlugin, 2000);

    // Make it globally available
    window.RatingsPlugin = RatingsPlugin;
})();
