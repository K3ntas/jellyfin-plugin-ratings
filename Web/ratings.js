/**
 * Jellyfin Ratings Plugin - Client-side component
 */

(function () {
    'use strict';

    const RatingsPlugin = {
        pluginId: 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d',
        ratingsCache: {}, // Cache for card ratings to avoid duplicate API calls
        currentLanguage: 'en', // Default language

        // Translations for request modal
        translations: {
            en: {
                requestMedia: 'Request Media',
                manageRequests: 'Manage Media Requests',
                requestDescription: '📬 Request Your Favorite Media!',
                requestDescriptionText: 'Use this form to request movies or TV series that you\'d like to watch. The admin will review your request and add it to the library as soon as possible. You can track the status of all your requests below.',
                mediaTitle: 'Media Title *',
                mediaTitlePlaceholder: 'e.g., Breaking Bad, The Godfather',
                type: 'Type *',
                selectType: '-- Select Type --',
                movie: 'Movie',
                tvSeries: 'TV Series',
                anime: 'Anime',
                documentary: 'Documentary',
                other: 'Other',
                additionalNotes: 'Additional Notes',
                notesPlaceholder: 'Season number, year, specific details, etc.',
                submitRequest: 'Submit Request',
                yourRequests: 'Your Requests',
                loadingRequests: 'Loading your requests...',
                noRequests: 'You haven\'t requested any media yet',
                errorLoading: 'Error loading your requests',
                notSpecified: 'Not specified',
                noDetails: 'No details',
                watchNow: '🎬 Watch Now',
                noRequestsYet: 'No media requests yet',
                pending: 'PENDING',
                processing: 'PROCESSING',
                done: 'DONE',
                rejected: 'REJECTED',
                titleRequired: 'Please enter a media title',
                typeRequired: 'Please select a media type',
                requestSubmitted: 'Request submitted successfully!',
                requestFailed: 'Failed to submit request',
                statusUpdated: 'Status updated',
                statusUpdateFailed: 'Failed to update status',
                addLink: '+ Link',
                enterMediaLink: 'Enter media link:',
                delete: 'Delete',
                confirmDelete: 'Are you sure you want to delete this request?',
                mediaLinkPlaceholder: 'Media link (paste URL when done)',
                unknown: 'Unknown',
                loading: 'Loading...',
                snooze: 'Snooze',
                unsnooze: 'Unsnooze',
                snoozed: 'SNOOZED',
                snoozedUntil: 'Snoozed until',
                snoozeDate: 'Snooze until date',
                categoryProcessing: '🔄 Processing',
                categoryPending: '⏳ Pending',
                categorySnoozed: '💤 Snoozed',
                categoryDone: '✅ Done',
                categoryRejected: '❌ Rejected',
                createRequest: 'Create Request',
                latestMedia: 'Latest Media',
                latestMediaLoading: 'Loading...',
                latestMediaEmpty: 'No recent media found',
                latestMediaError: 'Failed to load',
                typeMovie: 'Movie',
                typeSeries: 'Series',
                typeAnime: 'Anime',
                typeOther: 'Other',
                timeAgo: 'ago',
                timeJustNow: 'just now',
                timeMinutes: 'min',
                timeHours: 'h',
                timeDays: 'd',
                // Media Management translations
                mediaManagement: 'Media',
                mediaManagementTitle: 'Media Management',
                mediaSearch: 'Search...',
                mediaTypeAll: 'All Types',
                mediaTypeMovie: 'Movies',
                mediaTypeSeries: 'Series',
                mediaSortBy: 'Sort by',
                mediaSortTitle: 'Title',
                mediaSortYear: 'Year',
                mediaSortRating: 'Rating',
                mediaSortDateAdded: 'Date Added',
                mediaSortPlayCount: 'Plays',
                mediaSortWatchTime: 'Watch Time',
                mediaSortSize: 'Size',
                mediaLoading: 'Loading media...',
                mediaNoResults: 'No media found',
                mediaError: 'Error loading media',
                mediaScheduleDelete: 'Schedule Delete',
                mediaCancelDelete: 'Cancel Deletion',
                mediaDeleteIn: 'Delete in',
                mediaLeavingIn: 'Leaving in',
                media1Day: '1 Day',
                media3Days: '3 Days',
                media1Week: '1 Week',
                media2Weeks: '2 Weeks',
                mediaCustom: 'Custom...',
                mediaDays: 'days',
                mediaPlays: 'plays',
                mediaMinutes: 'min',
                mediaGB: 'GB',
                mediaMB: 'MB',
                mediaPage: 'Page',
                mediaOf: 'of',
                mediaPrev: 'Prev',
                mediaNext: 'Next'
            },
            lt: {
                requestMedia: 'Užsakyti Mediją',
                manageRequests: 'Tvarkyti Medijos Užklausas',
                requestDescription: '📬 Užsakykite Savo Mėgstamą Mediją!',
                requestDescriptionText: 'Naudokite šią formą, kad užsakytumėte filmus ar TV serialus, kuriuos norėtumėte žiūrėti. Administratorius peržiūrės jūsų užklausą ir pridės ją į biblioteką kuo greičiau. Žemiau galite sekti visų savo užklausų būseną.',
                mediaTitle: 'Medijos Pavadinimas *',
                mediaTitlePlaceholder: 'pvz., Breaking Bad, Krikštatėvis',
                type: 'Tipas *',
                selectType: '-- Pasirinkite Tipą --',
                movie: 'Filmas',
                tvSeries: 'TV Serialas',
                anime: 'Anime',
                documentary: 'Dokumentika',
                other: 'Kita',
                additionalNotes: 'Papildomos Pastabos',
                notesPlaceholder: 'Sezono numeris, metai, specifinė informacija ir t.t.',
                submitRequest: 'Pateikti Užklausą',
                yourRequests: 'Jūsų Užklausos',
                loadingRequests: 'Kraunamos jūsų užklausos...',
                noRequests: 'Jūs dar neužsakėte jokios medijos',
                errorLoading: 'Klaida kraunant jūsų užklausas',
                notSpecified: 'Nenurodyta',
                noDetails: 'Nėra detalių',
                watchNow: '🎬 Žiūrėti Dabar',
                noRequestsYet: 'Medijos užklausų dar nėra',
                pending: 'LAUKIAMA',
                processing: 'VYKDOMA',
                done: 'ATLIKTA',
                rejected: 'ATMESTA',
                titleRequired: 'Įveskite medijos pavadinimą',
                typeRequired: 'Pasirinkite medijos tipą',
                requestSubmitted: 'Užklausa sėkmingai pateikta!',
                requestFailed: 'Nepavyko pateikti užklausos',
                statusUpdated: 'Būsena atnaujinta',
                statusUpdateFailed: 'Nepavyko atnaujinti būsenos',
                addLink: '+ Nuoroda',
                enterMediaLink: 'Įveskite medijos nuorodą:',
                delete: 'Ištrinti',
                confirmDelete: 'Ar tikrai norite ištrinti šią užklausą?',
                mediaLinkPlaceholder: 'Medijos nuoroda (įklijuokite URL kai baigta)',
                unknown: 'Nežinoma',
                loading: 'Kraunama...',
                snooze: 'Atidėti',
                unsnooze: 'Atšaukti atidėjimą',
                snoozed: 'ATIDĖTA',
                snoozedUntil: 'Atidėta iki',
                snoozeDate: 'Atidėti iki datos',
                categoryProcessing: '🔄 Vykdoma',
                categoryPending: '⏳ Laukiama',
                categorySnoozed: '💤 Atidėta',
                categoryDone: '✅ Atlikta',
                categoryRejected: '❌ Atmesta',
                createRequest: 'Sukurti Užklausą',
                latestMedia: 'Naujausia Medija',
                latestMediaLoading: 'Kraunama...',
                latestMediaEmpty: 'Naujų medijų nerasta',
                latestMediaError: 'Nepavyko įkelti',
                typeMovie: 'Filmas',
                typeSeries: 'Serialas',
                typeAnime: 'Anime',
                typeOther: 'Kita',
                timeAgo: 'prieš',
                timeJustNow: 'ką tik',
                timeMinutes: 'min',
                timeHours: 'val',
                timeDays: 'd',
                // Media Management translations
                mediaManagement: 'Medija',
                mediaManagementTitle: 'Medijos Valdymas',
                mediaSearch: 'Ieškoti...',
                mediaTypeAll: 'Visi Tipai',
                mediaTypeMovie: 'Filmai',
                mediaTypeSeries: 'Serialai',
                mediaSortBy: 'Rūšiuoti pagal',
                mediaSortTitle: 'Pavadinimas',
                mediaSortYear: 'Metai',
                mediaSortRating: 'Reitingas',
                mediaSortDateAdded: 'Pridėjimo data',
                mediaSortPlayCount: 'Peržiūros',
                mediaSortWatchTime: 'Žiūrėjimo laikas',
                mediaSortSize: 'Dydis',
                mediaLoading: 'Kraunama medija...',
                mediaNoResults: 'Medija nerasta',
                mediaError: 'Klaida kraunant mediją',
                mediaScheduleDelete: 'Planuoti Ištrynimą',
                mediaCancelDelete: 'Atšaukti Ištrynimą',
                mediaDeleteIn: 'Ištrinti po',
                mediaLeavingIn: 'Išeina po',
                media1Day: '1 Diena',
                media3Days: '3 Dienos',
                media1Week: '1 Savaitė',
                media2Weeks: '2 Savaitės',
                mediaCustom: 'Pasirinkti...',
                mediaDays: 'dienų',
                mediaPlays: 'peržiūrų',
                mediaMinutes: 'min',
                mediaGB: 'GB',
                mediaMB: 'MB',
                mediaPage: 'Puslapis',
                mediaOf: 'iš',
                mediaPrev: 'Ankstesnis',
                mediaNext: 'Kitas'
            }
        },

        // Get translation for current language
        t: function(key) {
            return this.translations[this.currentLanguage]?.[key] || this.translations.en[key] || key;
        },

        // Set language and refresh modal if open
        setLanguage: function(lang) {
            this.currentLanguage = lang;
            localStorage.setItem('ratingsPluginLanguage', lang);
            // Update language toggle visual state
            const toggle = document.getElementById('languageToggle');
            if (toggle) {
                toggle.checked = lang === 'lt';
            }
            // Update button text
            const btnText = document.querySelector('#requestMediaBtn .btn-text');
            if (btnText) {
                btnText.textContent = this.t('requestMedia');
            }
        },

        /**
         * Initialize the ratings plugin
         */
        init: function () {
            // Load saved language preference
            const savedLang = localStorage.getItem('ratingsPluginLanguage');
            if (savedLang && (savedLang === 'en' || savedLang === 'lt')) {
                this.currentLanguage = savedLang;
            }

            this.injectStyles();
            this.observeDetailPages();
            this.observeHomePageCards();

            // Initialize request button with multiple attempts for reliability
            this.initRequestButtonWithRetry();

            // Initialize search field in header
            this.initSearchField();

            // Initialize notification toggle in header
            this.initNotificationToggle();

            // Initialize latest media button (replaces sync play)
            this.initLatestMediaButton();

            // Initialize responsive scaling
            this.updateResponsiveScaling();

            // Initialize Netflix view if enabled
            this.initNetflixView();

            // Initialize new media notifications
            this.initNotifications();

            // Initialize media management button (admin only)
            this.initMediaManagementButtonWithRetry();

            // Initialize deletion badges (for all users)
            this.initDeletionBadges();
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
                    background-color: transparent !important;
                    border: none !important;
                    outline: none !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                    width: 200px !important;
                    padding: 4px 0 !important;
                    -webkit-appearance: none !important;
                    -moz-appearance: none !important;
                    appearance: none !important;
                }

                #headerSearchInput:focus {
                    background: transparent !important;
                    background-color: transparent !important;
                    outline: none !important;
                }

                #headerSearchInput:-webkit-autofill,
                #headerSearchInput:-webkit-autofill:hover,
                #headerSearchInput:-webkit-autofill:focus,
                #headerSearchInput:-webkit-autofill:active {
                    -webkit-box-shadow: 0 0 0 30px rgba(60, 60, 60, 0.9) inset !important;
                    -webkit-text-fill-color: #fff !important;
                    background-color: transparent !important;
                    transition: background-color 5000s ease-in-out 0s !important;
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

                /* Search Dropdown Results */
                #searchDropdown {
                    position: fixed !important;
                    min-width: 350px !important;
                    max-width: 450px !important;
                    max-height: 70vh !important;
                    overflow-y: auto !important;
                    background: #1e1e1e !important;
                    border: 1px solid rgba(255, 255, 255, 0.3) !important;
                    border-radius: 12px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.8) !important;
                    z-index: 2147483647 !important;
                    display: none;
                }

                #searchDropdown.visible {
                    display: block !important;
                }

                #searchDropdown .dropdown-loading {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #999 !important;
                    font-size: 13px !important;
                }

                #searchDropdown .dropdown-empty {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #888 !important;
                    font-size: 13px !important;
                }

                #searchDropdown .dropdown-item {
                    display: flex !important;
                    align-items: center !important;
                    padding: 10px 12px !important;
                    cursor: pointer !important;
                    transition: background 0.2s ease !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
                    text-decoration: none !important;
                }

                #searchDropdown .dropdown-item:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                }

                #searchDropdown .dropdown-item:last-child {
                    border-bottom: none !important;
                }

                #searchDropdown .dropdown-item-image {
                    width: 45px !important;
                    height: 65px !important;
                    object-fit: cover !important;
                    border-radius: 4px !important;
                    margin-right: 12px !important;
                    background: #333 !important;
                    flex-shrink: 0 !important;
                }

                #searchDropdown .dropdown-item-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                    overflow: hidden !important;
                }

                #searchDropdown .dropdown-item-title {
                    color: #fff !important;
                    font-size: 14px !important;
                    font-weight: 500 !important;
                    white-space: nowrap !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    margin-bottom: 4px !important;
                }

                #searchDropdown .dropdown-item-meta {
                    color: #888 !important;
                    font-size: 11px !important;
                    display: flex !important;
                    gap: 8px !important;
                }

                #searchDropdown .dropdown-item-type {
                    background: rgba(0, 164, 220, 0.3) !important;
                    color: #00a4dc !important;
                    padding: 2px 6px !important;
                    border-radius: 4px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                }

                #searchDropdown .dropdown-item-year {
                    color: #666 !important;
                }

                @media screen and (max-width: 925px) {
                    #searchDropdown {
                        min-width: 280px !important;
                        max-width: 320px !important;
                        left: 0 !important;
                        right: auto !important;
                    }

                    #searchDropdown .dropdown-item-image {
                        width: 40px !important;
                        height: 58px !important;
                    }

                    #searchDropdown .dropdown-item-title {
                        font-size: 13px !important;
                    }
                }

                /* Notification Toggle Styles - Positioned LEFT of search field */
                /* Search field: right:480px, ~258px wide = ends at ~738px from right */
                /* Toggle must be at right:750px+ to be LEFT of search */
                #notificationToggle {
                    position: absolute !important;
                    top: 8px;
                    right: 755px !important;
                    z-index: 999998 !important;
                    display: flex !important;
                    align-items: center !important;
                    background: rgba(60, 60, 60, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    border-radius: 25px !important;
                    padding: 8px 14px !important;
                    transition: all 0.3s ease !important;
                    cursor: pointer !important;
                    gap: 8px !important;
                    overflow: visible !important;
                }

                #notificationToggle:hover {
                    background: rgba(70, 70, 70, 0.95) !important;
                    border-color: rgba(255, 255, 255, 0.4) !important;
                }


                #notificationToggle.hidden {
                    display: none !important;
                }

                #notificationToggleIcon {
                    font-size: 16px !important;
                    opacity: 0.9 !important;
                }

                #notificationToggleSwitch {
                    position: relative !important;
                    width: 36px !important;
                    height: 20px !important;
                    background: rgba(100, 100, 100, 0.8) !important;
                    border-radius: 10px !important;
                    transition: background 0.3s ease !important;
                }

                #notificationToggleSwitch.enabled {
                    background: rgba(0, 164, 220, 0.9) !important;
                }

                #notificationToggleSwitch::after {
                    content: '' !important;
                    position: absolute !important;
                    top: 2px !important;
                    left: 2px !important;
                    width: 16px !important;
                    height: 16px !important;
                    background: #fff !important;
                    border-radius: 50% !important;
                    transition: transform 0.3s ease !important;
                }

                #notificationToggleSwitch.enabled::after {
                    transform: translateX(16px) !important;
                }

                /* Tooltip for notification toggle - using fixed positioning to avoid clipping */
                #notificationTooltip {
                    position: fixed !important;
                    background: rgba(20, 20, 20, 0.98) !important;
                    color: #fff !important;
                    padding: 10px 14px !important;
                    border-radius: 8px !important;
                    font-size: 13px !important;
                    white-space: nowrap !important;
                    opacity: 0;
                    visibility: hidden;
                    transition: opacity 0.2s ease, visibility 0.2s ease !important;
                    pointer-events: none !important;
                    z-index: 99999999 !important;
                    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                }

                #notificationTooltip.show {
                    opacity: 1 !important;
                    visibility: visible !important;
                }

                /* Mobile Responsive for Notification Toggle - LEFT of Request button */
                @media screen and (max-width: 925px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 55px !important;
                        left: auto !important;
                        right: 150px !important;
                        padding: 8px 10px !important;
                    }
                }

                @media screen and (max-width: 590px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 58px !important;
                        right: 130px !important;
                        padding: 6px 10px !important;
                    }

                    #notificationToggleSwitch {
                        width: 32px !important;
                        height: 18px !important;
                    }

                    #notificationToggleSwitch::after {
                        width: 14px !important;
                        height: 14px !important;
                    }

                    #notificationToggleSwitch.enabled::after {
                        transform: translateX(14px) !important;
                    }

                    #notificationToggleIcon {
                        font-size: 14px !important;
                    }
                }

                @media screen and (max-width: 470px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 10px !important;
                        right: 180px !important;
                        padding: 6px 10px !important;
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

                /* Language Toggle Container */
                .language-toggle-container {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    gap: 10px !important;
                    margin-bottom: 15px !important;
                    padding: 10px !important;
                    background: rgba(255, 255, 255, 0.05) !important;
                    border-radius: 8px !important;
                }

                .lang-label {
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #aaa !important;
                    min-width: 20px !important;
                    text-align: center !important;
                }

                .language-switch {
                    position: relative !important;
                    display: inline-block !important;
                    width: 50px !important;
                    height: 26px !important;
                }

                .language-switch input {
                    opacity: 0 !important;
                    width: 0 !important;
                    height: 0 !important;
                }

                .lang-slider {
                    position: absolute !important;
                    cursor: pointer !important;
                    top: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    bottom: 0 !important;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    transition: 0.3s !important;
                    border-radius: 26px !important;
                }

                .lang-slider:before {
                    position: absolute !important;
                    content: "" !important;
                    height: 20px !important;
                    width: 20px !important;
                    left: 3px !important;
                    bottom: 3px !important;
                    background: white !important;
                    transition: 0.3s !important;
                    border-radius: 50% !important;
                }

                .language-switch input:checked + .lang-slider:before {
                    transform: translateX(24px) !important;
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

                .admin-request-status-badge.rejected {
                    background: #f44336 !important;
                    color: #fff !important;
                }

                .admin-request-status-badge.snoozed {
                    background: #9c27b0 !important;
                    color: #fff !important;
                }

                /* Category Sections */
                .admin-category-section {
                    margin-bottom: 24px !important;
                }

                .admin-category-header {
                    color: #fff !important;
                    font-size: 16px !important;
                    font-weight: 600 !important;
                    margin: 0 0 12px 0 !important;
                    padding: 10px 15px !important;
                    background: linear-gradient(90deg, rgba(255,255,255,0.1) 0%, transparent 100%) !important;
                    border-radius: 6px !important;
                    border-left: 4px solid #00a4dc !important;
                }

                .admin-category-section[data-category="processing"] .admin-category-header {
                    border-left-color: #2196F3 !important;
                }

                .admin-category-section[data-category="pending"] .admin-category-header {
                    border-left-color: #ff9800 !important;
                }

                .admin-category-section[data-category="snoozed"] .admin-category-header {
                    border-left-color: #9c27b0 !important;
                }

                .admin-category-section[data-category="done"] .admin-category-header {
                    border-left-color: #4CAF50 !important;
                }

                .admin-category-section[data-category="rejected"] .admin-category-header {
                    border-left-color: #f44336 !important;
                }

                /* Snooze Controls */
                .admin-snooze-controls {
                    display: flex !important;
                    gap: 8px !important;
                    align-items: center !important;
                    margin-top: 8px !important;
                    padding-top: 8px !important;
                    border-top: 1px dashed #444 !important;
                }

                .admin-snooze-date {
                    padding: 6px 10px !important;
                    border-radius: 6px !important;
                    border: 1px solid #444 !important;
                    background: #333 !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    cursor: pointer !important;
                }

                .admin-snooze-date::-webkit-calendar-picker-indicator {
                    filter: invert(1) !important;
                    cursor: pointer !important;
                }

                .admin-snooze-btn, .admin-unsnooze-btn {
                    padding: 6px 12px !important;
                    border: none !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .admin-snooze-btn {
                    background: rgba(156, 39, 176, 0.2) !important;
                    color: #ce93d8 !important;
                    border: 1px solid #9c27b0 !important;
                }

                .admin-snooze-btn:hover {
                    background: rgba(156, 39, 176, 0.4) !important;
                }

                .admin-unsnooze-btn {
                    background: rgba(255, 152, 0, 0.2) !important;
                    color: #ffb74d !important;
                    border: 1px solid #ff9800 !important;
                }

                .admin-unsnooze-btn:hover {
                    background: rgba(255, 152, 0, 0.4) !important;
                }

                .admin-snooze-info {
                    color: #ce93d8 !important;
                    font-size: 11px !important;
                    margin-top: 4px !important;
                    font-weight: 500 !important;
                }

                .snoozed-item {
                    border-color: #9c27b0 !important;
                    background: rgba(156, 39, 176, 0.1) !important;
                }

                .admin-rejection-reason {
                    color: #f44336 !important;
                    font-size: 11px !important;
                    margin-top: 4px !important;
                    font-style: italic !important;
                }

                .admin-rejection-input {
                    width: 100% !important;
                    padding: 8px !important;
                    border-radius: 6px !important;
                    border: 1px solid #444 !important;
                    background: #333 !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    margin-top: 4px !important;
                }

                .admin-rejection-input::placeholder {
                    color: #888 !important;
                }

                .admin-request-custom-field {
                    color: #9c9 !important;
                    font-size: 11px !important;
                    margin-top: 2px !important;
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

                .admin-status-btn.rejected {
                    background: rgba(244, 67, 54, 0.2) !important;
                    color: #f44336 !important;
                    border: 1px solid #f44336 !important;
                }

                .admin-status-btn.rejected:hover {
                    background: #f44336 !important;
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

                .user-request-status.rejected {
                    background: #f44336 !important;
                    color: #fff !important;
                }

                .user-request-rejection-reason {
                    color: #f44336 !important;
                    font-size: 12px !important;
                    margin-top: 6px !important;
                    font-style: italic !important;
                    padding: 6px 10px !important;
                    background: rgba(244, 67, 54, 0.1) !important;
                    border-radius: 4px !important;
                }

                .user-request-custom-field {
                    color: #9c9 !important;
                    font-size: 12px !important;
                    margin-top: 2px !important;
                }

                .user-request-imdb {
                    color: #f5c518 !important;
                    font-size: 12px !important;
                    margin-top: 2px !important;
                }

                .user-request-imdb .imdb-link,
                .admin-request-imdb .imdb-link {
                    color: #f5c518 !important;
                    text-decoration: none !important;
                }

                .user-request-imdb .imdb-link:hover,
                .admin-request-imdb .imdb-link:hover {
                    text-decoration: underline !important;
                }

                .admin-tabs {
                    display: flex !important;
                    gap: 0 !important;
                    margin-bottom: 15px !important;
                    border-bottom: 2px solid #444 !important;
                }

                .admin-tab {
                    padding: 10px 20px !important;
                    background: transparent !important;
                    border: none !important;
                    color: #999 !important;
                    cursor: pointer !important;
                    font-size: 14px !important;
                    font-weight: 500 !important;
                    transition: all 0.2s ease !important;
                    border-bottom: 2px solid transparent !important;
                    margin-bottom: -2px !important;
                }

                .admin-tab:hover {
                    color: #fff !important;
                }

                .admin-tab.active {
                    color: #00a4dc !important;
                    border-bottom-color: #00a4dc !important;
                }

                .admin-tab-content {
                    min-height: 200px !important;
                }

                .user-request-actions {
                    display: flex !important;
                    gap: 8px !important;
                    margin-top: 10px !important;
                }

                .user-edit-btn,
                .user-delete-btn {
                    padding: 6px 12px !important;
                    border: none !important;
                    border-radius: 4px !important;
                    cursor: pointer !important;
                    font-size: 12px !important;
                    transition: all 0.2s ease !important;
                }

                .user-edit-btn {
                    background: #4a90d9 !important;
                    color: #fff !important;
                }

                .user-edit-btn:hover {
                    background: #3a7bc8 !important;
                }

                .user-delete-btn {
                    background: #d94a4a !important;
                    color: #fff !important;
                }

                .user-delete-btn:hover {
                    background: #c83a3a !important;
                }

                .admin-request-imdb {
                    color: #f5c518 !important;
                    font-size: 11px !important;
                    margin-top: 2px !important;
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

                /* Latest Media Button - Replaces Sync Play Button */
                #latestMediaBtn {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    padding: 8px !important;
                    border-radius: 50% !important;
                    transition: background 0.2s ease !important;
                    color: #fff !important;
                    font-size: 24px !important;
                }

                #latestMediaBtn:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                }

                #latestMediaBtn.hidden {
                    display: none !important;
                }

                #latestMediaBtn svg {
                    width: 24px !important;
                    height: 24px !important;
                    fill: currentColor !important;
                }

                /* Latest Media Dropdown */
                #latestMediaDropdown {
                    position: fixed !important;
                    min-width: 320px !important;
                    max-width: 400px !important;
                    max-height: 70vh !important;
                    overflow-y: auto !important;
                    background: #1a1a1a !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 8px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.8) !important;
                    z-index: 2147483647 !important;
                    display: none;
                }

                #latestMediaDropdown.visible {
                    display: block !important;
                }

                #latestMediaDropdown .latest-header {
                    padding: 12px 14px !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    background: #1a1a1a !important;
                    position: sticky !important;
                    top: 0 !important;
                    z-index: 1 !important;
                }

                #latestMediaDropdown .latest-loading {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #888 !important;
                    font-size: 13px !important;
                }

                #latestMediaDropdown .latest-empty {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #666 !important;
                    font-size: 13px !important;
                }

                #latestMediaDropdown .latest-item {
                    display: flex !important;
                    align-items: center !important;
                    padding: 6px 10px !important;
                    cursor: pointer !important;
                    transition: background 0.15s ease !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.03) !important;
                    text-decoration: none !important;
                    gap: 10px !important;
                }

                #latestMediaDropdown .latest-item:hover {
                    background: rgba(255, 255, 255, 0.08) !important;
                }

                #latestMediaDropdown .latest-item:last-child {
                    border-bottom: none !important;
                }

                #latestMediaDropdown .latest-item-image {
                    width: 32px !important;
                    height: 48px !important;
                    object-fit: cover !important;
                    border-radius: 3px !important;
                    background: #2a2a2a !important;
                    flex-shrink: 0 !important;
                }

                #latestMediaDropdown .latest-item-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                    overflow: hidden !important;
                }

                #latestMediaDropdown .latest-item-title {
                    color: #e0e0e0 !important;
                    font-size: 12px !important;
                    font-weight: 500 !important;
                    white-space: nowrap !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    line-height: 1.3 !important;
                }

                #latestMediaDropdown .latest-item-meta {
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    margin-top: 2px !important;
                }

                #latestMediaDropdown .latest-item-year {
                    color: #666 !important;
                    font-size: 10px !important;
                }

                #latestMediaDropdown .latest-item-time {
                    color: #888 !important;
                    font-size: 9px !important;
                    margin-left: auto !important;
                    white-space: nowrap !important;
                }

                #latestMediaDropdown .latest-item-type {
                    padding: 1px 5px !important;
                    border-radius: 3px !important;
                    font-size: 9px !important;
                    font-weight: 600 !important;
                    text-transform: uppercase !important;
                }

                #latestMediaDropdown .latest-item-type.movie {
                    background: rgba(33, 150, 243, 0.25) !important;
                    color: #64b5f6 !important;
                }

                #latestMediaDropdown .latest-item-type.series {
                    background: rgba(76, 175, 80, 0.25) !important;
                    color: #81c784 !important;
                }

                #latestMediaDropdown .latest-item-type.anime {
                    background: rgba(156, 39, 176, 0.25) !important;
                    color: #ba68c8 !important;
                }

                #latestMediaDropdown .latest-item-type.other {
                    background: rgba(158, 158, 158, 0.25) !important;
                    color: #9e9e9e !important;
                }

                /* Scrollbar styling for latest media dropdown */
                #latestMediaDropdown::-webkit-scrollbar {
                    width: 6px !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-track {
                    background: transparent !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-thumb {
                    background: rgba(255, 255, 255, 0.2) !important;
                    border-radius: 3px !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-thumb:hover {
                    background: rgba(255, 255, 255, 0.3) !important;
                }

                @media screen and (max-width: 768px) {
                    #latestMediaDropdown {
                        min-width: 280px !important;
                        max-width: calc(100vw - 20px) !important;
                        right: 10px !important;
                        left: auto !important;
                    }

                    #latestMediaDropdown .latest-item {
                        padding: 5px 8px !important;
                    }

                    #latestMediaDropdown .latest-item-image {
                        width: 28px !important;
                        height: 42px !important;
                    }

                    #latestMediaDropdown .latest-item-title {
                        font-size: 11px !important;
                    }
                }

                /* Media Management Button Styles */
                #mediaManagementBtn {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    color: rgba(255, 255, 255, 0.8) !important;
                    padding: 0 !important;
                    margin: 0 4px !important;
                    width: 42px !important;
                    height: 42px !important;
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    transition: color 0.2s ease !important;
                    position: relative !important;
                }

                #mediaManagementBtn:hover {
                    color: #fff !important;
                }

                #mediaManagementBtn.hidden {
                    display: none !important;
                }

                #mediaManagementBtn svg {
                    width: 24px !important;
                    height: 24px !important;
                    fill: currentColor !important;
                }

                /* Media Management Modal Styles */
                #mediaManagementModal {
                    display: none;
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.85);
                    z-index: 999999;
                    overflow: auto;
                }

                #mediaManagementModal.show {
                    display: flex;
                    align-items: flex-start;
                    justify-content: center;
                    padding: 20px;
                }

                #mediaManagementModalContent {
                    background: #1a1a1a;
                    border-radius: 12px;
                    width: 100%;
                    max-width: 1200px;
                    max-height: 90vh;
                    overflow: hidden;
                    display: flex;
                    flex-direction: column;
                    position: relative;
                    margin-top: 20px;
                }

                #mediaManagementModalClose {
                    position: absolute;
                    top: 10px;
                    right: 15px;
                    background: transparent;
                    border: none;
                    color: #fff;
                    font-size: 28px;
                    cursor: pointer;
                    z-index: 10;
                    opacity: 0.7;
                    transition: opacity 0.2s;
                }

                #mediaManagementModalClose:hover {
                    opacity: 1;
                }

                #mediaManagementModalTitle {
                    font-size: 20px;
                    font-weight: 600;
                    padding: 15px 20px;
                    border-bottom: 1px solid #333;
                    color: #fff;
                }

                #mediaManagementControls {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                    padding: 15px 20px;
                    background: #222;
                    align-items: center;
                }

                #mediaManagementControls input,
                #mediaManagementControls select {
                    padding: 8px 12px;
                    border: 1px solid #444;
                    border-radius: 6px;
                    background: #333;
                    color: #fff;
                    font-size: 13px;
                }

                #mediaManagementControls input:focus,
                #mediaManagementControls select:focus {
                    outline: none;
                    border-color: #52b4e5;
                }

                #mediaSearchInput {
                    flex: 1;
                    min-width: 200px;
                }

                #mediaManagementBody {
                    flex: 1;
                    overflow-y: auto;
                    padding: 0;
                }

                .media-list-table {
                    width: 100%;
                    border-collapse: collapse;
                }

                .media-list-table th {
                    background: #282828;
                    color: #aaa;
                    font-size: 12px;
                    font-weight: 500;
                    text-align: left;
                    padding: 10px 12px;
                    position: sticky;
                    top: 0;
                    z-index: 5;
                }

                .media-list-table td {
                    padding: 8px 12px;
                    border-bottom: 1px solid #333;
                    color: #ddd;
                    font-size: 13px;
                }

                .media-list-table tr:hover td {
                    background: #282828;
                }

                .media-item-image {
                    width: 40px;
                    height: 60px;
                    object-fit: cover;
                    border-radius: 4px;
                    background: #333;
                }

                .media-item-title {
                    font-weight: 500;
                    color: #fff;
                }

                .media-item-title a {
                    color: #52b4e5;
                    text-decoration: none;
                }

                .media-item-title a:hover {
                    text-decoration: underline;
                }

                .media-item-type {
                    font-size: 10px;
                    padding: 2px 6px;
                    border-radius: 3px;
                    background: #2c3e50;
                    color: #fff;
                    display: inline-block;
                    margin-top: 4px;
                }

                .media-item-type.movie {
                    background: #2980b9;
                }

                .media-item-type.series {
                    background: #27ae60;
                }

                .media-item-rating {
                    color: #f1c40f;
                }

                .media-item-scheduled {
                    color: #e74c3c;
                    font-size: 11px;
                    font-weight: 500;
                }

                .media-actions {
                    display: flex;
                    gap: 6px;
                }

                .media-action-btn {
                    padding: 6px 10px;
                    border: none;
                    border-radius: 4px;
                    cursor: pointer;
                    font-size: 11px;
                    transition: all 0.2s;
                }

                .media-action-btn.delete {
                    background: #e74c3c;
                    color: #fff;
                }

                .media-action-btn.delete:hover {
                    background: #c0392b;
                }

                .media-action-btn.cancel {
                    background: #3498db;
                    color: #fff;
                }

                .media-action-btn.cancel:hover {
                    background: #2980b9;
                }

                #mediaManagementPagination {
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    gap: 10px;
                    padding: 15px;
                    background: #222;
                    border-top: 1px solid #333;
                }

                #mediaManagementPagination button {
                    padding: 6px 12px;
                    border: 1px solid #444;
                    border-radius: 4px;
                    background: #333;
                    color: #fff;
                    cursor: pointer;
                    font-size: 12px;
                }

                #mediaManagementPagination button:hover:not(:disabled) {
                    background: #444;
                }

                #mediaManagementPagination button:disabled {
                    opacity: 0.5;
                    cursor: not-allowed;
                }

                #mediaManagementPagination span {
                    color: #aaa;
                    font-size: 13px;
                }

                /* Deletion Dialog */
                #deletionDialog {
                    display: none;
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.7);
                    z-index: 9999999;
                    align-items: center;
                    justify-content: center;
                }

                #deletionDialog.show {
                    display: flex;
                }

                #deletionDialogContent {
                    background: #1a1a1a;
                    border-radius: 12px;
                    padding: 20px;
                    max-width: 400px;
                    width: 90%;
                }

                #deletionDialogTitle {
                    font-size: 16px;
                    font-weight: 600;
                    margin-bottom: 15px;
                    color: #fff;
                }

                #deletionDialogOptions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                }

                #deletionDialogOptions button {
                    flex: 1;
                    min-width: 80px;
                    padding: 10px 15px;
                    border: none;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 13px;
                    transition: all 0.2s;
                }

                .deletion-option-btn {
                    background: #e74c3c;
                    color: #fff;
                }

                .deletion-option-btn:hover {
                    background: #c0392b;
                }

                .deletion-cancel-btn {
                    background: #555;
                    color: #fff;
                }

                .deletion-cancel-btn:hover {
                    background: #666;
                }

                /* Leaving Badge Styles */
                .card-leaving-badge {
                    position: absolute !important;
                    bottom: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    background: linear-gradient(to top, rgba(231, 76, 60, 0.95), rgba(231, 76, 60, 0.8)) !important;
                    color: #fff !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    text-align: center !important;
                    padding: 4px 6px !important;
                    z-index: 10 !important;
                }

                .detail-leaving-badge {
                    display: inline-block !important;
                    background: #e74c3c !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    padding: 4px 10px !important;
                    border-radius: 4px !important;
                    margin: 10px 0 !important;
                }

                /* Media Management responsive */
                @media (max-width: 768px) {
                    #mediaManagementModalContent {
                        max-height: 95vh;
                        margin-top: 10px;
                    }

                    #mediaManagementControls {
                        flex-direction: column;
                    }

                    #mediaSearchInput {
                        width: 100%;
                    }

                    .media-list-table th,
                    .media-list-table td {
                        padding: 6px 8px;
                        font-size: 11px;
                    }

                    .media-item-image {
                        width: 30px;
                        height: 45px;
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
            const self = this;
            const stars = document.querySelectorAll('.ratings-plugin-star');
            const popup = document.getElementById('ratingsPluginPopup');
            const starsContainer = document.getElementById('ratingsPluginStars');

            stars.forEach(star => {
                star.addEventListener('click', () => {
                    const rating = parseInt(star.getAttribute('data-rating'));
                    // Check if clicking the same rating - toggle off
                    if (self.currentUserRating === rating) {
                        self.deleteRating(itemId);
                    } else {
                        self.submitRating(itemId, rating);
                    }
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
                    // Track user's current rating for toggle-off feature
                    self.currentUserRating = stats.UserRating || 0;
                    self.updateStarDisplay(stats.UserRating || 0);

                    let statsHtml = '';
                    if (stats.TotalRatings > 0) {
                        statsHtml = `<span class="ratings-plugin-average">${stats.AverageRating.toFixed(1)}/10</span> - ${stats.TotalRatings} rating${stats.TotalRatings !== 1 ? 's' : ''}`;
                        if (stats.UserRating) {
                            statsHtml += `<div class="ratings-plugin-your-rating">Your rating: ${stats.UserRating}/10 <span class="ratings-plugin-remove-hint">(click to remove)</span></div>`;
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

                    // Update current rating for toggle-off feature
                    self.currentUserRating = rating;

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
         * Delete rating (toggle off)
         */
        deleteRating: function (itemId) {
            const self = this;

            if (!window.ApiClient) {
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Rating`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestOptions = {
                method: 'DELETE',
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
                    return response;
                })
                .then(function() {
                    // Clear the current rating
                    self.currentUserRating = 0;
                    self.updateStarDisplay(0);
                    self.loadRatings(itemId);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Rating removed');
                        });
                    }
                })
                .catch(function(err) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error removing rating: ' + err.message);
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
                btn.innerHTML = '<span class="btn-text">' + self.t('requestMedia') + '</span>';
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
         * Manages notification session lifecycle
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
                            console.log('RatingsPlugin: User changed from', lastUserId, 'to', currentUserId);

                            // Clear all cached data
                            self.clearRequestCache();

                            // Handle notification session lifecycle
                            if (lastUserId && !currentUserId) {
                                // User logged out - clear notification session and stop polling
                                console.log('RatingsPlugin: User logged out, clearing notification session');
                                self.clearNotificationSession();
                                self.stopNotificationPolling();
                            } else if (!lastUserId && currentUserId) {
                                // User just logged in - start new notification session
                                console.log('RatingsPlugin: User logged in, starting notification session');
                                self.startNotificationSession();
                                // Reinitialize notifications for new user
                                if (self.notificationsEnabled) {
                                    self.createNotificationContainer();
                                    self.startNotificationPolling();
                                } else {
                                    // Check if notifications are enabled and start if so
                                    self.initNotifications();
                                }
                            } else if (lastUserId && currentUserId && lastUserId !== currentUserId) {
                                // User switched accounts - clear old session and start new one
                                console.log('RatingsPlugin: User switched accounts, resetting notification session');
                                self.clearNotificationSession();
                                self.stopNotificationPolling();
                                self.startNotificationSession();
                                // Reinitialize notifications for new user
                                if (self.notificationsEnabled) {
                                    self.createNotificationContainer();
                                    self.startNotificationPolling();
                                } else {
                                    self.initNotifications();
                                }
                            }

                            lastUserId = currentUserId;

                            // Update badge for new user
                            const btn = document.getElementById('requestMediaBtn');
                            if (btn && currentUserId) {
                                self.updateRequestBadge(btn);
                            }

                            // Test notification button disabled - use TV app for testing
                            // const testBtn = document.getElementById('testNotificationBtn');
                            // if (testBtn) {
                            //     testBtn.remove();
                            // }
                            // if (currentUserId) {
                            //     self.initTestNotificationButton();
                            // }
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 2000);

                // Also listen for Jellyfin events if available
                if (window.Events) {
                    Events.on(ApiClient, 'authenticated', () => {
                        console.log('RatingsPlugin: Jellyfin authenticated event received');
                        self.clearRequestCache();
                        const btn = document.getElementById('requestMediaBtn');
                        if (btn) {
                            self.updateRequestBadge(btn);
                        }
                        // Start notification session on authentication
                        if (!self.notificationSessionUserId) {
                            self.startNotificationSession();
                            self.initNotifications();
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

                // Check config if search button should be shown
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.ShowSearchButton === false) {
                                return; // Don't create search field
                            }
                            createSearchField();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createSearchField();
                        });
                };

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
                        searchInput.autocomplete = 'off';
                        searchInput.setAttribute('autocomplete', 'off');
                        searchInput.setAttribute('autocorrect', 'off');
                        searchInput.setAttribute('autocapitalize', 'off');
                        searchInput.setAttribute('spellcheck', 'false');

                        // Create search icon
                        const searchIcon = document.createElement('span');
                        searchIcon.id = 'headerSearchIcon';
                        searchIcon.innerHTML = '🔍';

                        // Create dropdown container - append to body to avoid stacking context issues
                        const searchDropdown = document.createElement('div');
                        searchDropdown.id = 'searchDropdown';
                        document.body.appendChild(searchDropdown);

                        // Append elements to search container
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

                        // Real-time search with dropdown
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
                            const query = searchInput.value.trim();

                            if (!query) {
                                // Hide dropdown when empty
                                self.hideSearchDropdown();
                                return;
                            }

                            // Show loading state and position dropdown
                            searchDropdown.innerHTML = '<div class="dropdown-loading">Searching...</div>';
                            searchDropdown.classList.add('visible');
                            self.positionSearchDropdown();

                            searchTimeout = setTimeout(() => {
                                // Search entire library and show in dropdown
                                self.searchLibraryDropdown(query);
                            }, 300); // Debounce for performance
                        });

                        // Handle enter key - select first result
                        searchInput.addEventListener('keypress', function(e) {
                            if (e.key === 'Enter') {
                                const firstItem = searchDropdown.querySelector('.dropdown-item');
                                if (firstItem) {
                                    firstItem.click();
                                }
                            }
                        });

                        // Handle escape key - close dropdown
                        searchInput.addEventListener('keydown', function(e) {
                            if (e.key === 'Escape') {
                                self.hideSearchDropdown();
                                searchInput.blur();
                            }
                        });

                        // Handle icon click - clear search or focus
                        searchIcon.addEventListener('click', function() {
                            if (searchInput.value.trim()) {
                                searchInput.value = '';
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                                self.hideSearchDropdown();
                            } else {
                                searchInput.focus();
                            }
                        });

                        // Close dropdown when clicking outside
                        document.addEventListener('click', function(e) {
                            const dropdown = document.getElementById('searchDropdown');
                            if (!searchContainer.contains(e.target) && (!dropdown || !dropdown.contains(e.target))) {
                                self.hideSearchDropdown();
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
                                    self.hideSearchDropdown();
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

                // Try to create immediately (check config first)
                setTimeout(checkConfigAndCreate, 1500);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('headerSearchField')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

                // Listen for Jellyfin navigation events
                try {
                    if (window.Emby && window.Emby.Page && typeof Emby.Page.addEventListener === 'function') {
                        Emby.Page.addEventListener('pageshow', () => {
                            if (!document.getElementById('headerSearchField')) {
                                setTimeout(checkConfigAndCreate, 500);
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
         * Initialize notification toggle in header
         */
        initNotificationToggle: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('notificationToggle')) {
                    return;
                }

                // Check config if notifications are enabled
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.EnableNewMediaNotifications === false) {
                                return; // Don't create notification toggle
                            }
                            createNotificationToggle();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createNotificationToggle();
                        });
                };

                const createNotificationToggle = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('notificationToggle')) {
                            return;
                        }

                        // Create toggle container
                        const toggleContainer = document.createElement('div');
                        toggleContainer.id = 'notificationToggle';

                        // Create bell icon
                        const bellIcon = document.createElement('span');
                        bellIcon.id = 'notificationToggleIcon';

                        // Create toggle switch
                        const toggleSwitch = document.createElement('div');
                        toggleSwitch.id = 'notificationToggleSwitch';

                        // Create tooltip
                        const tooltip = document.createElement('div');
                        tooltip.id = 'notificationTooltip';
                        tooltip.textContent = 'Enable/disable new media notifications';

                        // Get saved preference (default to enabled)
                        const savedPref = localStorage.getItem('ratingsNotificationsEnabled');
                        const isEnabled = savedPref === null ? true : savedPref === 'true';

                        // Update visual state
                        const updateToggleState = (enabled) => {
                            bellIcon.innerHTML = enabled ? '🔔' : '🔕';
                            if (enabled) {
                                toggleSwitch.classList.add('enabled');
                            } else {
                                toggleSwitch.classList.remove('enabled');
                            }
                            // Store in localStorage for this user
                            localStorage.setItem('ratingsNotificationsEnabled', enabled.toString());
                            // Also store in self for notification checking
                            self.userNotificationsEnabled = enabled;
                        };

                        // Set initial state
                        updateToggleState(isEnabled);
                        self.userNotificationsEnabled = isEnabled;

                        // Handle click
                        toggleContainer.addEventListener('click', () => {
                            const currentState = self.userNotificationsEnabled;
                            updateToggleState(!currentState);
                        });

                        // Append elements
                        toggleContainer.appendChild(bellIcon);
                        toggleContainer.appendChild(toggleSwitch);
                        document.body.appendChild(tooltip); // Tooltip in body to avoid clipping

                        // Position tooltip on hover
                        toggleContainer.addEventListener('mouseenter', () => {
                            const rect = toggleContainer.getBoundingClientRect();
                            tooltip.style.top = (rect.bottom + 8) + 'px';
                            tooltip.style.left = (rect.left + rect.width / 2) + 'px';
                            tooltip.style.transform = 'translateX(-50%)';
                            tooltip.classList.add('show');
                        });

                        toggleContainer.addEventListener('mouseleave', () => {
                            tooltip.classList.remove('show');
                        });

                        // Append to header container
                        const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                        if (headerContainer) {
                            headerContainer.style.position = 'relative';
                            headerContainer.appendChild(toggleContainer);
                        } else {
                            document.body.appendChild(toggleContainer);
                        }

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    toggleContainer.classList.add('hidden');
                                } else {
                                    toggleContainer.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating notification toggle:', err);
                    }
                };

                // Try to create after a delay (check config first)
                setTimeout(checkConfigAndCreate, 1600);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('notificationToggle')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

            } catch (err) {
                console.error('Notification toggle initialization failed:', err);
            }
        },

        /**
         * Initialize latest media button (replaces Sync Play button)
         */
        initLatestMediaButton: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('latestMediaBtn')) {
                    return;
                }

                // Check config if latest media button is enabled
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.ShowLatestMediaButton === false) {
                                return; // Don't create button
                            }
                            createLatestMediaButton();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createLatestMediaButton();
                        });
                };

                const createLatestMediaButton = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('latestMediaBtn')) {
                            return;
                        }

                        // Find and hide the Sync Play button
                        const syncPlayBtn = document.querySelector('.headerSyncButton');
                        if (syncPlayBtn) {
                            syncPlayBtn.style.display = 'none';
                        }

                        // Create the latest media button
                        const btn = document.createElement('button');
                        btn.id = 'latestMediaBtn';
                        btn.className = 'headerButton headerButtonRight paper-icon-button-light';
                        btn.setAttribute('type', 'button');
                        btn.setAttribute('title', self.t('latestMedia'));
                        // Clock/new icon - represents "latest/recent"
                        btn.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/>
                        </svg>`;

                        // Create dropdown container
                        const dropdown = document.createElement('div');
                        dropdown.id = 'latestMediaDropdown';
                        document.body.appendChild(dropdown);

                        // Position dropdown below button
                        const positionDropdown = () => {
                            const rect = btn.getBoundingClientRect();
                            dropdown.style.top = (rect.bottom + 4) + 'px';
                            dropdown.style.right = (window.innerWidth - rect.right) + 'px';
                            dropdown.style.left = 'auto';
                        };

                        // Toggle dropdown on click
                        btn.addEventListener('click', (e) => {
                            e.preventDefault();
                            e.stopPropagation();

                            if (dropdown.classList.contains('visible')) {
                                dropdown.classList.remove('visible');
                            } else {
                                positionDropdown();
                                dropdown.classList.add('visible');
                                self.loadLatestMedia(dropdown);
                            }
                        });

                        // Close dropdown when clicking outside
                        document.addEventListener('click', (e) => {
                            if (!btn.contains(e.target) && !dropdown.contains(e.target)) {
                                dropdown.classList.remove('visible');
                            }
                        });

                        // Close dropdown on escape
                        document.addEventListener('keydown', (e) => {
                            if (e.key === 'Escape') {
                                dropdown.classList.remove('visible');
                            }
                        });

                        // Insert button in header - try to find headerRight or similar container
                        const headerRight = document.querySelector('.headerRight');
                        if (headerRight) {
                            // Insert at the beginning of headerRight
                            headerRight.insertBefore(btn, headerRight.firstChild);
                        } else {
                            // Fallback: find skinHeader and append
                            const skinHeader = document.querySelector('.skinHeader');
                            if (skinHeader) {
                                skinHeader.appendChild(btn);
                            } else {
                                document.body.appendChild(btn);
                            }
                        }

                        // Observe for Sync Play button appearing later (SPA navigation)
                        const observer = new MutationObserver(() => {
                            const syncBtn = document.querySelector('.headerSyncButton');
                            if (syncBtn && syncBtn.style.display !== 'none') {
                                syncBtn.style.display = 'none';
                            }
                        });
                        observer.observe(document.body, { childList: true, subtree: true });

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    btn.classList.add('hidden');
                                    dropdown.classList.remove('visible');
                                } else {
                                    btn.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating latest media button:', err);
                    }
                };

                // Try to create after a delay
                setTimeout(checkConfigAndCreate, 1700);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('latestMediaBtn')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

            } catch (err) {
                console.error('Latest media button initialization failed:', err);
            }
        },

        /**
         * Load latest media items into dropdown
         */
        loadLatestMedia: function (dropdown) {
            const self = this;

            // Show loading state
            dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-loading">${self.t('latestMediaLoading')}</div>`;

            if (!window.ApiClient) {
                dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaError')}</div>`;
                return;
            }

            const userId = ApiClient.getCurrentUserId();
            const baseUrl = ApiClient.serverAddress();

            // Fetch latest items using Jellyfin's Items endpoint
            // Get movies, series sorted by DateCreated descending
            fetch(`${baseUrl}/Users/${userId}/Items?SortBy=DateCreated&SortOrder=Descending&IncludeItemTypes=Movie,Series&Recursive=true&Limit=50&Fields=PrimaryImageAspectRatio,Genres,ProductionYear,DateCreated`, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'X-Emby-Authorization': ApiClient._serverInfo?.AccessToken ?
                        `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${ApiClient._deviceId}", Version="${ApiClient._appVersion}", Token="${ApiClient._serverInfo.AccessToken}"` : ''
                }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to fetch');
                return response.json();
            })
            .then(data => {
                if (!data.Items || data.Items.length === 0) {
                    dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaEmpty')}</div>`;
                    return;
                }

                let html = `<div class="latest-header">${self.t('latestMedia')}</div>`;

                // Helper function to format time ago
                const formatTimeAgo = (dateString) => {
                    if (!dateString) return '';
                    const date = new Date(dateString);
                    const now = new Date();
                    const diffMs = now - date;
                    const diffMins = Math.floor(diffMs / 60000);
                    const diffHours = Math.floor(diffMs / 3600000);
                    const diffDays = Math.floor(diffMs / 86400000);

                    if (diffMins < 1) return self.t('timeJustNow');
                    if (diffMins < 60) return `${diffMins} ${self.t('timeMinutes')} ${self.t('timeAgo')}`;
                    if (diffHours < 24) return `${diffHours} ${self.t('timeHours')} ${self.t('timeAgo')}`;
                    return `${diffDays} ${self.t('timeDays')} ${self.t('timeAgo')}`;
                };

                // Helper function to clean title (remove IMDB IDs)
                const cleanTitle = (title) => {
                    if (!title) return 'Unknown';
                    // Remove patterns like [imdbid-tt1234567], [tt1234567], (tt1234567), [imdbid:tt1234567]
                    return title
                        .replace(/\s*\[imdbid[-:]?tt\d+\]/gi, '')
                        .replace(/\s*\[tt\d+\]/gi, '')
                        .replace(/\s*\(tt\d+\)/gi, '')
                        .trim();
                };

                data.Items.forEach(item => {
                    const itemId = item.Id;
                    const itemName = cleanTitle(item.Name);
                    const itemYear = item.ProductionYear || '';
                    const itemType = item.Type;
                    const genres = item.Genres || [];
                    const timeAgo = formatTimeAgo(item.DateCreated);

                    // Determine display type
                    let displayType = 'other';
                    let typeLabel = self.t('typeOther');

                    if (itemType === 'Movie') {
                        // Check if it's anime
                        if (genres.some(g => g.toLowerCase() === 'anime' || g.toLowerCase() === 'animation')) {
                            displayType = 'anime';
                            typeLabel = self.t('typeAnime');
                        } else {
                            displayType = 'movie';
                            typeLabel = self.t('typeMovie');
                        }
                    } else if (itemType === 'Series') {
                        // Check if it's anime
                        if (genres.some(g => g.toLowerCase() === 'anime' || g.toLowerCase() === 'animation')) {
                            displayType = 'anime';
                            typeLabel = self.t('typeAnime');
                        } else {
                            displayType = 'series';
                            typeLabel = self.t('typeSeries');
                        }
                    }

                    // Get image URL
                    const imageSrc = item.ImageTags && item.ImageTags.Primary
                        ? `${baseUrl}/Items/${itemId}/Images/Primary?maxHeight=96&tag=${item.ImageTags.Primary}`
                        : `${baseUrl}/Items/${itemId}/Images/Primary?maxHeight=96`;

                    html += `
                        <a href="#!/details?id=${itemId}" class="latest-item" data-item-id="${itemId}">
                            <img src="${imageSrc}" class="latest-item-image" alt="" onerror="this.style.visibility='hidden'"/>
                            <div class="latest-item-info">
                                <div class="latest-item-title">${self.escapeHtml(itemName)}</div>
                                <div class="latest-item-meta">
                                    <span class="latest-item-type ${displayType}">${typeLabel}</span>
                                    ${itemYear ? `<span class="latest-item-year">${itemYear}</span>` : ''}
                                    ${timeAgo ? `<span class="latest-item-time">${timeAgo}</span>` : ''}
                                </div>
                            </div>
                        </a>
                    `;
                });

                dropdown.innerHTML = html;

                // Add click handlers to close dropdown after navigation
                dropdown.querySelectorAll('.latest-item').forEach(item => {
                    item.addEventListener('click', () => {
                        dropdown.classList.remove('visible');
                    });
                });
            })
            .catch(err => {
                console.error('Failed to load latest media:', err);
                dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaError')}</div>`;
            });
        },

        // ===============================================
        // Media Management Functions (Admin Only)
        // ===============================================

        /**
         * Initialize media management button with retry logic for SPA navigation
         */
        initMediaManagementButtonWithRetry: function () {
            const self = this;
            let configChecked = false;
            let isAdminUser = false;
            let featureEnabled = false;

            const checkConfigAndAdmin = () => {
                if (!window.ApiClient) return Promise.resolve(false);

                return fetch(`${ApiClient.serverAddress()}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                    .then(response => response.json())
                    .then(config => {
                        featureEnabled = config.EnableMediaManagement === true;
                        return self.isAdmin();
                    })
                    .then(admin => {
                        isAdminUser = admin;
                        configChecked = true;
                        return featureEnabled && isAdminUser;
                    })
                    .catch(() => false);
            };

            const tryInjectMenuItem = () => {
                if (document.getElementById('mediaManagementMenuItem')) return;
                if (!configChecked) return;
                if (!featureEnabled || !isAdminUser) return;

                // Find the main drawer - Jellyfin's sidebar container
                const mainDrawer = document.querySelector('.mainDrawer');
                if (!mainDrawer) return;

                const scrollContainer = mainDrawer.querySelector('.scrollContainer') || mainDrawer;

                // Find Dashboard link by class or href - this is in the Server section
                let dashboardLink = scrollContainer.querySelector('.lnkManageServer');
                if (!dashboardLink) {
                    dashboardLink = scrollContainer.querySelector('a[href*="#/dashboard"]');
                }
                if (!dashboardLink) {
                    // Try finding by text content
                    scrollContainer.querySelectorAll('.navMenuOption').forEach(item => {
                        const text = (item.textContent || '').trim().toLowerCase();
                        if (text === 'dashboard') {
                            dashboardLink = item;
                        }
                    });
                }

                if (!dashboardLink) return;

                // Create the Media menu item using Jellyfin's own structure
                const menuItem = document.createElement('a');
                menuItem.id = 'mediaManagementMenuItem';
                menuItem.href = '#';
                menuItem.setAttribute('is', 'emby-linkbutton');
                menuItem.className = 'navMenuOption lnkMediaFolder';
                menuItem.innerHTML = `
                    <span class="material-icons navMenuOptionIcon">folder_special</span>
                    <span class="navMenuOptionText">${self.t('mediaManagement')}</span>
                `;

                // Insert after Dashboard link
                if (dashboardLink.nextSibling) {
                    dashboardLink.parentNode.insertBefore(menuItem, dashboardLink.nextSibling);
                } else {
                    dashboardLink.parentNode.appendChild(menuItem);
                }

                // Click handler to open modal
                menuItem.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    self.openMediaManagementModal();
                });
            };

            // Use MutationObserver to watch for drawer content changes
            const observer = new MutationObserver((mutations) => {
                tryInjectMenuItem();
            });

            // Initial check
            const startObserving = () => {
                checkConfigAndAdmin().then(shouldShow => {
                    if (shouldShow) {
                        // Create modal/dialog elements first
                        self.initMediaManagementButton();

                        tryInjectMenuItem();

                        // Observe body for drawer changes
                        observer.observe(document.body, {
                            childList: true,
                            subtree: true
                        });
                    }
                });
            };

            // Start after a delay to let page load
            setTimeout(startObserving, 2000);

            // Also retry on route changes (Jellyfin rebuilds the drawer)
            setInterval(() => {
                if (configChecked && featureEnabled && isAdminUser) {
                    tryInjectMenuItem();
                }
            }, 2000);
        },

        /**
         * Initialize Media Management Button - creates modal and dialog elements
         */
        initMediaManagementButton: function () {
            const self = this;

            // Create modal if not exists
            if (!document.getElementById('mediaManagementModal')) {
                    const modal = document.createElement('div');
                    modal.id = 'mediaManagementModal';
                    modal.innerHTML = `
                        <div id="mediaManagementModalContent">
                            <button id="mediaManagementModalClose" type="button">&times;</button>
                            <div id="mediaManagementModalTitle">${self.t('mediaManagementTitle')}</div>
                            <div id="mediaManagementControls">
                                <input type="text" id="mediaSearchInput" placeholder="${self.t('mediaSearch')}" />
                                <select id="mediaTypeFilter">
                                    <option value="">${self.t('mediaTypeAll')}</option>
                                    <option value="Movie">${self.t('mediaTypeMovie')}</option>
                                    <option value="Series">${self.t('mediaTypeSeries')}</option>
                                </select>
                                <select id="mediaSortBy">
                                    <option value="dateAdded">${self.t('mediaSortDateAdded')}</option>
                                    <option value="title">${self.t('mediaSortTitle')}</option>
                                    <option value="year">${self.t('mediaSortYear')}</option>
                                    <option value="rating">${self.t('mediaSortRating')}</option>
                                    <option value="playCount">${self.t('mediaSortPlayCount')}</option>
                                    <option value="size">${self.t('mediaSortSize')}</option>
                                </select>
                                <select id="mediaSortOrder">
                                    <option value="desc">↓</option>
                                    <option value="asc">↑</option>
                                </select>
                            </div>
                            <div id="mediaManagementBody">
                                <p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaLoading')}</p>
                            </div>
                            <div id="mediaManagementPagination"></div>
                        </div>
                    `;
                    document.body.appendChild(modal);

                    // Close button
                    document.getElementById('mediaManagementModalClose').addEventListener('click', (e) => {
                        e.preventDefault();
                        modal.classList.remove('show');
                    });

                    // Click outside to close
                    modal.addEventListener('click', (e) => {
                        if (e.target === modal) {
                            modal.classList.remove('show');
                        }
                    });

                    // Filter/sort change handlers
                    let searchTimeout;
                    document.getElementById('mediaSearchInput').addEventListener('input', () => {
                        clearTimeout(searchTimeout);
                        searchTimeout = setTimeout(() => self.loadMediaList(), 500);
                    });
                    document.getElementById('mediaTypeFilter').addEventListener('change', () => self.loadMediaList());
                    document.getElementById('mediaSortBy').addEventListener('change', () => self.loadMediaList());
                    document.getElementById('mediaSortOrder').addEventListener('change', () => self.loadMediaList());
                }

                // Create deletion dialog if not exists
                if (!document.getElementById('deletionDialog')) {
                    const deletionDialog = document.createElement('div');
                    deletionDialog.id = 'deletionDialog';
                    deletionDialog.innerHTML = `
                        <div id="deletionDialogContent">
                            <div id="deletionDialogTitle">${self.t('mediaScheduleDelete')}</div>
                            <div id="deletionDialogOptions">
                                <button class="deletion-option-btn" data-days="1">${self.t('media1Day')}</button>
                                <button class="deletion-option-btn" data-days="3">${self.t('media3Days')}</button>
                                <button class="deletion-option-btn" data-days="7">${self.t('media1Week')}</button>
                                <button class="deletion-option-btn" data-days="14">${self.t('media2Weeks')}</button>
                                <button class="deletion-cancel-btn">${self.t('mediaCancelDelete').replace('Cancel ', '')}</button>
                            </div>
                        </div>
                    `;
                    document.body.appendChild(deletionDialog);

                    // Deletion dialog option clicks
                    deletionDialog.querySelectorAll('.deletion-option-btn').forEach(btn => {
                        btn.addEventListener('click', () => {
                            const days = parseInt(btn.getAttribute('data-days'));
                            const itemId = deletionDialog.getAttribute('data-item-id');
                            if (itemId && days) {
                                self.scheduleDeletion(itemId, days);
                            }
                            deletionDialog.classList.remove('show');
                        });
                    });

                    deletionDialog.querySelector('.deletion-cancel-btn').addEventListener('click', () => {
                        deletionDialog.classList.remove('show');
                    });
                }
        },

        /**
         * Open Media Management Modal
         */
        openMediaManagementModal: function () {
            const modal = document.getElementById('mediaManagementModal');
            if (modal) {
                modal.classList.add('show');
                this.loadMediaList();
            }
        },

        /**
         * Current media list state
         */
        mediaListState: {
            page: 1,
            totalPages: 1
        },

        /**
         * Load media list from API
         */
        loadMediaList: function (page) {
            const self = this;
            const body = document.getElementById('mediaManagementBody');
            const pagination = document.getElementById('mediaManagementPagination');

            if (!body) return;

            const currentPage = page || 1;
            const search = document.getElementById('mediaSearchInput')?.value || '';
            const type = document.getElementById('mediaTypeFilter')?.value || '';
            const sortBy = document.getElementById('mediaSortBy')?.value || 'dateAdded';
            const sortOrder = document.getElementById('mediaSortOrder')?.value || 'desc';

            body.innerHTML = `<p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaLoading')}</p>`;

            const baseUrl = ApiClient.serverAddress();
            const userId = ApiClient.getCurrentUserId();
            const token = ApiClient.accessToken();

            let url = `${baseUrl}/Ratings/Media?page=${currentPage}&pageSize=50&sortBy=${sortBy}&sortOrder=${sortOrder}`;
            if (search) url += `&search=${encodeURIComponent(search)}`;
            if (type) url += `&type=${encodeURIComponent(type)}`;

            fetch(url, {
                method: 'GET',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to load media');
                return response.json();
            })
            .then(data => {
                self.mediaListState.page = data.CurrentPage;
                self.mediaListState.totalPages = data.TotalPages;
                self.renderMediaTable(data.Items, body);
                self.renderPagination(pagination, data);
            })
            .catch(err => {
                console.error('Error loading media:', err);
                body.innerHTML = `<p style="text-align: center; color: #e74c3c; padding: 20px;">${self.t('mediaError')}</p>`;
            });
        },

        /**
         * Render media table
         */
        renderMediaTable: function (items, container) {
            const self = this;

            if (!items || items.length === 0) {
                container.innerHTML = `<p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaNoResults')}</p>`;
                return;
            }

            const baseUrl = ApiClient.serverAddress();

            // Format file size
            const formatSize = (bytes) => {
                if (!bytes || bytes === 0) return '-';
                const gb = bytes / (1024 * 1024 * 1024);
                if (gb >= 1) return gb.toFixed(1) + ' ' + self.t('mediaGB');
                const mb = bytes / (1024 * 1024);
                return mb.toFixed(0) + ' ' + self.t('mediaMB');
            };

            // Format days until deletion
            const formatDaysUntil = (deleteAt) => {
                const now = new Date();
                const deleteDate = new Date(deleteAt);
                const diffMs = deleteDate - now;
                const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                if (diffDays <= 0) return 'Today';
                return diffDays + ' ' + self.t('mediaDays');
            };

            let html = `
                <table class="media-list-table">
                    <thead>
                        <tr>
                            <th></th>
                            <th>${self.t('mediaSortTitle')}</th>
                            <th>${self.t('mediaSortYear')}</th>
                            <th>${self.t('mediaSortRating')}</th>
                            <th>${self.t('mediaSortSize')}</th>
                            <th>Status</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
            `;

            items.forEach(item => {
                const imageUrl = item.ImageUrl ? baseUrl + item.ImageUrl : '';
                const hasScheduledDeletion = item.ScheduledDeletion && !item.ScheduledDeletion.IsCancelled;

                html += `
                    <tr>
                        <td>
                            ${imageUrl ? `<img src="${imageUrl}?maxWidth=80" class="media-item-image" alt="" />` : '<div class="media-item-image"></div>'}
                        </td>
                        <td>
                            <div class="media-item-title">
                                <a href="#/details?id=${item.ItemId}">${item.Title}</a>
                            </div>
                            <span class="media-item-type ${item.Type.toLowerCase()}">${item.Type}</span>
                        </td>
                        <td>${item.Year || '-'}</td>
                        <td class="media-item-rating">${item.AverageRating ? '★ ' + item.AverageRating.toFixed(1) : '-'}</td>
                        <td>${formatSize(item.FileSizeBytes)}</td>
                        <td>
                            ${hasScheduledDeletion
                                ? `<span class="media-item-scheduled">${self.t('mediaLeavingIn')} ${formatDaysUntil(item.ScheduledDeletion.DeleteAt)}</span>`
                                : '-'}
                        </td>
                        <td class="media-actions">
                            ${hasScheduledDeletion
                                ? `<button class="media-action-btn cancel" data-item-id="${item.ItemId}" data-action="cancel">${self.t('mediaCancelDelete')}</button>`
                                : `<button class="media-action-btn delete" data-item-id="${item.ItemId}" data-action="delete">${self.t('mediaScheduleDelete')}</button>`
                            }
                        </td>
                    </tr>
                `;
            });

            html += `</tbody></table>`;
            container.innerHTML = html;

            // Add action button handlers
            container.querySelectorAll('.media-action-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const itemId = btn.getAttribute('data-item-id');
                    const action = btn.getAttribute('data-action');
                    if (action === 'delete') {
                        self.showDeletionDialog(itemId);
                    } else if (action === 'cancel') {
                        self.cancelDeletion(itemId);
                    }
                });
            });
        },

        /**
         * Render pagination controls
         */
        renderPagination: function (container, data) {
            const self = this;
            if (!container) return;

            const { CurrentPage, TotalPages, TotalItems } = data;

            container.innerHTML = `
                <button ${CurrentPage <= 1 ? 'disabled' : ''} data-page="${CurrentPage - 1}">${self.t('mediaPrev')}</button>
                <span>${self.t('mediaPage')} ${CurrentPage} ${self.t('mediaOf')} ${TotalPages} (${TotalItems} items)</span>
                <button ${CurrentPage >= TotalPages ? 'disabled' : ''} data-page="${CurrentPage + 1}">${self.t('mediaNext')}</button>
            `;

            container.querySelectorAll('button').forEach(btn => {
                btn.addEventListener('click', () => {
                    const page = parseInt(btn.getAttribute('data-page'));
                    if (page >= 1 && page <= TotalPages) {
                        self.loadMediaList(page);
                    }
                });
            });
        },

        /**
         * Show deletion dialog
         */
        showDeletionDialog: function (itemId) {
            const dialog = document.getElementById('deletionDialog');
            if (dialog) {
                dialog.setAttribute('data-item-id', itemId);
                dialog.classList.add('show');
            }
        },

        /**
         * Schedule deletion for an item
         */
        scheduleDeletion: function (itemId, delayDays) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/Media/${itemId}/ScheduleDeletion?delayDays=${delayDays}`, {
                method: 'POST',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to schedule deletion');
                return response.json();
            })
            .then(() => {
                // Reload list and refresh badges
                self.loadMediaList(self.mediaListState.page);
                self.loadScheduledDeletions();
            })
            .catch(err => {
                console.error('Error scheduling deletion:', err);
                alert('Failed to schedule deletion');
            });
        },

        /**
         * Cancel scheduled deletion for an item
         */
        cancelDeletion: function (itemId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/Media/${itemId}/ScheduleDeletion`, {
                method: 'DELETE',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to cancel deletion');
                return response.json();
            })
            .then(() => {
                // Reload list and refresh badges
                self.loadMediaList(self.mediaListState.page);
                self.loadScheduledDeletions();
            })
            .catch(err => {
                console.error('Error cancelling deletion:', err);
                alert('Failed to cancel deletion');
            });
        },

        // ===============================================
        // Deletion Badges Functions (All Users)
        // ===============================================

        /**
         * Cached scheduled deletions
         */
        scheduledDeletionsCache: {},

        /**
         * Initialize deletion badges system
         */
        initDeletionBadges: function () {
            const self = this;

            // Load scheduled deletions initially
            setTimeout(() => {
                self.loadScheduledDeletions();
            }, 3000);

            // Refresh every 5 minutes
            setInterval(() => {
                self.loadScheduledDeletions();
            }, 5 * 60 * 1000);

            // Update badges on page changes
            setInterval(() => {
                self.updateDeletionBadges();
            }, 2000);
        },

        /**
         * Load scheduled deletions from API
         */
        loadScheduledDeletions: function () {
            const self = this;

            if (!window.ApiClient) return;

            const baseUrl = ApiClient.serverAddress();

            fetch(`${baseUrl}/Ratings/ScheduledDeletions`, {
                method: 'GET',
                credentials: 'include'
            })
            .then(response => response.json())
            .then(deletions => {
                // Build cache by itemId
                self.scheduledDeletionsCache = {};
                deletions.forEach(d => {
                    self.scheduledDeletionsCache[d.ItemId.toLowerCase()] = d;
                });
                // Update badges immediately
                self.updateDeletionBadges();
            })
            .catch(() => {});
        },

        /**
         * Update deletion badges on cards
         */
        updateDeletionBadges: function () {
            const self = this;

            // Format days until deletion
            const formatDaysUntil = (deleteAt) => {
                const now = new Date();
                const deleteDate = new Date(deleteAt);
                const diffMs = deleteDate - now;
                const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                if (diffDays <= 0) return self.t('mediaLeavingIn') + ' Today';
                return self.t('mediaLeavingIn') + ' ' + diffDays + ' ' + self.t('mediaDays');
            };

            // Find all media cards
            const cards = document.querySelectorAll('.card, [data-id]');

            cards.forEach(card => {
                // Try to get item ID from card
                let itemId = card.getAttribute('data-id');
                if (!itemId) {
                    // Try finding it in child elements
                    const link = card.querySelector('a[href*="id="]');
                    if (link) {
                        const match = link.href.match(/id=([a-f0-9-]+)/i);
                        if (match) itemId = match[1];
                    }
                }

                if (!itemId) return;

                // Check if this item has scheduled deletion
                const deletion = self.scheduledDeletionsCache[itemId.toLowerCase()];
                const existingBadge = card.querySelector('.card-leaving-badge');

                if (deletion) {
                    // Add or update badge
                    if (!existingBadge) {
                        const cardBox = card.querySelector('.cardBox, .cardContent') || card;
                        cardBox.style.position = 'relative';
                        const badge = document.createElement('div');
                        badge.className = 'card-leaving-badge';
                        badge.textContent = formatDaysUntil(deletion.DeleteAt);
                        cardBox.appendChild(badge);
                    } else {
                        existingBadge.textContent = formatDaysUntil(deletion.DeleteAt);
                    }
                } else {
                    // Remove badge if it exists
                    if (existingBadge) {
                        existingBadge.remove();
                    }
                }
            });

            // Also check detail page
            self.updateDetailPageBadge();
        },

        /**
         * Update badge on detail page
         */
        updateDetailPageBadge: function () {
            const self = this;

            // Get current item ID from URL
            const match = window.location.href.match(/id=([a-f0-9-]+)/i);
            if (!match) return;

            const itemId = match[1].toLowerCase();
            const deletion = self.scheduledDeletionsCache[itemId];

            // Find the detail header area
            const detailHeader = document.querySelector('.detailPagePrimaryContainer, .itemDetailPage');
            if (!detailHeader) return;

            const existingBadge = detailHeader.querySelector('.detail-leaving-badge');

            if (deletion) {
                const formatDaysUntil = (deleteAt) => {
                    const now = new Date();
                    const deleteDate = new Date(deleteAt);
                    const diffMs = deleteDate - now;
                    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                    if (diffDays <= 0) return self.t('mediaLeavingIn') + ' Today';
                    return self.t('mediaLeavingIn') + ' ' + diffDays + ' ' + self.t('mediaDays');
                };

                if (!existingBadge) {
                    const badge = document.createElement('div');
                    badge.className = 'detail-leaving-badge';
                    badge.textContent = formatDaysUntil(deletion.DeleteAt);
                    // Insert at top of detail header
                    detailHeader.insertBefore(badge, detailHeader.firstChild);
                } else {
                    existingBadge.textContent = formatDaysUntil(deletion.DeleteAt);
                }
            } else {
                if (existingBadge) {
                    existingBadge.remove();
                }
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
         * Hide search dropdown
         */
        hideSearchDropdown: function () {
            const dropdown = document.getElementById('searchDropdown');
            if (dropdown) {
                dropdown.classList.remove('visible');
                dropdown.innerHTML = '';
            }
        },

        /**
         * Position the search dropdown below the search field
         */
        positionSearchDropdown: function () {
            const searchField = document.getElementById('headerSearchField');
            const dropdown = document.getElementById('searchDropdown');
            if (!searchField || !dropdown) return;

            const rect = searchField.getBoundingClientRect();
            dropdown.style.top = (rect.bottom + 4) + 'px';
            dropdown.style.left = rect.left + 'px';
        },

        /**
         * Search library and show results in dropdown
         */
        searchLibraryDropdown: async function (query) {
            const self = this;
            const dropdown = document.getElementById('searchDropdown');

            try {
                if (!query || !window.ApiClient || !dropdown) {
                    console.log('RatingsPlugin: Search cancelled', { query, hasApiClient: !!window.ApiClient, hasDropdown: !!dropdown });
                    return;
                }

                // Position dropdown below search field
                self.positionSearchDropdown();

                const userId = ApiClient.getCurrentUserId();
                const baseUrl = ApiClient.serverAddress();

                // Use Jellyfin's search hints API - search entire library
                const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${encodeURIComponent(query)}&UserId=${userId}&IncludeItemTypes=Movie,Series&Limit=20`;

                const response = await fetch(searchUrl, {
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${ApiClient.deviceId()}", Version="10.11.0", Token="${ApiClient.accessToken()}"`
                    }
                });

                if (!response.ok) {
                    dropdown.innerHTML = '<div class="dropdown-empty">Search failed</div>';
                    return;
                }

                const data = await response.json();
                const searchItems = data.SearchHints || [];

                if (searchItems.length === 0) {
                    dropdown.innerHTML = '<div class="dropdown-empty">No results found</div>';
                    dropdown.classList.add('visible');
                    return;
                }

                // Build dropdown items
                let html = '';
                searchItems.forEach(item => {
                    const itemId = item.Id;
                    const itemName = item.Name || 'Unknown';
                    const itemType = item.Type || '';
                    const itemYear = item.ProductionYear || '';

                    // Build image URL
                    const imageSrc = `${baseUrl}/Items/${itemId}/Images/Primary?quality=90&maxWidth=100`;

                    html += `
                        <a href="#!/details?id=${itemId}" class="dropdown-item" data-item-id="${itemId}">
                            <img src="${imageSrc}" class="dropdown-item-image" alt="" onerror="this.style.display='none'"/>
                            <div class="dropdown-item-info">
                                <div class="dropdown-item-title">${self.escapeHtml(itemName)}</div>
                                <div class="dropdown-item-meta">
                                    <span class="dropdown-item-type">${itemType}</span>
                                    ${itemYear ? `<span class="dropdown-item-year">${itemYear}</span>` : ''}
                                </div>
                            </div>
                        </a>
                    `;
                });

                dropdown.innerHTML = html;
                dropdown.classList.add('visible');

                // Add click handlers to close dropdown after selection
                dropdown.querySelectorAll('.dropdown-item').forEach(item => {
                    item.addEventListener('click', () => {
                        // Clear search and close dropdown
                        const searchInput = document.getElementById('headerSearchInput');
                        const searchIcon = document.getElementById('headerSearchIcon');
                        if (searchInput) {
                            searchInput.value = '';
                        }
                        if (searchIcon) {
                            searchIcon.innerHTML = '🔍';
                            searchIcon.style.fontSize = '18px';
                        }
                        self.hideSearchDropdown();
                    });
                });

            } catch (error) {
                console.error('RatingsPlugin: Dropdown search error:', error);
                dropdown.innerHTML = '<div class="dropdown-empty">Search error</div>';
            }
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
                // Fetch config first to check EnableAdminRequests
                const baseUrl = ApiClient.serverAddress();
                fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                    .then(response => response.json())
                    .then(config => {
                        // Check if user is admin
                        self.checkIfAdmin().then(isAdmin => {
                            if (isAdmin) {
                                if (config.EnableAdminRequests) {
                                    // Admin can create requests - show tabs
                                    self.loadAdminWithTabs(config);
                                } else {
                                    // Admin cannot create requests - show manage only
                                    self.loadAdminInterface();
                                }
                            } else {
                                self.loadUserInterface();
                            }
                        }).catch(err => {
                            console.error('Error checking admin status:', err);
                            self.loadUserInterface();
                        });
                    })
                    .catch(err => {
                        console.error('Error fetching config:', err);
                        // Fallback: check admin and show appropriate interface
                        self.checkIfAdmin().then(isAdmin => {
                            if (isAdmin) {
                                self.loadAdminInterface();
                            } else {
                                self.loadUserInterface();
                            }
                        }).catch(() => {
                            self.loadUserInterface();
                        });
                    });
            } catch (err) {
                console.error('Error loading request interface:', err);
            }
        },

        /**
         * Load admin interface with tabs (Create Request / Manage Requests)
         */
        loadAdminWithTabs: function (config) {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            modalTitle.textContent = this.t('requestMedia') || 'Request Media';

            // Create tabs
            const tabsHtml = `
                <div class="admin-tabs">
                    <button class="admin-tab active" data-tab="create">${this.t('createRequest') || 'Create Request'}</button>
                    <button class="admin-tab" data-tab="manage">${this.t('manageRequests') || 'Manage Requests'}</button>
                </div>
                <div class="admin-tab-content" id="adminTabContent"></div>
            `;

            modalBody.innerHTML = tabsHtml;

            // Attach tab handlers
            const tabs = modalBody.querySelectorAll('.admin-tab');
            tabs.forEach(tab => {
                tab.addEventListener('click', (e) => {
                    // Remove active from all tabs
                    tabs.forEach(t => t.classList.remove('active'));
                    // Add active to clicked tab
                    e.target.classList.add('active');
                    // Load appropriate content
                    const tabName = e.target.getAttribute('data-tab');
                    if (tabName === 'create') {
                        self.renderUserInterfaceInTab(config);
                    } else {
                        self.renderAdminInterfaceInTab(config);
                    }
                });
            });

            // Load create tab by default
            this.renderUserInterfaceInTab(config);
        },

        /**
         * Render user interface inside tab content
         */
        renderUserInterfaceInTab: function (config) {
            const self = this;
            const tabContent = document.getElementById('adminTabContent');
            if (!tabContent) return;

            // Get custom texts or use defaults
            const windowDesc = config.RequestWindowDescription;
            const titleLabel = config.RequestTitleLabel || this.t('mediaTitle');
            const titlePlaceholder = config.RequestTitlePlaceholder || this.t('mediaTitlePlaceholder');
            const submitText = config.RequestSubmitButtonText || this.t('submitRequest');
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Field visibility and required settings
            const typeEnabled = config.RequestTypeEnabled !== false;
            const typeRequired = config.RequestTypeRequired === true;
            const typeLabel = config.RequestTypeLabel || this.t('type');

            const notesEnabled = config.RequestNotesEnabled !== false;
            const notesRequired = config.RequestNotesRequired === true;
            const notesLabel = config.RequestNotesLabel || this.t('additionalNotes');
            const notesPlaceholder = config.RequestNotesPlaceholder || this.t('notesPlaceholder');

            const imdbCodeEnabled = config.RequestImdbCodeEnabled !== false;
            const imdbCodeRequired = config.RequestImdbCodeRequired === true;
            const imdbCodeLabel = config.RequestImdbCodeLabel || 'IMDB Code';
            const imdbCodePlaceholder = config.RequestImdbCodePlaceholder || 'tt0448134';

            const imdbLinkEnabled = config.RequestImdbLinkEnabled !== false;
            const imdbLinkRequired = config.RequestImdbLinkRequired === true;
            const imdbLinkLabel = config.RequestImdbLinkLabel || 'IMDB Link';
            const imdbLinkPlaceholder = config.RequestImdbLinkPlaceholder || 'https://www.imdb.com/title/tt0448134/';

            // Parse custom fields
            let customFields = [];
            if (config.CustomRequestFields) {
                try {
                    customFields = JSON.parse(config.CustomRequestFields);
                } catch (e) {
                    console.error('Error parsing custom fields:', e);
                }
            }

            // Build custom fields HTML
            let customFieldsHtml = '';
            customFields.forEach((field, index) => {
                const fieldId = `customField_${index}`;
                const requiredAttr = field.required ? 'required' : '';
                const requiredMark = field.required ? ' *' : '';
                customFieldsHtml += `
                    <div class="request-input-group">
                        <label for="${fieldId}">${self.escapeHtml(field.name)}${requiredMark}</label>
                        <input type="text" id="${fieldId}" data-field-name="${self.escapeHtml(field.name)}" placeholder="${self.escapeHtml(field.placeholder || '')}" ${requiredAttr} />
                    </div>
                `;
            });

            // Language switch HTML (only if enabled)
            const langSwitchHtml = showLangSwitch ? `
                <div class="language-toggle-container">
                    <span class="lang-label">EN</span>
                    <label class="language-switch">
                        <input type="checkbox" id="languageToggle" ${this.currentLanguage === 'lt' ? 'checked' : ''}>
                        <span class="lang-slider"></span>
                    </label>
                    <span class="lang-label">LT</span>
                </div>
            ` : '';

            // Build description HTML (only if configured)
            const descriptionHtml = windowDesc ? `
                <div class="request-description">
                    <strong>${this.t('requestDescription')}</strong><br>
                    ${windowDesc}
                </div>
            ` : '';

            // Build Type field HTML (if enabled)
            const typeHtml = typeEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaType">${typeLabel}${typeRequired ? ' *' : ''}</label>
                    <select id="requestMediaType" ${typeRequired ? 'required' : ''}>
                        <option value="">${this.t('selectType')}</option>
                        <option value="Movie">${this.t('movie')}</option>
                        <option value="TV Series">${this.t('tvSeries')}</option>
                        <option value="Anime">${this.t('anime')}</option>
                        <option value="Documentary">${this.t('documentary')}</option>
                        <option value="Other">${this.t('other')}</option>
                    </select>
                </div>
            ` : '';

            // Build IMDB Code field HTML (if enabled)
            const imdbCodeHtml = imdbCodeEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbCode">${imdbCodeLabel}${imdbCodeRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbCode" placeholder="${imdbCodePlaceholder}" ${imdbCodeRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build IMDB Link field HTML (if enabled)
            const imdbLinkHtml = imdbLinkEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbLink">${imdbLinkLabel}${imdbLinkRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbLink" placeholder="${imdbLinkPlaceholder}" ${imdbLinkRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build Notes field HTML (if enabled)
            const notesHtml = notesEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaNotes">${notesLabel}${notesRequired ? ' *' : ''}</label>
                    <textarea id="requestMediaNotes" placeholder="${notesPlaceholder}" ${notesRequired ? 'required' : ''}></textarea>
                </div>
            ` : '';

            tabContent.innerHTML = `
                ${langSwitchHtml}
                ${descriptionHtml}
                <div class="request-input-group">
                    <label for="requestMediaTitle">${titleLabel} *</label>
                    <input type="text" id="requestMediaTitle" placeholder="${titlePlaceholder}" required />
                </div>
                ${typeHtml}
                ${imdbCodeHtml}
                ${imdbLinkHtml}
                ${customFieldsHtml}
                ${notesHtml}
                <button class="request-submit-btn" id="submitRequestBtn">${submitText}</button>
                <div class="user-requests-title">${this.t('yourRequests') || 'Your Requests'}</div>
                <div id="userRequestsList"><p style="text-align: center; color: #999;">${this.t('loadingRequests') || 'Loading...'}</p></div>
            `;

            // Attach language toggle handler (only if it exists)
            const langToggle = document.getElementById('languageToggle');
            if (langToggle) {
                langToggle.addEventListener('change', () => {
                    self.setLanguage(langToggle.checked ? 'lt' : 'en');
                    self.renderUserInterfaceInTab(config);
                });
            }

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
         * Render admin interface inside tab content
         */
        renderAdminInterfaceInTab: function (config) {
            const self = this;
            const tabContent = document.getElementById('adminTabContent');
            if (!tabContent) return;

            tabContent.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loading') + '</p>';

            // Reuse renderAdminInterface logic but target tabContent
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            this.fetchAllRequests().then(requests => {
                const langSwitchHtml = showLangSwitch ? `
                    <div class="language-toggle-container">
                        <span class="lang-label">EN</span>
                        <label class="language-switch">
                            <input type="checkbox" id="languageToggleAdmin" ${self.currentLanguage === 'lt' ? 'checked' : ''}>
                            <span class="lang-slider"></span>
                        </label>
                        <span class="lang-label">LT</span>
                    </div>
                ` : '';

                let html = langSwitchHtml;

                if (requests.length === 0) {
                    tabContent.innerHTML = html + '<div class="admin-request-empty">' + self.t('noRequestsYet') + '</div>';
                    const langToggle = document.getElementById('languageToggleAdmin');
                    if (langToggle) {
                        langToggle.addEventListener('change', () => {
                            self.setLanguage(langToggle.checked ? 'lt' : 'en');
                            self.renderAdminInterfaceInTab(config);
                        });
                    }
                    return;
                }

                // Group requests by status in order: processing > pending > snoozed > done > rejected
                const statusOrder = ['processing', 'pending', 'snoozed', 'done', 'rejected'];
                const categoryLabels = {
                    processing: self.t('categoryProcessing'),
                    pending: self.t('categoryPending'),
                    snoozed: self.t('categorySnoozed'),
                    done: self.t('categoryDone'),
                    rejected: self.t('categoryRejected')
                };

                // Categorize requests
                const categorized = {
                    processing: [],
                    pending: [],
                    snoozed: [],
                    done: [],
                    rejected: []
                };

                requests.forEach(request => {
                    // Check if request is snoozed (has SnoozedUntil date in the future)
                    const isSnoozed = request.SnoozedUntil && new Date(request.SnoozedUntil) > new Date();
                    if (isSnoozed) {
                        categorized.snoozed.push(request);
                    } else if (categorized[request.Status]) {
                        categorized[request.Status].push(request);
                    } else {
                        categorized.pending.push(request); // Fallback
                    }
                });

                // Build HTML for each category
                statusOrder.forEach(status => {
                    const categoryRequests = categorized[status];
                    if (categoryRequests.length === 0) return;

                    html += `<div class="admin-category-section" data-category="${status}">`;
                    html += `<h3 class="admin-category-header">${categoryLabels[status]} (${categoryRequests.length})</h3>`;
                    html += '<ul class="admin-request-list">';

                    categoryRequests.forEach(request => {
                        html += self.renderAdminRequestItem(request, status === 'snoozed');
                    });

                    html += '</ul></div>';
                });

                tabContent.innerHTML = html;

                // Attach language toggle handler
                const langToggle = document.getElementById('languageToggleAdmin');
                if (langToggle) {
                    langToggle.addEventListener('change', () => {
                        self.setLanguage(langToggle.checked ? 'lt' : 'en');
                        self.renderAdminInterfaceInTab(config);
                    });
                }

                // Attach status change handlers for buttons
                const statusBtns = tabContent.querySelectorAll('.admin-status-btn');
                statusBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.getAttribute('data-status');
                        const linkInput = tabContent.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        const rejectionInput = tabContent.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatusInTab(requestId, newStatus, mediaLink, rejectionReason, config);
                    });
                });

                // Attach status change handlers for dropdown
                const statusSelects = tabContent.querySelectorAll('.admin-status-select');
                statusSelects.forEach(select => {
                    select.addEventListener('change', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.value;
                        const linkInput = tabContent.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        const rejectionInput = tabContent.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatusInTab(requestId, newStatus, mediaLink, rejectionReason, config);
                    });
                });

                // Attach snooze handlers
                const snoozeBtns = tabContent.querySelectorAll('.admin-snooze-btn');
                snoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const dateInput = tabContent.querySelector(`.admin-snooze-date[data-request-id="${requestId}"]`);
                        if (dateInput && dateInput.value) {
                            self.snoozeRequest(requestId, dateInput.value, config);
                        } else {
                            if (window.require) {
                                require(['toast'], function(toast) {
                                    toast('Please select a snooze date');
                                });
                            }
                        }
                    });
                });

                // Attach unsnooze handlers
                const unsnoozeBtns = tabContent.querySelectorAll('.admin-unsnooze-btn');
                unsnoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        self.unsnoozeRequest(requestId, config);
                    });
                });

                // Attach delete handlers
                const deleteBtns = tabContent.querySelectorAll('.admin-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete'))) {
                            self.deleteRequestInTab(requestId, config);
                        }
                    });
                });
            }).catch(err => {
                console.error('Error loading requests:', err);
                tabContent.innerHTML = '<div class="admin-request-empty">' + self.t('errorLoading') + '</div>';
            });
        },

        /**
         * Update request status (for tab view)
         */
        updateRequestStatusInTab: function (requestId, status, mediaLink, rejectionReason, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();

            let url = `${baseUrl}/Ratings/Requests/${requestId}/Status?status=${status}`;
            if (mediaLink) url += `&mediaLink=${encodeURIComponent(mediaLink)}`;
            if (rejectionReason) url += `&rejectionReason=${encodeURIComponent(rejectionReason)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to update status');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Status updated');
                    });
                }
                self.renderAdminInterfaceInTab(config);
                self.updateRequestBadge();
            })
            .catch(err => {
                console.error('Error updating status:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error updating status');
                    });
                }
            });
        },

        /**
         * Delete request (for tab view)
         */
        deleteRequestInTab: function (requestId, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to delete');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('requestDeleted') || 'Request deleted');
                    });
                }
                self.renderAdminInterfaceInTab(config);
                self.updateRequestBadge();
            })
            .catch(err => {
                console.error('Error deleting request:', err);
            });
        },

        /**
         * Render a single admin request item
         */
        renderAdminRequestItem: function (request, isSnoozed) {
            const self = this;
            const details = [];
            if (request.Type) details.push(request.Type);
            if (request.Notes) details.push(request.Notes);
            const detailsText = details.join(' • ');

            let customFieldsHtml = '';
            if (request.CustomFields) {
                try {
                    const customFields = JSON.parse(request.CustomFields);
                    for (const [key, value] of Object.entries(customFields)) {
                        customFieldsHtml += `<div class="admin-request-custom-field"><strong>${self.escapeHtml(key)}:</strong> ${self.escapeHtml(value)}</div>`;
                    }
                } catch (e) {}
            }

            const createdAt = request.CreatedAt ? self.formatDateTime(request.CreatedAt) : self.t('unknown');
            const completedAt = request.CompletedAt ? self.formatDateTime(request.CompletedAt) : null;
            const hasLink = request.MediaLink && request.Status === 'done';
            const isRejected = request.Status === 'rejected';
            const statusText = isSnoozed ? self.t('snoozed') : self.t(request.Status);

            const rejectionDisplay = isRejected && request.RejectionReason
                ? `<div class="admin-rejection-reason">❌ ${self.escapeHtml(request.RejectionReason)}</div>`
                : '';

            let imdbHtml = '';
            if (request.ImdbCode) {
                imdbHtml += `<div class="admin-request-imdb"><strong>IMDB:</strong> ${self.escapeHtml(request.ImdbCode)}</div>`;
            }
            if (request.ImdbLink) {
                imdbHtml += `<div class="admin-request-imdb"><a href="${self.escapeHtml(request.ImdbLink)}" target="_blank" class="imdb-link">View on IMDB</a></div>`;
            }

            // Snooze info display
            let snoozeInfoHtml = '';
            if (isSnoozed && request.SnoozedUntil) {
                const snoozedUntilDate = self.formatDateTime(request.SnoozedUntil);
                snoozeInfoHtml = `<div class="admin-snooze-info">💤 ${self.t('snoozedUntil')}: ${snoozedUntilDate}</div>`;
            }

            // Snooze controls - show date picker and snooze button for non-snoozed, unsnooze for snoozed
            let snoozeControlsHtml = '';
            if (isSnoozed) {
                snoozeControlsHtml = `
                    <div class="admin-snooze-controls">
                        <button class="admin-unsnooze-btn" data-request-id="${request.Id}">⏰ ${self.t('unsnooze')}</button>
                    </div>
                `;
            } else if (request.Status !== 'done' && request.Status !== 'rejected') {
                // Only show snooze for pending/processing requests
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                const minDate = tomorrow.toISOString().split('T')[0];
                snoozeControlsHtml = `
                    <div class="admin-snooze-controls">
                        <input type="date" class="admin-snooze-date" data-request-id="${request.Id}" min="${minDate}" title="${self.t('snoozeDate')}">
                        <button class="admin-snooze-btn" data-request-id="${request.Id}">💤 ${self.t('snooze')}</button>
                    </div>
                `;
            }

            return `
                <li class="admin-request-item ${isSnoozed ? 'snoozed-item' : ''}" data-request-id="${request.Id}">
                    <div class="admin-request-title" title="${self.escapeHtml(request.Title)}">${self.escapeHtml(request.Title)}</div>
                    <div class="admin-request-user" title="${self.escapeHtml(request.Username)}">${self.escapeHtml(request.Username)}</div>
                    <div class="admin-request-details" title="${self.escapeHtml(detailsText)}">${self.escapeHtml(detailsText) || self.t('noDetails')}</div>
                    ${imdbHtml}
                    ${customFieldsHtml}
                    <div class="admin-request-time">
                        <span>📅 ${createdAt}</span>
                        ${completedAt ? `<span>✅ ${completedAt}</span>` : ''}
                        ${hasLink ? `<a href="${self.escapeHtml(request.MediaLink)}" class="request-media-link" target="_blank">${self.t('watchNow')}</a>` : ''}
                    </div>
                    ${snoozeInfoHtml}
                    ${rejectionDisplay}
                    <span class="admin-request-status-badge ${isSnoozed ? 'snoozed' : request.Status}">${statusText}</span>
                    <div class="admin-request-actions">
                        <button class="admin-status-btn pending" data-status="pending" data-request-id="${request.Id}">${self.t('pending')}</button>
                        <button class="admin-status-btn processing" data-status="processing" data-request-id="${request.Id}">${self.t('processing')}</button>
                        <button class="admin-status-btn done" data-status="done" data-request-id="${request.Id}">${self.t('done')}</button>
                        <button class="admin-status-btn rejected admin-reject-btn" data-status="rejected" data-request-id="${request.Id}">${self.t('rejected')}</button>
                        <button class="admin-delete-btn" data-request-id="${request.Id}">🗑️</button>
                    </div>
                    ${snoozeControlsHtml}
                    <select class="admin-status-select" data-request-id="${request.Id}">
                        <option value="pending" ${request.Status === 'pending' ? 'selected' : ''}>${self.t('pending')}</option>
                        <option value="processing" ${request.Status === 'processing' ? 'selected' : ''}>${self.t('processing')}</option>
                        <option value="done" ${request.Status === 'done' ? 'selected' : ''}>${self.t('done')}</option>
                        <option value="rejected" ${request.Status === 'rejected' ? 'selected' : ''}>${self.t('rejected')}</option>
                    </select>
                    <input type="text" class="admin-link-input" data-request-id="${request.Id}" placeholder="${self.t('mediaLinkPlaceholder')}" value="${self.escapeHtml(request.MediaLink || '')}">
                    <input type="text" class="admin-rejection-input" data-request-id="${request.Id}" placeholder="Rejection reason..." value="${self.escapeHtml(request.RejectionReason || '')}">
                    <button class="admin-delete-btn mobile-delete" data-request-id="${request.Id}">🗑️ ${self.t('delete')}</button>
                </li>
            `;
        },

        /**
         * Snooze a request until a specified date
         */
        snoozeRequest: function (requestId, snoozedUntil, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Snooze?snoozedUntil=${encodeURIComponent(snoozedUntil)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to snooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request snoozed');
                    });
                }
                self.renderAdminInterfaceInTab(config);
            })
            .catch(err => {
                console.error('Error snoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error snoozing request');
                    });
                }
            });
        },

        /**
         * Unsnooze a request
         */
        unsnoozeRequest: function (requestId, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Unsnooze`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to unsnooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request unsnoozed');
                    });
                }
                self.renderAdminInterfaceInTab(config);
            })
            .catch(err => {
                console.error('Error unsnoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error unsnoozing request');
                    });
                }
            });
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

            // Fetch config and render with custom settings
            const baseUrl = ApiClient.serverAddress();
            fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                .then(response => response.json())
                .then(config => {
                    self.renderUserInterface(modalBody, modalTitle, config);
                })
                .catch(() => {
                    // Fallback with default config
                    self.renderUserInterface(modalBody, modalTitle, {});
                });
        },

        /**
         * Render user interface with config
         */
        renderUserInterface: function (modalBody, modalTitle, config) {
            const self = this;

            // Get custom texts or use defaults
            const windowTitle = config.RequestWindowTitle || this.t('requestMedia');
            const windowDesc = config.RequestWindowDescription;
            const titleLabel = config.RequestTitleLabel || this.t('mediaTitle');
            const titlePlaceholder = config.RequestTitlePlaceholder || this.t('mediaTitlePlaceholder');
            const submitText = config.RequestSubmitButtonText || this.t('submitRequest');
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Field visibility and required settings
            const typeEnabled = config.RequestTypeEnabled !== false;
            const typeRequired = config.RequestTypeRequired === true;
            const typeLabel = config.RequestTypeLabel || this.t('type');

            const notesEnabled = config.RequestNotesEnabled !== false;
            const notesRequired = config.RequestNotesRequired === true;
            const notesLabel = config.RequestNotesLabel || this.t('additionalNotes');
            const notesPlaceholder = config.RequestNotesPlaceholder || this.t('notesPlaceholder');

            const imdbCodeEnabled = config.RequestImdbCodeEnabled !== false;
            const imdbCodeRequired = config.RequestImdbCodeRequired === true;
            const imdbCodeLabel = config.RequestImdbCodeLabel || 'IMDB Code';
            const imdbCodePlaceholder = config.RequestImdbCodePlaceholder || 'tt0448134';

            const imdbLinkEnabled = config.RequestImdbLinkEnabled !== false;
            const imdbLinkRequired = config.RequestImdbLinkRequired === true;
            const imdbLinkLabel = config.RequestImdbLinkLabel || 'IMDB Link';
            const imdbLinkPlaceholder = config.RequestImdbLinkPlaceholder || 'https://www.imdb.com/title/tt0448134/';

            // Parse custom fields
            let customFields = [];
            if (config.CustomRequestFields) {
                try {
                    customFields = JSON.parse(config.CustomRequestFields);
                } catch (e) {
                    console.error('Error parsing custom fields:', e);
                }
            }

            // Build custom fields HTML
            let customFieldsHtml = '';
            customFields.forEach((field, index) => {
                const fieldId = `customField_${index}`;
                const requiredAttr = field.required ? 'required' : '';
                const requiredMark = field.required ? ' *' : '';
                customFieldsHtml += `
                    <div class="request-input-group">
                        <label for="${fieldId}">${self.escapeHtml(field.name)}${requiredMark}</label>
                        <input type="text" id="${fieldId}" data-field-name="${self.escapeHtml(field.name)}" placeholder="${self.escapeHtml(field.placeholder || '')}" ${requiredAttr} />
                    </div>
                `;
            });

            // Language switch HTML (only if enabled)
            const langSwitchHtml = showLangSwitch ? `
                <div class="language-toggle-container">
                    <span class="lang-label">EN</span>
                    <label class="language-switch">
                        <input type="checkbox" id="languageToggle" ${this.currentLanguage === 'lt' ? 'checked' : ''}>
                        <span class="lang-slider"></span>
                    </label>
                    <span class="lang-label">LT</span>
                </div>
            ` : '';

            // Build description HTML (only if configured)
            const descriptionHtml = windowDesc ? `
                <div class="request-description">
                    <strong>${this.t('requestDescription')}</strong><br>
                    ${windowDesc}
                </div>
            ` : '';

            // Build Type field HTML (if enabled)
            const typeHtml = typeEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaType">${typeLabel}${typeRequired ? ' *' : ''}</label>
                    <select id="requestMediaType" ${typeRequired ? 'required' : ''}>
                        <option value="">${this.t('selectType')}</option>
                        <option value="Movie">${this.t('movie')}</option>
                        <option value="TV Series">${this.t('tvSeries')}</option>
                        <option value="Anime">${this.t('anime')}</option>
                        <option value="Documentary">${this.t('documentary')}</option>
                        <option value="Other">${this.t('other')}</option>
                    </select>
                </div>
            ` : '';

            // Build IMDB Code field HTML (if enabled)
            const imdbCodeHtml = imdbCodeEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbCode">${imdbCodeLabel}${imdbCodeRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbCode" placeholder="${imdbCodePlaceholder}" ${imdbCodeRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build IMDB Link field HTML (if enabled)
            const imdbLinkHtml = imdbLinkEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbLink">${imdbLinkLabel}${imdbLinkRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbLink" placeholder="${imdbLinkPlaceholder}" ${imdbLinkRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build Notes field HTML (if enabled)
            const notesHtml = notesEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaNotes">${notesLabel}${notesRequired ? ' *' : ''}</label>
                    <textarea id="requestMediaNotes" placeholder="${notesPlaceholder}" ${notesRequired ? 'required' : ''}></textarea>
                </div>
            ` : '';

            modalTitle.textContent = windowTitle;
            modalBody.innerHTML = `
                ${langSwitchHtml}
                ${descriptionHtml}
                <div class="request-input-group">
                    <label for="requestMediaTitle">${titleLabel} *</label>
                    <input type="text" id="requestMediaTitle" placeholder="${titlePlaceholder}" required />
                </div>
                ${typeHtml}
                ${imdbCodeHtml}
                ${imdbLinkHtml}
                ${customFieldsHtml}
                ${notesHtml}
                <button class="request-submit-btn" id="submitRequestBtn">${submitText}</button>
                <div class="user-requests-title">${this.t('yourRequests')}</div>
                <div id="userRequestsList"><p style="text-align: center; color: #999;">${this.t('loadingRequests')}</p></div>
            `;

            // Attach language toggle handler (only if it exists)
            const langToggle = document.getElementById('languageToggle');
            if (langToggle) {
                langToggle.addEventListener('change', () => {
                    self.setLanguage(langToggle.checked ? 'lt' : 'en');
                    self.loadUserInterface(); // Reload interface with new language
                });
            }

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

            listContainer.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loadingRequests') + '</p>';

            this.fetchAllRequests().then(requests => {
                // Filter to only current user's requests
                const userId = ApiClient.getCurrentUserId();
                const userRequests = requests.filter(r => r.UserId === userId);

                if (userRequests.length === 0) {
                    listContainer.innerHTML = '<p style="text-align: center; color: #999;">' + self.t('noRequests') + '</p>';
                    return;
                }

                let html = '<ul class="user-request-list">';
                userRequests.forEach(request => {
                    // Format timestamps
                    const createdAt = request.CreatedAt ? self.formatDateTime(request.CreatedAt) : '';
                    const completedAt = request.CompletedAt ? self.formatDateTime(request.CompletedAt) : null;
                    const hasLink = request.MediaLink && request.Status === 'done';
                    const isRejected = request.Status === 'rejected';
                    const statusText = self.t(request.Status);

                    // Parse custom fields if present
                    let customFieldsHtml = '';
                    if (request.CustomFields) {
                        try {
                            const customFields = JSON.parse(request.CustomFields);
                            for (const [key, value] of Object.entries(customFields)) {
                                customFieldsHtml += `<div class="user-request-custom-field"><strong>${self.escapeHtml(key)}:</strong> ${self.escapeHtml(value)}</div>`;
                            }
                        } catch (e) {
                            // Ignore parse errors
                        }
                    }

                    // Rejection reason
                    const rejectionHtml = isRejected && request.RejectionReason
                        ? `<div class="user-request-rejection-reason">❌ ${self.escapeHtml(request.RejectionReason)}</div>`
                        : '';

                    // IMDB info
                    let imdbHtml = '';
                    if (request.ImdbCode) {
                        imdbHtml += `<div class="user-request-imdb"><strong>IMDB:</strong> ${self.escapeHtml(request.ImdbCode)}</div>`;
                    }
                    if (request.ImdbLink) {
                        imdbHtml += `<div class="user-request-imdb"><a href="${self.escapeHtml(request.ImdbLink)}" target="_blank" class="imdb-link">View on IMDB</a></div>`;
                    }

                    // Edit/Delete buttons only for pending requests
                    const isPending = request.Status === 'pending';
                    const actionsHtml = isPending ? `
                        <div class="user-request-actions">
                            <button class="user-edit-btn" data-request-id="${request.Id}" data-request-title="${self.escapeHtml(request.Title)}" data-request-type="${self.escapeHtml(request.Type || '')}" data-request-notes="${self.escapeHtml(request.Notes || '')}" data-request-imdb-code="${self.escapeHtml(request.ImdbCode || '')}" data-request-imdb-link="${self.escapeHtml(request.ImdbLink || '')}" data-request-custom-fields="${self.escapeHtml(request.CustomFields || '')}">✏️ ${self.t('edit') || 'Edit'}</button>
                            <button class="user-delete-btn" data-request-id="${request.Id}">🗑️ ${self.t('delete') || 'Delete'}</button>
                        </div>
                    ` : '';

                    html += `
                        <li class="user-request-item">
                            <div class="user-request-info">
                                <div class="user-request-item-title">${self.escapeHtml(request.Title)}</div>
                                <div class="user-request-item-type">${request.Type ? self.escapeHtml(request.Type) : self.t('notSpecified')}</div>
                                ${imdbHtml}
                                ${customFieldsHtml}
                                <div class="user-request-time">📅 ${createdAt}${completedAt ? ` • ✅ ${completedAt}` : ''}</div>
                                ${rejectionHtml}
                                ${hasLink ? `<a href="${self.escapeHtml(request.MediaLink)}" class="request-media-link" target="_blank">${self.t('watchNow')}</a>` : ''}
                                ${actionsHtml}
                            </div>
                            <span class="user-request-status ${request.Status}">${statusText}</span>
                        </li>
                    `;
                });
                html += '</ul>';
                listContainer.innerHTML = html;

                // Attach edit button handlers
                const editBtns = listContainer.querySelectorAll('.user-edit-btn');
                editBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const title = e.target.getAttribute('data-request-title');
                        const type = e.target.getAttribute('data-request-type');
                        const notes = e.target.getAttribute('data-request-notes');
                        const imdbCode = e.target.getAttribute('data-request-imdb-code');
                        const imdbLink = e.target.getAttribute('data-request-imdb-link');
                        const customFields = e.target.getAttribute('data-request-custom-fields');
                        self.showEditRequestForm(requestId, title, type, notes, imdbCode, imdbLink, customFields);
                    });
                });

                // Attach delete button handlers
                const deleteBtns = listContainer.querySelectorAll('.user-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete') || 'Are you sure you want to delete this request?')) {
                            self.deleteUserRequest(requestId);
                        }
                    });
                });
            }).catch(err => {
                console.error('Error loading user requests:', err);
                listContainer.innerHTML = '<p style="text-align: center; color: #f44336;">' + self.t('errorLoading') + '</p>';
            });
        },

        /**
         * Show edit form for a request
         */
        showEditRequestForm: function (requestId, title, type, notes, imdbCode, imdbLink, customFields) {
            const self = this;

            // Fill the existing form with request data
            const titleInput = document.getElementById('requestMediaTitle');
            const typeSelect = document.getElementById('requestMediaType');
            const notesInput = document.getElementById('requestMediaNotes');
            const imdbCodeInput = document.getElementById('requestImdbCode');
            const imdbLinkInput = document.getElementById('requestImdbLink');

            if (titleInput) titleInput.value = title || '';
            if (typeSelect) typeSelect.value = type || '';
            if (notesInput) notesInput.value = notes || '';
            if (imdbCodeInput) imdbCodeInput.value = imdbCode || '';
            if (imdbLinkInput) imdbLinkInput.value = imdbLink || '';

            // Parse and fill custom fields
            if (customFields) {
                try {
                    const parsedFields = JSON.parse(customFields);
                    for (const [key, value] of Object.entries(parsedFields)) {
                        const customInput = document.querySelector(`[data-field-name="${key}"]`);
                        if (customInput) customInput.value = value || '';
                    }
                } catch (e) {
                    // Ignore parse errors
                }
            }

            // Change submit button to update mode
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.textContent = self.t('updateRequest') || 'Update Request';
                submitBtn.setAttribute('data-edit-mode', 'true');
                submitBtn.setAttribute('data-request-id', requestId);

                // Remove old listener and add new one
                const newBtn = submitBtn.cloneNode(true);
                submitBtn.parentNode.replaceChild(newBtn, submitBtn);
                newBtn.addEventListener('click', () => {
                    self.updateUserRequest(requestId);
                });
            }

            // Scroll to form
            if (titleInput) {
                titleInput.focus();
                titleInput.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        /**
         * Update user's own request
         */
        updateUserRequest: function (requestId) {
            const self = this;

            const title = document.getElementById('requestMediaTitle').value.trim();
            const typeEl = document.getElementById('requestMediaType');
            const type = typeEl ? typeEl.value.trim() : '';
            const notesEl = document.getElementById('requestMediaNotes');
            const notes = notesEl ? notesEl.value.trim() : '';
            const imdbCodeEl = document.getElementById('requestImdbCode');
            const imdbCode = imdbCodeEl ? imdbCodeEl.value.trim() : '';
            const imdbLinkEl = document.getElementById('requestImdbLink');
            const imdbLink = imdbLinkEl ? imdbLinkEl.value.trim() : '';

            if (!title) {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('titleRequired') || 'Title is required');
                    });
                }
                return;
            }

            // Collect custom fields
            const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
            const customFieldsObj = {};
            customFieldInputs.forEach(input => {
                const fieldName = input.getAttribute('data-field-name');
                const value = input.value.trim();
                if (fieldName && value) {
                    customFieldsObj[fieldName] = value;
                }
            });

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestData = {
                Title: title,
                Type: type,
                Notes: notes,
                CustomFields: Object.keys(customFieldsObj).length > 0 ? JSON.stringify(customFieldsObj) : '',
                ImdbCode: imdbCode,
                ImdbLink: imdbLink
            };

            fetch(url, {
                method: 'PUT',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                },
                body: JSON.stringify(requestData)
            })
            .then(response => {
                if (!response.ok) {
                    return response.text().then(text => {
                        throw new Error(text || 'Failed to update request');
                    });
                }
                return response.json();
            })
            .then(data => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('requestUpdated') || 'Request updated successfully');
                    });
                }

                // Reset form to create mode
                self.resetRequestForm();

                // Reload user requests
                self.loadUserRequests();
            })
            .catch(err => {
                console.error('Error updating request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(err.message || 'Error updating request');
                    });
                }
            });
        },

        /**
         * Delete user's own request
         */
        deleteUserRequest: function (requestId) {
            const self = this;

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
                        toast(self.t('requestDeleted') || 'Request deleted successfully');
                    });
                }

                // Reload user requests
                self.loadUserRequests();
            })
            .catch(err => {
                console.error('Error deleting request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error deleting request');
                    });
                }
            });
        },

        /**
         * Reset request form to create mode
         */
        resetRequestForm: function () {
            const self = this;

            // Clear form fields
            const titleInput = document.getElementById('requestMediaTitle');
            const typeSelect = document.getElementById('requestMediaType');
            const notesInput = document.getElementById('requestMediaNotes');
            const imdbCodeInput = document.getElementById('requestImdbCode');
            const imdbLinkInput = document.getElementById('requestImdbLink');

            if (titleInput) titleInput.value = '';
            if (typeSelect) typeSelect.value = '';
            if (notesInput) notesInput.value = '';
            if (imdbCodeInput) imdbCodeInput.value = '';
            if (imdbLinkInput) imdbLinkInput.value = '';

            // Clear custom fields
            const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
            customFieldInputs.forEach(input => {
                input.value = '';
            });

            // Reset submit button
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.textContent = self.t('submitRequest') || 'Submit Request';
                submitBtn.removeAttribute('data-edit-mode');
                submitBtn.removeAttribute('data-request-id');

                // Remove old listener and add new one for create
                const newBtn = submitBtn.cloneNode(true);
                submitBtn.parentNode.replaceChild(newBtn, submitBtn);
                newBtn.addEventListener('click', () => {
                    self.submitMediaRequest();
                });
            }
        },

        /**
         * Load admin interface for managing requests
         */
        loadAdminInterface: function () {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            modalTitle.textContent = this.t('manageRequests');
            modalBody.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loading') + '</p>';

            // Fetch config and then requests
            const baseUrl = ApiClient.serverAddress();
            fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                .then(response => response.json())
                .then(config => {
                    self.renderAdminInterface(modalBody, config);
                })
                .catch(() => {
                    self.renderAdminInterface(modalBody, {});
                });
        },

        /**
         * Render admin interface with config
         */
        renderAdminInterface: function (modalBody, config) {
            const self = this;
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Fetch all requests
            this.fetchAllRequests().then(requests => {
                // Language switch HTML (only if enabled)
                const langSwitchHtml = showLangSwitch ? `
                    <div class="language-toggle-container">
                        <span class="lang-label">EN</span>
                        <label class="language-switch">
                            <input type="checkbox" id="languageToggle" ${self.currentLanguage === 'lt' ? 'checked' : ''}>
                            <span class="lang-slider"></span>
                        </label>
                        <span class="lang-label">LT</span>
                    </div>
                ` : '';

                let html = langSwitchHtml;

                if (requests.length === 0) {
                    modalBody.innerHTML = html + '<div class="admin-request-empty">' + self.t('noRequestsYet') + '</div>';
                    // Attach language toggle handler
                    const langToggle = document.getElementById('languageToggle');
                    if (langToggle) {
                        langToggle.addEventListener('change', () => {
                            self.setLanguage(langToggle.checked ? 'lt' : 'en');
                            self.loadAdminInterface();
                        });
                    }
                    return;
                }

                // Group requests by status in order: processing > pending > snoozed > done > rejected
                const statusOrder = ['processing', 'pending', 'snoozed', 'done', 'rejected'];
                const categoryLabels = {
                    processing: self.t('categoryProcessing'),
                    pending: self.t('categoryPending'),
                    snoozed: self.t('categorySnoozed'),
                    done: self.t('categoryDone'),
                    rejected: self.t('categoryRejected')
                };

                // Categorize requests
                const categorized = {
                    processing: [],
                    pending: [],
                    snoozed: [],
                    done: [],
                    rejected: []
                };

                requests.forEach(request => {
                    // Check if request is snoozed (has SnoozedUntil date in the future)
                    const isSnoozed = request.SnoozedUntil && new Date(request.SnoozedUntil) > new Date();
                    if (isSnoozed) {
                        categorized.snoozed.push(request);
                    } else if (categorized[request.Status]) {
                        categorized[request.Status].push(request);
                    } else {
                        categorized.pending.push(request); // Fallback
                    }
                });

                // Build HTML for each category
                statusOrder.forEach(status => {
                    const categoryRequests = categorized[status];
                    if (categoryRequests.length === 0) return;

                    html += `<div class="admin-category-section" data-category="${status}">`;
                    html += `<h3 class="admin-category-header">${categoryLabels[status]} (${categoryRequests.length})</h3>`;
                    html += '<ul class="admin-request-list">';

                    categoryRequests.forEach(request => {
                        html += self.renderAdminRequestItem(request, status === 'snoozed');
                    });

                    html += '</ul></div>';
                });

                modalBody.innerHTML = html;

                // Attach language toggle handler (only if it exists)
                const langToggle = document.getElementById('languageToggle');
                if (langToggle) {
                    langToggle.addEventListener('change', () => {
                        self.setLanguage(langToggle.checked ? 'lt' : 'en');
                        self.loadAdminInterface();
                    });
                }

                // Attach status change handlers for buttons (desktop)
                const statusBtns = modalBody.querySelectorAll('.admin-status-btn');
                statusBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.getAttribute('data-status');
                        // Get the media link if marking as done
                        const linkInput = modalBody.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        // Get rejection reason if rejecting
                        const rejectionInput = modalBody.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatus(requestId, newStatus, mediaLink, rejectionReason);
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
                        // Get rejection reason if rejecting
                        const rejectionInput = modalBody.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatus(requestId, newStatus, mediaLink, rejectionReason);
                    });
                });

                // Attach snooze handlers
                const snoozeBtns = modalBody.querySelectorAll('.admin-snooze-btn');
                snoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const dateInput = modalBody.querySelector(`.admin-snooze-date[data-request-id="${requestId}"]`);
                        if (dateInput && dateInput.value) {
                            self.snoozeRequestModal(requestId, dateInput.value);
                        } else {
                            if (window.require) {
                                require(['toast'], function(toast) {
                                    toast('Please select a snooze date');
                                });
                            }
                        }
                    });
                });

                // Attach unsnooze handlers
                const unsnoozeBtns = modalBody.querySelectorAll('.admin-unsnooze-btn');
                unsnoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        self.unsnoozeRequestModal(requestId);
                    });
                });

                // Attach delete handlers
                const deleteBtns = modalBody.querySelectorAll('.admin-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete'))) {
                            self.deleteRequest(requestId);
                        }
                    });
                });
            }).catch(err => {
                console.error('Error loading requests:', err);
                modalBody.innerHTML = '<div class="admin-request-empty">' + self.t('errorLoading') + '</div>';
            });
        },

        /**
         * Snooze a request (modal version)
         */
        snoozeRequestModal: function (requestId, snoozedUntil) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Snooze?snoozedUntil=${encodeURIComponent(snoozedUntil)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to snooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request snoozed');
                    });
                }
                self.loadAdminInterface();
            })
            .catch(err => {
                console.error('Error snoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error snoozing request');
                    });
                }
            });
        },

        /**
         * Unsnooze a request (modal version)
         */
        unsnoozeRequestModal: function (requestId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Unsnooze`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to unsnooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request unsnoozed');
                    });
                }
                self.loadAdminInterface();
            })
            .catch(err => {
                console.error('Error unsnoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error unsnoozing request');
                    });
                }
            });
        },

        /**
         * Submit a new media request
         */
        submitMediaRequest: function () {
            const self = this;
            try {
                const title = document.getElementById('requestMediaTitle').value.trim();
                const typeEl = document.getElementById('requestMediaType');
                const type = typeEl ? typeEl.value.trim() : '';
                const notesEl = document.getElementById('requestMediaNotes');
                const notes = notesEl ? notesEl.value.trim() : '';
                const imdbCodeEl = document.getElementById('requestImdbCode');
                const imdbCode = imdbCodeEl ? imdbCodeEl.value.trim() : '';
                const imdbLinkEl = document.getElementById('requestImdbLink');
                const imdbLink = imdbLinkEl ? imdbLinkEl.value.trim() : '';

                if (!title) {
                    if (window.require) {
                        const msg = self.t('titleRequired');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    return;
                }

                // Type validation - only if element exists and is required
                if (typeEl && typeEl.hasAttribute('required') && !type) {
                    if (window.require) {
                        const msg = self.t('typeRequired');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    return;
                }

                // IMDB Code validation - only if element exists and is required
                if (imdbCodeEl && imdbCodeEl.hasAttribute('required') && !imdbCode) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: IMDB Code');
                        });
                    }
                    return;
                }

                // IMDB Link validation - only if element exists and is required
                if (imdbLinkEl && imdbLinkEl.hasAttribute('required') && !imdbLink) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: IMDB Link');
                        });
                    }
                    return;
                }

                // Notes validation - only if element exists and is required
                if (notesEl && notesEl.hasAttribute('required') && !notes) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: Notes');
                        });
                    }
                    return;
                }

                // Collect custom fields
                const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
                const customFieldsObj = {};
                let customFieldsValid = true;
                customFieldInputs.forEach(input => {
                    const fieldName = input.getAttribute('data-field-name');
                    const value = input.value.trim();
                    if (input.hasAttribute('required') && !value) {
                        customFieldsValid = false;
                        if (window.require) {
                            require(['toast'], function(toast) {
                                toast(`Please fill in: ${fieldName}`);
                            });
                        }
                    }
                    if (fieldName && value) {
                        customFieldsObj[fieldName] = value;
                    }
                });

                if (!customFieldsValid) {
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
                    Notes: notes,
                    CustomFields: Object.keys(customFieldsObj).length > 0 ? JSON.stringify(customFieldsObj) : '',
                    ImdbCode: imdbCode,
                    ImdbLink: imdbLink
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
                        const msg = self.t('requestSubmitted');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }

                    // Clear form
                    document.getElementById('requestMediaTitle').value = '';
                    if (typeEl) typeEl.value = '';
                    if (notesEl) notesEl.value = '';
                    // Clear IMDB fields
                    if (imdbCodeEl) imdbCodeEl.value = '';
                    if (imdbLinkEl) imdbLinkEl.value = '';
                    // Clear custom fields
                    customFieldInputs.forEach(input => {
                        input.value = '';
                    });

                    // Reload user's request list to show the new request
                    self.loadUserRequests();
                })
                .catch(err => {
                    console.error('Error submitting request:', err);
                    if (window.require) {
                        const msg = self.t('requestFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
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
        updateRequestStatus: function (requestId, newStatus, mediaLink, rejectionReason) {
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

                // Add rejectionReason if provided and status is rejected
                if (rejectionReason && newStatus === 'rejected') {
                    url += `&rejectionReason=${encodeURIComponent(rejectionReason)}`;
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
                        const msg = self.t('statusUpdated') + ': ' + self.t(newStatus);
                        require(['toast'], function(toast) {
                            toast(msg);
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
                        const msg = self.t('statusUpdateFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
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
         * Session-based: only shows notifications for media added AFTER user login
         */
        notificationsEnabled: false,
        lastNotificationCheck: null,
        notificationPollingInterval: null,
        shownNotificationIds: [],
        notificationSessionUserId: null, // Track which user session this is for

        /**
         * SessionStorage keys for notification persistence
         */
        NOTIFICATION_KEYS: {
            SESSION_START: 'ratingsNotificationSessionStart',    // When user logged in (ISO timestamp)
            SHOWN_IDS: 'ratingsShownNotificationIds',            // Array of shown notification IDs
            LAST_CHECK: 'ratingsLastNotificationCheck',          // Last poll timestamp
            SESSION_USER: 'ratingsNotificationSessionUser'       // User ID for this session
        },

        /**
         * Load notification session from sessionStorage
         */
        loadNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                const storedUserId = sessionStorage.getItem(this.NOTIFICATION_KEYS.SESSION_USER);

                // If user changed, clear session data
                if (currentUserId && storedUserId && currentUserId !== storedUserId) {
                    console.log('RatingsPlugin: User changed, clearing notification session');
                    this.clearNotificationSession();
                    return null;
                }

                // Load shown notification IDs
                const shownIdsJson = sessionStorage.getItem(this.NOTIFICATION_KEYS.SHOWN_IDS);
                if (shownIdsJson) {
                    try {
                        this.shownNotificationIds = JSON.parse(shownIdsJson);
                    } catch (e) {
                        this.shownNotificationIds = [];
                    }
                }

                // Load last check time
                const lastCheck = sessionStorage.getItem(this.NOTIFICATION_KEYS.LAST_CHECK);
                if (lastCheck) {
                    this.lastNotificationCheck = lastCheck;
                }

                // Load session start time
                const sessionStart = sessionStorage.getItem(this.NOTIFICATION_KEYS.SESSION_START);

                this.notificationSessionUserId = currentUserId;

                return sessionStart;
            } catch (err) {
                console.error('RatingsPlugin: Error loading notification session:', err);
                return null;
            }
        },

        /**
         * Save notification session to sessionStorage
         */
        saveNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                if (currentUserId) {
                    sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_USER, currentUserId);
                }
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SHOWN_IDS, JSON.stringify(this.shownNotificationIds));
                if (this.lastNotificationCheck) {
                    sessionStorage.setItem(this.NOTIFICATION_KEYS.LAST_CHECK, this.lastNotificationCheck);
                }
            } catch (err) {
                console.error('RatingsPlugin: Error saving notification session:', err);
            }
        },

        /**
         * Clear notification session data (on logout or user change)
         */
        clearNotificationSession: function () {
            try {
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SESSION_START);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SHOWN_IDS);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.LAST_CHECK);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SESSION_USER);
                this.shownNotificationIds = [];
                this.lastNotificationCheck = null;
                this.notificationSessionUserId = null;
            } catch (err) {
                console.error('RatingsPlugin: Error clearing notification session:', err);
            }
        },

        /**
         * Start a new notification session (on login)
         */
        startNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                if (!currentUserId) {
                    console.log('RatingsPlugin: Cannot start notification session - no user');
                    return;
                }

                const now = new Date().toISOString();
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_START, now);
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_USER, currentUserId);
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SHOWN_IDS, '[]');
                sessionStorage.setItem(this.NOTIFICATION_KEYS.LAST_CHECK, now);

                this.lastNotificationCheck = now;
                this.shownNotificationIds = [];
                this.notificationSessionUserId = currentUserId;

                console.log('RatingsPlugin: Started notification session at:', now, 'for user:', currentUserId);
            } catch (err) {
                console.error('RatingsPlugin: Error starting notification session:', err);
            }
        },

        /**
         * Initialize notifications system
         * Uses session-based tracking: only shows notifications for media added AFTER user login
         */
        initNotifications: function () {
            const self = this;

            // Check if user is logged in
            const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
            if (!currentUserId) {
                console.log('RatingsPlugin: No user logged in, skipping notification init');
                return;
            }

            // Check if notifications are enabled in config
            this.checkNotificationsEnabled().then(enabled => {
                self.notificationsEnabled = enabled;
                console.log('RatingsPlugin: Notifications enabled:', enabled);
                if (enabled) {
                    // Create notification container
                    self.createNotificationContainer();

                    // Load existing session or start new one
                    const sessionStart = self.loadNotificationSession();

                    if (sessionStart) {
                        // Existing session - use stored timestamp
                        // lastNotificationCheck was already loaded in loadNotificationSession
                        console.log('RatingsPlugin: Restored notification session, lastCheck:', self.lastNotificationCheck);
                    } else {
                        // New session - start from NOW (not 5 minutes ago!)
                        // This prevents old notifications from appearing
                        self.startNotificationSession();
                        console.log('RatingsPlugin: Started new notification session at:', self.lastNotificationCheck);
                    }

                    // Start polling for notifications
                    self.startNotificationPolling();

                    // Admin test button disabled - use TV app for testing
                    // self.initTestNotificationButton();
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
         * Only polls when user is logged in, persists state to sessionStorage
         */
        checkForNewNotifications: function () {
            const self = this;

            // Check if user has disabled notifications via toggle
            if (this.userNotificationsEnabled === false) {
                return;
            }

            if (!window.ApiClient) {
                console.log('RatingsPlugin: No ApiClient available for notifications');
                return;
            }

            // Check if user is logged in
            const currentUserId = ApiClient.getCurrentUserId();
            if (!currentUserId) {
                console.log('RatingsPlugin: No user logged in, stopping notification poll');
                this.stopNotificationPolling();
                return;
            }

            // Verify user hasn't changed mid-session
            if (this.notificationSessionUserId && this.notificationSessionUserId !== currentUserId) {
                console.log('RatingsPlugin: User changed during session, reinitializing notifications');
                this.clearNotificationSession();
                this.startNotificationSession();
            }

            const baseUrl = ApiClient.serverAddress();
            // Use session start time as fallback - NEVER use 5 minutes ago
            const since = this.lastNotificationCheck || new Date().toISOString();

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

                    // Persist session state to sessionStorage
                    self.saveNotificationSession();
                })
                .catch(err => {
                    console.error('RatingsPlugin: Error checking for notifications:', err);
                });
        },

        /**
         * Stop notification polling
         */
        stopNotificationPolling: function () {
            if (this.notificationPollingInterval) {
                clearInterval(this.notificationPollingInterval);
                this.notificationPollingInterval = null;
                console.log('RatingsPlugin: Stopped notification polling');
            }
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
                    const seriesName = notification.SeriesName ? this.escapeHtml(notification.SeriesName) : 'Series';
                    const seasonNum = notification.SeasonNumber;
                    const seasonText = (seasonNum !== null && seasonNum !== undefined && seasonNum > 0)
                        ? ` S${seasonNum.toString().padStart(2, '0')}` : '';
                    typeLabel = seriesName + seasonText + yearText;

                    // Handle grouped episode notifications
                    if (notification.EpisodeNumbers && notification.EpisodeNumbers.length > 1) {
                        const episodeDisplay = this.formatEpisodeRange(notification.EpisodeNumbers);
                        titleText = `New episodes: ${episodeDisplay}`;
                    } else {
                        const episodeNum = notification.EpisodeNumber;
                        // Check for valid episode number (not null, not undefined, not 0)
                        titleText = (episodeNum !== null && episodeNum !== undefined && episodeNum > 0)
                            ? `Episode ${episodeNum} is available`
                            : 'New episode available';
                    }
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
         * Format episode numbers into a readable range (e.g., "4-8" or "1, 3, 5")
         */
        formatEpisodeRange: function (episodeNumbers) {
            if (!episodeNumbers || episodeNumbers.length === 0) return '';
            if (episodeNumbers.length === 1) return episodeNumbers[0].toString();

            // Sort episodes
            const sorted = [...episodeNumbers].sort((a, b) => a - b);

            // Check if consecutive
            let isConsecutive = true;
            for (let i = 1; i < sorted.length; i++) {
                if (sorted[i] !== sorted[i - 1] + 1) {
                    isConsecutive = false;
                    break;
                }
            }

            if (isConsecutive) {
                return `${sorted[0]}-${sorted[sorted.length - 1]}`;
            } else {
                return sorted.join(', ');
            }
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
