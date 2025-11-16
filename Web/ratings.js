/**
 * Jellyfin Ratings Plugin - Client-side component
 */

(function () {
    'use strict';

    const RatingsPlugin = {
        pluginId: 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d',

        /**
         * Initialize the ratings plugin
         */
        init: function () {
            console.log('[Ratings Plugin] Initializing...');
            this.injectStyles();
            this.observeDetailPages();
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
                    margin: 1em 0;
                    padding: 1em;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
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
                    gap: 0.3em;
                    margin-bottom: 0.5em;
                    position: relative;
                }

                .ratings-plugin-star {
                    font-size: 2em;
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

            // Create observer for route changes
            let lastUrl = location.href;
            new MutationObserver(() => {
                const url = location.href;
                if (url !== lastUrl) {
                    lastUrl = url;
                    self.onPageChange();
                }
            }).observe(document.querySelector('body'), { subtree: true, childList: true });

            // Initial check
            this.onPageChange();
        },

        /**
         * Handle page change
         */
        onPageChange: function () {
            const itemId = this.getItemIdFromUrl();
            if (itemId) {
                console.log('[Ratings Plugin] Item detected:', itemId);
                setTimeout(() => this.injectRatingComponent(itemId), 500);
            }
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

            console.log('[Ratings Plugin] Checking URL for item ID...');
            console.log('[Ratings Plugin] Full URL:', url);
            console.log('[Ratings Plugin] Hash:', hash);
            console.log('[Ratings Plugin] Search:', search);

            // Pattern 1: Hash-based routing (#!/details?id=...)
            let match = hash.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                console.log('[Ratings Plugin] Found item ID in hash:', match[1]);
                return match[1];
            }

            // Pattern 2: Query string (?id=...)
            match = search.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                console.log('[Ratings Plugin] Found item ID in search:', match[1]);
                return match[1];
            }

            // Pattern 3: Path-based (/item/id or /details/id)
            match = pathname.match(/\/(?:item|details)\/([a-f0-9]+)/i);
            if (match) {
                console.log('[Ratings Plugin] Found item ID in path:', match[1]);
                return match[1];
            }

            // Pattern 4: Anywhere in URL
            match = url.match(/id=([a-f0-9]{32})/i);
            if (match) {
                console.log('[Ratings Plugin] Found item ID in full URL:', match[1]);
                return match[1];
            }

            console.log('[Ratings Plugin] No item ID found in URL');
            return null;
        },

        /**
         * Inject rating component into the page
         */
        injectRatingComponent: function (itemId) {
            if (document.getElementById('ratingsPluginComponent')) {
                return; // Already injected
            }

            // Find a good place to inject the component
            const detailPageContent = document.querySelector('.detailPageContent') ||
                                     document.querySelector('.itemDetailPage') ||
                                     document.querySelector('.detailPage-content');

            if (!detailPageContent) {
                console.log('[Ratings Plugin] Could not find detail page content');
                return;
            }

            const container = document.createElement('div');
            container.id = 'ratingsPluginComponent';
            container.className = 'ratings-plugin-container';

            container.innerHTML = `
                <div class="ratings-plugin-title">Rate This</div>
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

            // Find better insertion point - look for specific Jellyfin elements
            let insertionPoint = null;

            // Try to find the overview section or similar elements
            const overview = detailPageContent.querySelector('.overview') ||
                           detailPageContent.querySelector('.itemOverview') ||
                           detailPageContent.querySelector('[class*="overview"]');

            if (overview) {
                // Insert before the overview
                insertionPoint = overview;
                console.log('[Ratings Plugin] Inserting before overview');
            } else {
                // Try to find any section that contains movie details
                const detailSections = detailPageContent.querySelectorAll('.detailSection, .itemDetails, [class*="detail"]');
                if (detailSections.length > 0) {
                    insertionPoint = detailSections[0];
                    console.log('[Ratings Plugin] Inserting before first detail section');
                }
            }

            if (insertionPoint) {
                detailPageContent.insertBefore(container, insertionPoint);
            } else {
                // Fallback to appending at beginning
                if (detailPageContent.firstChild) {
                    detailPageContent.insertBefore(container, detailPageContent.firstChild);
                } else {
                    detailPageContent.appendChild(container);
                }
                console.log('[Ratings Plugin] Using fallback insertion');
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

            ApiClient.getJSON(ApiClient.getUrl(`Ratings/Items/${itemId}/Stats`))
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
                    console.error('[Ratings Plugin] Error loading stats:', err);
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

            console.log('========== [Ratings Plugin] START SUBMIT RATING ==========');
            console.log('[Ratings Plugin] Step 1: Submitting rating:', rating, 'for item:', itemId);

            if (!window.ApiClient) {
                console.error('[Ratings Plugin] ERROR: ApiClient not available');
                return;
            }
            console.log('[Ratings Plugin] Step 2: ApiClient is available');

            // Gather all authentication info
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Rating?rating=${rating}`;

            console.log('[Ratings Plugin] Step 3: Authentication details:');
            console.log('  - Base URL:', baseUrl);
            console.log('  - Access Token:', accessToken ? `${accessToken.substring(0, 10)}...` : 'NULL');
            console.log('  - Device ID:', deviceId);
            console.log('  - Full URL:', url);

            // Build proper X-Emby-Authorization header (Jellyfin's dedicated auth header)
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;
            console.log('[Ratings Plugin] Step 4: X-Emby-Authorization header built:', authHeader);

            const requestOptions = {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            };
            console.log('[Ratings Plugin] Step 5: Request options:', JSON.stringify(requestOptions, null, 2));

            console.log('[Ratings Plugin] Step 6: Sending fetch request...');
            fetch(url, requestOptions)
                .then(function(response) {
                    console.log('[Ratings Plugin] Step 7: Response received');
                    console.log('  - Status:', response.status);
                    console.log('  - Status Text:', response.statusText);
                    console.log('  - OK:', response.ok);
                    console.log('  - Headers:', Array.from(response.headers.entries()));

                    if (!response.ok) {
                        return response.text().then(function(errorText) {
                            console.error('[Ratings Plugin] Step 8: ERROR Response body:', errorText);
                            throw new Error('HTTP ' + response.status + ': ' + errorText);
                        });
                    }
                    console.log('[Ratings Plugin] Step 8: Response OK, parsing...');
                    return response.text().then(function(text) {
                        console.log('[Ratings Plugin] Step 9: Response text:', text);
                        return text ? JSON.parse(text) : {};
                    });
                })
                .then(function(data) {
                    console.log('[Ratings Plugin] Step 10: SUCCESS! Data:', data);
                    console.log('[Ratings Plugin] Rating submitted successfully:', rating);

                    // Immediately update the star display for instant feedback
                    self.updateStarDisplay(rating);

                    // Then reload full stats from server
                    self.loadRatings(itemId);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Rated ' + rating + '/10');
                        });
                    }
                    console.log('========== [Ratings Plugin] END SUBMIT RATING (SUCCESS) ==========');
                })
                .catch(function(err) {
                    console.error('========== [Ratings Plugin] ERROR SUBMITTING RATING ==========');
                    console.error('[Ratings Plugin] Error object:', err);
                    console.error('[Ratings Plugin] Error message:', err.message);
                    console.error('[Ratings Plugin] Error stack:', err.stack);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error submitting rating: ' + err.message);
                        });
                    }
                    console.error('========== [Ratings Plugin] END SUBMIT RATING (ERROR) ==========');
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
                    console.error('[Ratings Plugin] Error loading detailed ratings:', err);
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
        }
    };

    // Initialize when DOM is ready
    console.log('[Ratings Plugin] Script loaded, readyState:', document.readyState);

    function initPlugin() {
        console.log('[Ratings Plugin] Attempting initialization...');
        console.log('[Ratings Plugin] Current URL:', window.location.href);
        console.log('[Ratings Plugin] Current pathname:', window.location.pathname);
        console.log('[Ratings Plugin] Current hash:', window.location.hash);
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
