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
                    margin: -12em 40em 8em;
                    padding: 0em;
                    background: rgba(0, 0, 0, 0.3);
                    border-radius: 8px;
                    max-width: 800px;
                    text-align: center;
                    z-index: 9999;
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
            // Check if this is a folder card by looking at data attributes
            if (card.getAttribute('data-isfolder') === 'true') {
                return null; // Skip all folder cards
            }

            // Try to find link with item ID
            const link = card.querySelector('a[href*="id="]');
            if (link) {
                // Skip folder/library view links (hash-based routing)
                if (link.href.includes('#/list') ||
                    link.href.includes('#/tv?') ||
                    link.href.includes('#/movies?') ||
                    link.href.includes('#/music?') ||
                    link.href.includes('topParentId=') ||
                    link.href.includes('parentId=')) {
                    return null;
                }

                // Only process links to detail pages
                if (!link.href.includes('#/details') && !link.href.includes('#!/details')) {
                    return null;
                }

                const match = link.href.match(/[?&]id=([a-f0-9]{32})/i);
                if (match) {
                    return match[1];
                }
            }

            // Try parent link
            const parentLink = card.closest('a[href*="id="]');
            if (parentLink) {
                // Skip folder/library view links
                if (parentLink.href.includes('#/list') ||
                    parentLink.href.includes('#/tv?') ||
                    parentLink.href.includes('#/movies?') ||
                    parentLink.href.includes('#/music?') ||
                    parentLink.href.includes('topParentId=') ||
                    parentLink.href.includes('parentId=')) {
                    return null;
                }

                // Only process links to detail pages
                if (!parentLink.href.includes('#/details') && !parentLink.href.includes('#!/details')) {
                    return null;
                }

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
