// ==UserScript==
// @name         Jellyfin Ratings Plugin UI
// @namespace    http://tampermonkey.net/
// @version      1.0
// @description  Load rating stars UI for Jellyfin Ratings Plugin
// @author       K3ntas
// @match        http://*/web/*
// @match        https://*/web/*
// @match        http://*/index.html*
// @match        https://*/index.html*
// @grant        none
// @run-at       document-end
// ==/UserScript==

(function() {
    'use strict';

    // Get the server URL from the current page
    const serverUrl = window.location.origin;

    // Create script element
    const script = document.createElement('script');
    script.src = serverUrl + '/Ratings/ratings.js';
    script.type = 'text/javascript';

    // Add error handling
    script.onerror = function() {
        console.error('[Jellyfin Ratings] Failed to load ratings.js from:', script.src);
    };

    script.onload = function() {
        console.log('[Jellyfin Ratings] Successfully loaded ratings UI');
    };

    // Append to document
    document.head.appendChild(script);
})();
