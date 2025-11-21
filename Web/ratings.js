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

                    self.initRequestButton();
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
            if (window.Emby && window.Emby.Page) {
                Emby.Page.addEventListener('pageshow', () => {
                    if (!document.getElementById('requestMediaBtn')) {
                        setTimeout(tryInit, 500);
                    }
                });
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
                    position: fixed !important;
                    top: 8px !important;
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

                /* Mobile Responsive - 70% size, positioned more to the right */
                @media screen and (max-width: 768px) {
                    #requestMediaBtn {
                        padding: 8px 24px !important;
                        font-size: 11px !important;
                        border-radius: 15px !important;
                        right: 190px !important;
                        top: 6px !important;
                    }

                    #requestMediaBtn .btn-text {
                        font-size: 11px !important;
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

                // Add to DOM
                document.body.appendChild(btn);
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
