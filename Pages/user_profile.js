/**
 * User Profile Page - Letterboxd-style profile with full customization
 */

(function() {
    'use strict';

    // State
    let profileUserId = null;
    let currentUserId = null;
    let isOwner = false;
    let profileData = null;
    let profileStyle = null;

    // Get user ID from URL params
    function getProfileUserId() {
        const params = new URLSearchParams(window.location.search);
        return params.get('userId') || params.get('id');
    }

    // Get auth header
    function getAuthHeader() {
        const token = window.ApiClient?.accessToken();
        return token ? { 'X-Emby-Token': token } : {};
    }

    // API helper
    async function api(endpoint, options = {}) {
        const url = window.ApiClient?.getUrl(endpoint) || `/Social/${endpoint}`;
        const response = await fetch(url, {
            ...options,
            headers: {
                'Content-Type': 'application/json',
                ...getAuthHeader(),
                ...options.headers
            }
        });
        if (!response.ok) {
            throw new Error(`API error: ${response.status}`);
        }
        return response.json();
    }

    // Format date
    function formatDate(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
    }

    // Format relative time
    function formatRelativeTime(dateStr) {
        const date = new Date(dateStr);
        const now = new Date();
        const diff = now - date;
        const minutes = Math.floor(diff / 60000);
        const hours = Math.floor(diff / 3600000);
        const days = Math.floor(diff / 86400000);

        if (minutes < 1) return 'just now';
        if (minutes < 60) return `${minutes}m ago`;
        if (hours < 24) return `${hours}h ago`;
        if (days < 7) return `${days}d ago`;
        return formatDate(dateStr);
    }

    // Render stars
    function renderStars(rating) {
        const fullStars = Math.floor(rating / 2);
        const halfStar = rating % 2 >= 1;
        let stars = '';
        for (let i = 0; i < fullStars; i++) stars += '★';
        if (halfStar) stars += '½';
        return stars || '☆';
    }

    // Get poster URL
    function getPosterUrl(item, height = 150) {
        if (!item) return '';
        const imageUrl = item.imageUrl || item.ImageUrl || item.CachedImageUrl;
        if (!imageUrl) return '';
        if (imageUrl.startsWith('http')) return imageUrl;
        return window.ApiClient?.getUrl(imageUrl + `?fillHeight=${height}&quality=96`) || imageUrl;
    }

    // Initialize profile
    async function initProfile() {
        try {
            profileUserId = getProfileUserId();

            // Get current user
            const currentUser = await api('Social/MyProfile');
            currentUserId = currentUser.userId;
            isOwner = currentUserId === profileUserId || !profileUserId;

            if (!profileUserId) {
                profileUserId = currentUserId;
            }

            // Load profile data
            await loadProfileData();
            await loadProfileStyle();

            // Setup UI
            setupTabs();
            setupActions();
            setupYearSelector();

            // Load content for active tab
            loadTabContent('profile');

        } catch (error) {
            console.error('Error initializing profile:', error);
            showError('Failed to load profile');
        }
    }

    // Load profile data
    async function loadProfileData() {
        try {
            profileData = await api(`Social/Profile/${profileUserId}/Stats/Full`);
            renderProfileHeader();
        } catch (error) {
            console.error('Error loading profile data:', error);
        }
    }

    // Load profile style
    async function loadProfileStyle() {
        try {
            profileStyle = await api(`Social/Profile/${profileUserId}/Style`);
            applyProfileStyle();
        } catch (error) {
            console.error('Error loading profile style:', error);
        }
    }

    // Render profile header
    function renderProfileHeader() {
        const data = profileData;

        // Avatar
        const avatar = document.getElementById('avatar');
        const avatarLetter = document.getElementById('avatarLetter');
        if (data.avatarUrl) {
            avatar.innerHTML = `<img src="${data.avatarUrl}" alt="${data.username}">`;
        } else {
            avatarLetter.textContent = (data.username || 'U')[0].toUpperCase();
        }

        // Online indicator
        const indicator = document.getElementById('onlineIndicator');
        indicator.className = `online-indicator ${data.isOnline ? 'online' : 'offline'}`;

        // Username and bio
        document.getElementById('username').textContent = data.username || 'Unknown';
        document.getElementById('bio').textContent = data.bio || 'No bio yet.';

        // Meta info
        document.getElementById('joinDate').textContent = `Joined ${formatDate(data.memberSince)}`;

        // Stats
        document.getElementById('statFilms').textContent = data.films || 0;
        document.getElementById('statSeries').textContent = data.series || 0;
        document.getElementById('statThisYear').textContent = data.thisYear || 0;
        document.getElementById('statLists').textContent = data.lists || 0;
        document.getElementById('statFollowing').textContent = data.following || 0;
        document.getElementById('statFollowers').textContent = data.followers || 0;
        document.getElementById('statLikes').textContent = data.profileLikes || 0;
    }

    // Apply profile style
    function applyProfileStyle() {
        if (!profileStyle) return;

        const root = document.documentElement;
        const style = profileStyle;

        // Background
        if (style.backgroundType === 'image' && style.backgroundImageUrl) {
            const headerBg = document.getElementById('headerBg');
            headerBg.style.backgroundImage = `url(${style.backgroundImageUrl})`;
            headerBg.style.filter = `blur(${style.backgroundBlur || 0}px)`;
        } else if (style.backgroundType === 'gradient' && style.backgroundGradient) {
            root.style.setProperty('--bg-secondary', style.backgroundGradient);
        } else if (style.backgroundColor) {
            root.style.setProperty('--bg-secondary', style.backgroundColor);
        }

        // Colors
        if (style.accentColor) root.style.setProperty('--accent', style.accentColor);
        if (style.usernameColor) document.getElementById('username').style.color = style.usernameColor;
        if (style.bioColor) document.getElementById('bio').style.color = style.bioColor;
        if (style.cardBackgroundColor) root.style.setProperty('--bg-card', style.cardBackgroundColor);
        if (style.cardBorderColor) root.style.setProperty('--border-color', style.cardBorderColor);

        // Font
        if (style.fontFamily) {
            document.body.style.fontFamily = style.fontFamily;
        }
    }

    // Setup action buttons
    function setupActions() {
        const container = document.getElementById('actionsContainer');
        container.innerHTML = '';

        if (isOwner) {
            container.innerHTML = `
                <button class="btn btn-primary" onclick="openSettings()">Edit Profile</button>
            `;
        } else {
            const data = profileData;
            container.innerHTML = `
                <button class="btn ${data.isFollowing ? 'btn-secondary' : 'btn-primary'}" id="followBtn">
                    ${data.isFollowing ? 'Following' : 'Follow'}
                </button>
                <button class="btn btn-like ${data.hasLikedProfile ? 'liked' : ''}" id="likeBtn">
                    ♥ ${data.profileLikes || 0}
                </button>
            `;

            // Follow button
            document.getElementById('followBtn').addEventListener('click', async () => {
                try {
                    if (data.isFollowing) {
                        await api(`Social/Follow/${profileUserId}`, { method: 'DELETE' });
                        data.isFollowing = false;
                    } else {
                        await api(`Social/Follow/${profileUserId}`, { method: 'POST' });
                        data.isFollowing = true;
                    }
                    setupActions();
                    loadProfileData();
                } catch (error) {
                    console.error('Follow error:', error);
                }
            });

            // Like button
            document.getElementById('likeBtn').addEventListener('click', async () => {
                try {
                    if (data.hasLikedProfile) {
                        await api(`Social/Profile/${profileUserId}/Like`, { method: 'DELETE' });
                        data.hasLikedProfile = false;
                    } else {
                        await api(`Social/Profile/${profileUserId}/Like`, { method: 'POST' });
                        data.hasLikedProfile = true;
                    }
                    setupActions();
                    loadProfileData();
                } catch (error) {
                    console.error('Like error:', error);
                }
            });
        }
    }

    // Setup tabs
    function setupTabs() {
        const tabs = document.querySelectorAll('.tab-btn');
        const links = document.querySelectorAll('.section-link');
        const statItems = document.querySelectorAll('.stat-item[data-tab]');

        tabs.forEach(tab => {
            tab.addEventListener('click', () => {
                const tabId = tab.dataset.tab;
                switchTab(tabId);
            });
        });

        links.forEach(link => {
            link.addEventListener('click', () => {
                const tabId = link.dataset.tab;
                if (tabId) switchTab(tabId);
            });
        });

        statItems.forEach(item => {
            item.addEventListener('click', () => {
                const tabId = item.dataset.tab;
                if (tabId) switchTab(tabId);
            });
        });
    }

    // Switch tab
    function switchTab(tabId) {
        // Update tab buttons
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.classList.toggle('active', btn.dataset.tab === tabId);
        });

        // Update content
        document.querySelectorAll('.tab-content').forEach(content => {
            content.classList.toggle('active', content.id === `tab-${tabId}`);
        });

        // Load content
        loadTabContent(tabId);
    }

    // Load tab content
    async function loadTabContent(tabId) {
        switch (tabId) {
            case 'profile':
                loadFavorites();
                loadRecentActivity();
                loadRecentReviews();
                loadSidebarWidgets();
                break;
            case 'films':
                loadFilms();
                break;
            case 'series':
                loadSeries();
                break;
            case 'reviews':
                loadAllReviews();
                break;
            case 'lists':
                loadLists();
                break;
            case 'activity':
                loadActivityFeed();
                break;
            case 'diary':
                loadDiary();
                break;
            case 'watchlist':
                loadWatchlist();
                break;
            case 'stats':
                loadStats();
                break;
        }
    }

    // Load favorites
    async function loadFavorites() {
        try {
            const lists = await api(`Social/Profile/${profileUserId}/Lists`);

            // Find favorites lists
            const movieFavorites = lists.lists?.find(l => l.isFavorites && l.listType === 'Movies');
            const seriesFavorites = lists.lists?.find(l => l.isFavorites && l.listType === 'Series');

            // Render movie favorites
            const filmsContainer = document.getElementById('favoriteFilms');
            if (movieFavorites) {
                const list = await api(`Social/Lists/${movieFavorites.id}`);
                filmsContainer.innerHTML = list.items?.map(item => `
                    <div class="poster-item" onclick="navigateToItem('${item.itemId || ''}')">
                        <img class="poster-image" src="${getPosterUrl(item)}" alt="${item.title}" onerror="this.style.display='none'">
                    </div>
                `).join('') || '<div class="empty-state">No favorites yet</div>';
            } else {
                filmsContainer.innerHTML = '<div class="empty-state">No favorites yet</div>';
            }

            // Render series favorites
            const seriesContainer = document.getElementById('favoriteSeries');
            if (seriesFavorites) {
                const list = await api(`Social/Lists/${seriesFavorites.id}`);
                seriesContainer.innerHTML = list.items?.map(item => `
                    <div class="poster-item" onclick="navigateToItem('${item.itemId || ''}')">
                        <img class="poster-image" src="${getPosterUrl(item)}" alt="${item.title}" onerror="this.style.display='none'">
                    </div>
                `).join('') || '<div class="empty-state">No favorites yet</div>';
            } else {
                seriesContainer.innerHTML = '<div class="empty-state">No favorites yet</div>';
            }
        } catch (error) {
            console.error('Error loading favorites:', error);
        }
    }

    // Load recent activity
    async function loadRecentActivity() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings?limit=8`);
            const container = document.getElementById('recentActivity');

            if (!ratings.ratings?.length) {
                container.innerHTML = '<div class="empty-state">No activity yet</div>';
                return;
            }

            container.innerHTML = ratings.ratings.map(rating => `
                <div class="activity-item" onclick="navigateToItem('${rating.itemId}')">
                    <img class="activity-poster" src="${getPosterUrl(rating)}" alt="${rating.title}" onerror="this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22/>'">
                    <div class="activity-stars">${renderStars(rating.rating)}</div>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading recent activity:', error);
            document.getElementById('recentActivity').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load recent reviews
    async function loadRecentReviews() {
        try {
            const reviews = await api(`Social/Profile/${profileUserId}/Reviews?limit=3`);
            const container = document.getElementById('recentReviews');

            if (!reviews.reviews?.length) {
                container.innerHTML = '<div class="empty-state">No reviews yet</div>';
                return;
            }

            container.innerHTML = reviews.reviews.map(review => renderReviewCard(review)).join('');
        } catch (error) {
            console.error('Error loading recent reviews:', error);
            // Try alternative endpoint
            try {
                const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
                const reviews = ratings.ratings?.filter(r => r.reviewText) || [];
                const container = document.getElementById('recentReviews');

                if (!reviews.length) {
                    container.innerHTML = '<div class="empty-state">No reviews yet</div>';
                    return;
                }

                container.innerHTML = reviews.slice(0, 3).map(review => renderReviewCard(review)).join('');
            } catch (e) {
                document.getElementById('recentReviews').innerHTML = '<div class="empty-state">Failed to load</div>';
            }
        }
    }

    // Render review card
    function renderReviewCard(review) {
        return `
            <div class="review-card">
                <img class="review-poster" src="${getPosterUrl(review, 120)}" alt="${review.title}" onerror="this.style.display='none'">
                <div class="review-content">
                    <div class="review-header">
                        <span class="review-title">${review.title || 'Unknown'}</span>
                        <span class="review-year">${review.year || ''}</span>
                    </div>
                    <div class="review-meta">
                        <span class="review-rating">${renderStars(review.rating)}</span>
                        <span>Watched ${formatRelativeTime(review.createdAt || review.reviewDate)}</span>
                    </div>
                    <p class="review-text">${review.reviewText || ''}</p>
                    <div class="review-actions">
                        <button class="review-action ${review.userLiked === true ? 'liked' : ''}" onclick="likeReview('${profileUserId}', '${review.itemId}', true)">
                            ♥ ${review.likes || review.likeCount || 0}
                        </button>
                    </div>
                </div>
            </div>
        `;
    }

    // Load sidebar widgets
    async function loadSidebarWidgets() {
        loadYearStats(new Date().getFullYear());
        loadWatchlistPreview();
        loadDiaryPreview();
        loadRatingDistribution();
    }

    // Setup year selector
    function setupYearSelector() {
        const selector = document.getElementById('yearSelector');
        const currentYear = new Date().getFullYear();
        const startYear = 2020;

        for (let year = currentYear; year >= startYear; year--) {
            const option = document.createElement('option');
            option.value = year;
            option.textContent = year;
            selector.appendChild(option);
        }

        selector.addEventListener('change', (e) => {
            loadYearStats(parseInt(e.target.value));
        });
    }

    // Load year stats
    async function loadYearStats(year) {
        try {
            const stats = await api(`Social/Profile/${profileUserId}/Stats/Year/${year}`);
            const container = document.getElementById('yearStats');

            container.innerHTML = `
                <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 10px; text-align: center;">
                    <div>
                        <div style="font-size: 24px; font-weight: bold;">${stats.films || 0}</div>
                        <div style="font-size: 11px; color: var(--text-muted);">FILMS</div>
                    </div>
                    <div>
                        <div style="font-size: 24px; font-weight: bold;">${stats.series || 0}</div>
                        <div style="font-size: 11px; color: var(--text-muted);">SERIES</div>
                    </div>
                    <div>
                        <div style="font-size: 24px; font-weight: bold;">${stats.hoursWatched || 0}</div>
                        <div style="font-size: 11px; color: var(--text-muted);">HOURS</div>
                    </div>
                    <div>
                        <div style="font-size: 24px; font-weight: bold;">${stats.reviewsWritten || 0}</div>
                        <div style="font-size: 11px; color: var(--text-muted);">REVIEWS</div>
                    </div>
                </div>
            `;
        } catch (error) {
            console.error('Error loading year stats:', error);
        }
    }

    // Load watchlist preview
    async function loadWatchlistPreview() {
        try {
            const lists = await api(`Social/Profile/${profileUserId}/Lists`);
            const watchlist = lists.lists?.find(l => l.isWatchlist);

            if (watchlist) {
                const list = await api(`Social/Lists/${watchlist.id}`);
                const container = document.getElementById('watchlistPreview');
                document.getElementById('watchlistCount').textContent = list.items?.length || 0;

                container.innerHTML = list.items?.slice(0, 8).map(item => `
                    <img class="mini-poster" src="${getPosterUrl(item, 80)}" alt="${item.title}" onerror="this.style.display='none'">
                `).join('') || '';
            }
        } catch (error) {
            console.error('Error loading watchlist preview:', error);
        }
    }

    // Load diary preview
    async function loadDiaryPreview() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings?limit=10`);
            const container = document.getElementById('diaryPreview');

            if (!ratings.ratings?.length) {
                container.innerHTML = '<div style="color: var(--text-muted);">No entries</div>';
                return;
            }

            // Group by month
            const grouped = {};
            ratings.ratings.forEach(r => {
                const date = new Date(r.createdAt);
                const monthKey = date.toLocaleDateString('en-US', { month: 'short' });
                if (!grouped[monthKey]) grouped[monthKey] = [];
                grouped[monthKey].push({
                    ...r,
                    day: date.getDate()
                });
            });

            let html = '';
            for (const [month, entries] of Object.entries(grouped)) {
                html += `<div class="diary-month">${month}</div>`;
                entries.slice(0, 5).forEach(entry => {
                    html += `
                        <div class="diary-entry">
                            <span class="diary-day">${entry.day}</span>
                            <span class="diary-title" onclick="navigateToItem('${entry.itemId}')">${entry.title || 'Unknown'}</span>
                        </div>
                    `;
                });
            }

            container.innerHTML = html;
        } catch (error) {
            console.error('Error loading diary preview:', error);
        }
    }

    // Load rating distribution
    async function loadRatingDistribution() {
        try {
            const stats = await api(`Social/Profile/${profileUserId}/Stats/Year/${new Date().getFullYear()}`);
            const container = document.getElementById('ratingDistribution');
            document.getElementById('totalRatings').textContent = profileData?.totalRatings || 0;

            const distribution = stats.distribution || new Array(10).fill(0);
            const max = Math.max(...distribution, 1);

            let html = '';
            for (let i = 10; i >= 1; i--) {
                const count = distribution[i - 1] || 0;
                const percent = (count / max) * 100;
                html += `
                    <div class="rating-bar">
                        <span class="rating-bar-label">${i}</span>
                        <div class="rating-bar-fill">
                            <div class="rating-bar-value" style="width: ${percent}%"></div>
                        </div>
                    </div>
                `;
            }

            container.innerHTML = html;
        } catch (error) {
            console.error('Error loading rating distribution:', error);
        }
    }

    // Load films
    async function loadFilms() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
            const container = document.getElementById('filmsGrid');

            // Filter movies (this would need actual item type info)
            const films = ratings.ratings || [];

            if (!films.length) {
                container.innerHTML = '<div class="empty-state">No films rated yet</div>';
                return;
            }

            container.innerHTML = films.map(film => `
                <div class="poster-item" onclick="navigateToItem('${film.itemId}')">
                    <img class="poster-image" src="${getPosterUrl(film)}" alt="${film.title}" onerror="this.style.display='none'">
                    <div class="poster-rating">
                        <span class="stars">${renderStars(film.rating)}</span>
                    </div>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading films:', error);
            document.getElementById('filmsGrid').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load series
    async function loadSeries() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
            const container = document.getElementById('seriesGrid');

            const series = ratings.ratings || [];

            if (!series.length) {
                container.innerHTML = '<div class="empty-state">No series rated yet</div>';
                return;
            }

            container.innerHTML = series.map(s => `
                <div class="poster-item" onclick="navigateToItem('${s.itemId}')">
                    <img class="poster-image" src="${getPosterUrl(s)}" alt="${s.title}" onerror="this.style.display='none'">
                    <div class="poster-rating">
                        <span class="stars">${renderStars(s.rating)}</span>
                    </div>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading series:', error);
            document.getElementById('seriesGrid').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load all reviews
    async function loadAllReviews() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
            const container = document.getElementById('allReviews');

            const reviews = (ratings.ratings || []).filter(r => r.reviewText);

            if (!reviews.length) {
                container.innerHTML = '<div class="empty-state">No reviews yet</div>';
                return;
            }

            container.innerHTML = reviews.map(review => renderReviewCard(review)).join('');
        } catch (error) {
            console.error('Error loading reviews:', error);
            document.getElementById('allReviews').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load lists
    async function loadLists() {
        try {
            const lists = await api(`Social/Profile/${profileUserId}/Lists`);
            const container = document.getElementById('listsGrid');

            if (!lists.lists?.length) {
                container.innerHTML = isOwner
                    ? `<div class="empty-state">
                        <div class="empty-state-icon">📝</div>
                        <p>You haven't created any lists yet</p>
                        <button class="btn btn-primary" onclick="createList()" style="margin-top: 15px;">Create List</button>
                       </div>`
                    : '<div class="empty-state">No lists yet</div>';
                return;
            }

            container.innerHTML = lists.lists.map(list => `
                <div class="list-card" onclick="viewList('${list.id}')">
                    <div class="list-header">
                        <div>
                            <div class="list-title">${list.title}</div>
                            <div class="list-meta">${list.itemCount || 0} items</div>
                            ${list.clonedFrom ? `<div class="list-cloned">Cloned from @${list.clonedFrom}</div>` : ''}
                        </div>
                    </div>
                    <div class="list-preview" id="list-preview-${list.id}">
                        <!-- Loaded dynamically -->
                    </div>
                </div>
            `).join('');

            // Load previews
            lists.lists.forEach(async list => {
                try {
                    const fullList = await api(`Social/Lists/${list.id}`);
                    const preview = document.getElementById(`list-preview-${list.id}`);
                    if (preview && fullList.items) {
                        preview.innerHTML = fullList.items.slice(0, 5).map(item => `
                            <img class="list-preview-poster" src="${getPosterUrl(item)}" alt="${item.title}" onerror="this.style.display='none'">
                        `).join('');
                    }
                } catch (e) {}
            });
        } catch (error) {
            console.error('Error loading lists:', error);
            document.getElementById('listsGrid').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load activity feed
    async function loadActivityFeed() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
            const container = document.getElementById('activityFeed');

            if (!ratings.ratings?.length) {
                container.innerHTML = '<div class="empty-state">No activity yet</div>';
                return;
            }

            container.innerHTML = ratings.ratings.map(rating => `
                <div class="review-card">
                    <img class="review-poster" src="${getPosterUrl(rating, 80)}" alt="${rating.title}" style="width: 60px; height: 90px;">
                    <div class="review-content">
                        <div class="review-header">
                            <span class="review-title">${rating.title || 'Unknown'}</span>
                        </div>
                        <div class="review-meta">
                            <span class="review-rating">${renderStars(rating.rating)}</span>
                            <span>${formatRelativeTime(rating.createdAt)}</span>
                        </div>
                        ${rating.reviewText ? `<p class="review-text">${rating.reviewText}</p>` : ''}
                    </div>
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading activity:', error);
            document.getElementById('activityFeed').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load diary
    async function loadDiary() {
        try {
            const ratings = await api(`Ratings/Users/${profileUserId}/Ratings`);
            const container = document.getElementById('diaryList');

            if (!ratings.ratings?.length) {
                container.innerHTML = '<div class="empty-state">No diary entries yet</div>';
                return;
            }

            // Group by month
            const grouped = {};
            ratings.ratings.forEach(r => {
                const date = new Date(r.createdAt);
                const monthKey = date.toLocaleDateString('en-US', { month: 'long', year: 'numeric' });
                if (!grouped[monthKey]) grouped[monthKey] = [];
                grouped[monthKey].push({
                    ...r,
                    day: date.getDate()
                });
            });

            let html = '';
            for (const [month, entries] of Object.entries(grouped)) {
                html += `<div class="diary-month" style="font-size: 16px; margin: 20px 0 10px;">${month}</div>`;
                entries.forEach(entry => {
                    html += `
                        <div class="review-card" style="margin-bottom: 10px;">
                            <img class="review-poster" src="${getPosterUrl(entry, 80)}" alt="${entry.title}" style="width: 50px; height: 75px;">
                            <div class="review-content">
                                <div class="review-header">
                                    <span class="diary-day" style="color: var(--text-muted); min-width: 30px;">${entry.day}</span>
                                    <span class="review-title">${entry.title || 'Unknown'}</span>
                                </div>
                                <div class="review-meta">
                                    <span class="review-rating">${renderStars(entry.rating)}</span>
                                </div>
                            </div>
                        </div>
                    `;
                });
            }

            container.innerHTML = html;
        } catch (error) {
            console.error('Error loading diary:', error);
            document.getElementById('diaryList').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load watchlist
    async function loadWatchlist() {
        try {
            const lists = await api(`Social/Profile/${profileUserId}/Lists`);
            const watchlist = lists.lists?.find(l => l.isWatchlist);
            const container = document.getElementById('watchlistGrid');

            if (!watchlist) {
                container.innerHTML = '<div class="empty-state">No watchlist</div>';
                return;
            }

            const list = await api(`Social/Lists/${watchlist.id}`);

            if (!list.items?.length) {
                container.innerHTML = '<div class="empty-state">Watchlist is empty</div>';
                return;
            }

            container.innerHTML = list.items.map(item => `
                <div class="poster-item" onclick="navigateToItem('${item.itemId || ''}')">
                    <img class="poster-image" src="${getPosterUrl(item)}" alt="${item.title}" onerror="this.style.display='none'">
                </div>
            `).join('');
        } catch (error) {
            console.error('Error loading watchlist:', error);
            document.getElementById('watchlistGrid').innerHTML = '<div class="empty-state">Failed to load</div>';
        }
    }

    // Load stats
    async function loadStats() {
        const container = document.getElementById('statsContent');
        container.innerHTML = `
            <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px;">
                <div class="widget">
                    <div class="widget-title">Total Stats</div>
                    <div style="margin-top: 15px;">
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span>Films</span>
                            <span style="font-weight: bold;">${profileData?.films || 0}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span>Series</span>
                            <span style="font-weight: bold;">${profileData?.series || 0}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span>Total Ratings</span>
                            <span style="font-weight: bold;">${profileData?.totalRatings || 0}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span>Average Rating</span>
                            <span style="font-weight: bold;">${profileData?.averageRating || 0}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between; margin-bottom: 10px;">
                            <span>Reviews Written</span>
                            <span style="font-weight: bold;">${profileData?.reviewsWritten || 0}</span>
                        </div>
                        <div style="display: flex; justify-content: space-between;">
                            <span>Likes Received</span>
                            <span style="font-weight: bold;">${profileData?.reviewLikesReceived || 0}</span>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    // ============ LIST MANAGEMENT ============

    let currentEditingList = null;
    let currentListItems = [];
    let imdbPreviewData = null;
    let mediaSearchTimeout = null;

    // Global functions
    window.navigateToItem = function(itemId) {
        if (itemId) {
            window.location.href = `#!/item?id=${itemId}`;
        }
    };

    window.openSettings = function() {
        window.location.href = 'profile_settings.html';
    };

    // Create new list
    window.createList = function() {
        currentEditingList = null;
        currentListItems = [];
        document.getElementById('listEditorTitle').textContent = 'Create New List';
        document.getElementById('listTitle').value = '';
        document.getElementById('listDescription').value = '';
        document.getElementById('listType').value = 'Mixed';
        document.getElementById('listMaxItems').value = '10';
        document.getElementById('listSpecial').value = '';
        document.getElementById('listVisibleRegular').checked = true;
        document.getElementById('listVisibleFriends').checked = true;
        renderListItems();
        document.getElementById('listEditorModal').style.display = 'flex';
    };

    // View/Edit list
    window.viewList = async function(listId) {
        try {
            const list = await api(`Social/Lists/${listId}`);
            currentEditingList = list;
            currentListItems = list.items || [];

            document.getElementById('listEditorTitle').textContent = list.isOwner ? 'Edit List' : list.title;
            document.getElementById('listTitle').value = list.title;
            document.getElementById('listDescription').value = list.description || '';
            document.getElementById('listType').value = list.listType || 'Mixed';
            document.getElementById('listMaxItems').value = list.maxItems || 10;
            document.getElementById('listSpecial').value = list.isFavorites ? 'favorites' : (list.isWatchlist ? 'watchlist' : '');
            document.getElementById('listVisibleRegular').checked = list.visibleToRegularUsers !== false;
            document.getElementById('listVisibleFriends').checked = list.visibleToFriends !== false;

            renderListItems();
            document.getElementById('listEditorModal').style.display = 'flex';
        } catch (error) {
            console.error('Error loading list:', error);
            showToast('Failed to load list', 'error');
        }
    };

    // Close list editor
    window.closeListEditor = function() {
        document.getElementById('listEditorModal').style.display = 'none';
        currentEditingList = null;
        currentListItems = [];
    };

    // Render list items in editor
    function renderListItems() {
        const container = document.getElementById('listItemsContainer');
        const maxItems = parseInt(document.getElementById('listMaxItems').value) || 10;

        let html = '';

        // Render existing items
        currentListItems.forEach((item, index) => {
            html += `
                <div class="list-item-row" draggable="true" data-index="${index}">
                    <span class="list-item-drag">⋮⋮</span>
                    <img class="list-item-poster" src="${getPosterUrl(item, 75)}" onerror="this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22/>'">
                    <div class="list-item-info">
                        <div class="list-item-title">${item.title || item.cachedTitle || 'Unknown'}</div>
                        <div class="list-item-year">${item.year || item.cachedYear || ''}</div>
                    </div>
                    <div class="list-item-note">
                        <input type="text" placeholder="Add note..." value="${item.note || ''}" onchange="updateItemNote(${index}, this.value)">
                    </div>
                    <button class="list-item-remove" onclick="removeListItem(${index})">&times;</button>
                </div>
            `;
        });

        // Render empty slots
        const emptyCount = maxItems - currentListItems.length;
        for (let i = 0; i < emptyCount; i++) {
            html += `
                <div class="empty-slot" onclick="openMediaPicker()">
                    + Add Item
                </div>
            `;
        }

        container.innerHTML = html;
        setupDragAndDrop();
    }

    // Setup drag and drop
    function setupDragAndDrop() {
        const items = document.querySelectorAll('.list-item-row');
        let draggedItem = null;

        items.forEach(item => {
            item.addEventListener('dragstart', (e) => {
                draggedItem = item;
                item.classList.add('dragging');
                e.dataTransfer.effectAllowed = 'move';
            });

            item.addEventListener('dragend', () => {
                item.classList.remove('dragging');
                draggedItem = null;
            });

            item.addEventListener('dragover', (e) => {
                e.preventDefault();
                if (draggedItem && draggedItem !== item) {
                    const rect = item.getBoundingClientRect();
                    const midY = rect.top + rect.height / 2;
                    if (e.clientY < midY) {
                        item.parentNode.insertBefore(draggedItem, item);
                    } else {
                        item.parentNode.insertBefore(draggedItem, item.nextSibling);
                    }
                }
            });

            item.addEventListener('drop', (e) => {
                e.preventDefault();
                // Update order
                const newOrder = [];
                document.querySelectorAll('.list-item-row').forEach(row => {
                    const idx = parseInt(row.dataset.index);
                    newOrder.push(currentListItems[idx]);
                });
                currentListItems = newOrder;
                renderListItems();
            });
        });
    }

    // Update item note
    window.updateItemNote = function(index, note) {
        if (currentListItems[index]) {
            currentListItems[index].note = note;
        }
    };

    // Remove list item
    window.removeListItem = function(index) {
        currentListItems.splice(index, 1);
        renderListItems();
    };

    // Save list
    window.saveList = async function() {
        const title = document.getElementById('listTitle').value.trim();
        if (!title) {
            showToast('Please enter a title', 'error');
            return;
        }

        const special = document.getElementById('listSpecial').value;
        const listData = {
            title: title,
            description: document.getElementById('listDescription').value,
            listType: document.getElementById('listType').value,
            maxItems: parseInt(document.getElementById('listMaxItems').value),
            visibleToRegularUsers: document.getElementById('listVisibleRegular').checked,
            visibleToFriends: document.getElementById('listVisibleFriends').checked,
            isFavorites: special === 'favorites',
            isWatchlist: special === 'watchlist'
        };

        try {
            let listId;

            if (currentEditingList) {
                // Update existing list
                await api(`Social/Lists/${currentEditingList.id}`, {
                    method: 'PUT',
                    body: JSON.stringify(listData)
                });
                listId = currentEditingList.id;

                // Update items (remove all and re-add)
                const existingItems = currentEditingList.items || [];
                for (const item of existingItems) {
                    await api(`Social/Lists/${listId}/Items/${item.id}`, { method: 'DELETE' });
                }
            } else {
                // Create new list
                const created = await api('Social/Lists', {
                    method: 'POST',
                    body: JSON.stringify(listData)
                });
                listId = created.id;
            }

            // Add items
            for (let i = 0; i < currentListItems.length; i++) {
                const item = currentListItems[i];
                await api(`Social/Lists/${listId}/Items`, {
                    method: 'POST',
                    body: JSON.stringify({
                        itemId: item.itemId,
                        imdbId: item.imdbId,
                        title: item.title || item.cachedTitle,
                        imageUrl: item.imageUrl || item.cachedImageUrl,
                        overview: item.overview || item.cachedOverview,
                        year: item.year || item.cachedYear,
                        mediaType: item.mediaType || item.cachedMediaType,
                        note: item.note
                    })
                });
            }

            showToast('List saved successfully!');
            closeListEditor();
            loadLists();

        } catch (error) {
            console.error('Error saving list:', error);
            showToast('Failed to save list', 'error');
        }
    };

    // ============ MEDIA PICKER ============

    window.openMediaPicker = function() {
        document.getElementById('mediaPickerModal').style.display = 'flex';
        document.getElementById('mediaSearchInput').value = '';
        loadLibraryMedia();
        setupFilterButtons();
    };

    window.closeMediaPicker = function() {
        document.getElementById('mediaPickerModal').style.display = 'none';
    };

    function setupFilterButtons() {
        const buttons = document.querySelectorAll('.filter-btn');
        buttons.forEach(btn => {
            btn.addEventListener('click', () => {
                buttons.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                loadLibraryMedia(btn.dataset.filter);
            });
        });
    }

    async function loadLibraryMedia(filter = 'all', search = '') {
        const container = document.getElementById('mediaPickerGrid');
        container.innerHTML = '<div class="loading"><div class="spinner"></div></div>';

        try {
            let url = 'Ratings/SortedLibrary?sortBy=added&direction=desc&limit=100';
            if (filter !== 'all') {
                url += `&type=${filter}`;
            }

            const response = await api(url);
            const items = response.items || [];

            let filtered = items;
            if (search) {
                const searchLower = search.toLowerCase();
                filtered = items.filter(item =>
                    (item.title || item.name || '').toLowerCase().includes(searchLower)
                );
            }

            if (!filtered.length) {
                container.innerHTML = '<div class="empty-state">No items found</div>';
                return;
            }

            container.innerHTML = filtered.map(item => `
                <div class="media-picker-item" onclick="addMediaToList('${item.id}', '${(item.title || item.name || '').replace(/'/g, "\\'")}', '${item.year || ''}', '${item.imageUrl || ''}', '${item.type || 'Movie'}')">
                    <img src="${getPosterUrl(item, 150)}" onerror="this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22/>'">
                    <div class="title">${item.title || item.name || 'Unknown'}</div>
                </div>
            `).join('');

        } catch (error) {
            console.error('Error loading library:', error);
            container.innerHTML = '<div class="empty-state">Failed to load library</div>';
        }
    }

    window.searchMedia = function(query) {
        clearTimeout(mediaSearchTimeout);
        mediaSearchTimeout = setTimeout(() => {
            const filter = document.querySelector('.filter-btn.active')?.dataset.filter || 'all';
            loadLibraryMedia(filter, query);
        }, 300);
    };

    window.addMediaToList = function(itemId, title, year, imageUrl, type) {
        const maxItems = parseInt(document.getElementById('listMaxItems').value) || 10;
        if (currentListItems.length >= maxItems) {
            showToast('List is full', 'error');
            return;
        }

        // Check if already in list
        if (currentListItems.some(item => item.itemId === itemId)) {
            showToast('Item already in list', 'error');
            return;
        }

        currentListItems.push({
            itemId: itemId,
            title: title,
            cachedTitle: title,
            year: parseInt(year) || null,
            cachedYear: parseInt(year) || null,
            imageUrl: imageUrl,
            cachedImageUrl: imageUrl,
            mediaType: type,
            cachedMediaType: type,
            note: ''
        });

        renderListItems();
        closeMediaPicker();
        showToast('Item added to list');
    };

    // ============ IMDB LOOKUP ============

    window.openImdbLookup = function() {
        document.getElementById('imdbLookupModal').style.display = 'flex';
        document.getElementById('imdbIdInput').value = '';
        document.getElementById('imdbPreview').style.display = 'none';
        imdbPreviewData = null;
    };

    window.closeImdbLookup = function() {
        document.getElementById('imdbLookupModal').style.display = 'none';
        imdbPreviewData = null;
    };

    window.lookupImdb = async function() {
        const imdbId = document.getElementById('imdbIdInput').value.trim();
        if (!imdbId || !imdbId.match(/^tt\d+$/)) {
            showToast('Please enter a valid IMDB ID (e.g., tt1234567)', 'error');
            return;
        }

        try {
            // Check cache first
            const cached = await api(`Social/IMDB/${imdbId}`).catch(() => null);

            if (cached && cached.fetchSuccess) {
                imdbPreviewData = cached;
                showImdbPreview(cached);
            } else {
                // For now, show a message that IMDB lookup needs backend implementation
                showToast('IMDB lookup: Enter details manually or check if item exists in library', 'error');

                // Show manual entry form
                imdbPreviewData = {
                    imdbId: imdbId,
                    title: '',
                    year: null,
                    overview: '',
                    posterUrl: ''
                };

                document.getElementById('imdbPreview').innerHTML = `
                    <div style="padding: 15px; background: var(--bg-secondary); border-radius: 8px;">
                        <p style="margin-bottom: 15px; color: var(--text-muted);">IMDB lookup not available. Enter details manually:</p>
                        <div class="form-group">
                            <label class="form-label">Title</label>
                            <input type="text" class="form-input" id="manualTitle" placeholder="Movie/Series title">
                        </div>
                        <div class="form-group">
                            <label class="form-label">Year</label>
                            <input type="number" class="form-input" id="manualYear" placeholder="2024">
                        </div>
                        <div class="form-group">
                            <label class="form-label">Type</label>
                            <select class="form-input" id="manualType">
                                <option value="Movie">Movie</option>
                                <option value="Series">Series</option>
                            </select>
                        </div>
                    </div>
                    <button class="btn btn-primary" onclick="addManualItem()" style="width: 100%; margin-top: 15px;">Add to List</button>
                `;
                document.getElementById('imdbPreview').style.display = 'block';
            }

        } catch (error) {
            console.error('Error looking up IMDB:', error);
            showToast('Failed to lookup IMDB', 'error');
        }
    };

    function showImdbPreview(data) {
        document.getElementById('imdbPreviewPoster').src = data.posterUrl || '';
        document.getElementById('imdbPreviewTitle').textContent = data.title;
        document.getElementById('imdbPreviewYear').textContent = data.year ? `(${data.year})` : '';
        document.getElementById('imdbPreviewOverview').textContent = data.overview || '';
        document.getElementById('imdbPreview').style.display = 'block';
    }

    window.addImdbItem = function() {
        if (!imdbPreviewData) return;

        const maxItems = parseInt(document.getElementById('listMaxItems').value) || 10;
        if (currentListItems.length >= maxItems) {
            showToast('List is full', 'error');
            return;
        }

        currentListItems.push({
            imdbId: imdbPreviewData.imdbId,
            title: imdbPreviewData.title,
            cachedTitle: imdbPreviewData.title,
            year: imdbPreviewData.year,
            cachedYear: imdbPreviewData.year,
            imageUrl: imdbPreviewData.posterUrl,
            cachedImageUrl: imdbPreviewData.posterUrl,
            overview: imdbPreviewData.overview,
            cachedOverview: imdbPreviewData.overview,
            mediaType: imdbPreviewData.mediaType || 'Movie',
            cachedMediaType: imdbPreviewData.mediaType || 'Movie',
            note: ''
        });

        renderListItems();
        closeImdbLookup();
        showToast('Item added to list');
    };

    window.addManualItem = function() {
        const title = document.getElementById('manualTitle').value.trim();
        if (!title) {
            showToast('Please enter a title', 'error');
            return;
        }

        const maxItems = parseInt(document.getElementById('listMaxItems').value) || 10;
        if (currentListItems.length >= maxItems) {
            showToast('List is full', 'error');
            return;
        }

        const imdbId = document.getElementById('imdbIdInput').value.trim();

        currentListItems.push({
            imdbId: imdbId,
            title: title,
            cachedTitle: title,
            year: parseInt(document.getElementById('manualYear').value) || null,
            cachedYear: parseInt(document.getElementById('manualYear').value) || null,
            mediaType: document.getElementById('manualType').value,
            cachedMediaType: document.getElementById('manualType').value,
            note: ''
        });

        renderListItems();
        closeImdbLookup();
        showToast('Item added to list');
    };

    // ============ UTILITIES ============

    window.likeReview = async function(userId, itemId, isLike) {
        try {
            await api(`Ratings/Reviews/${userId}/${itemId}/Like?isLike=${isLike}`, { method: 'POST' });
            loadRecentReviews();
        } catch (error) {
            console.error('Error liking review:', error);
        }
    };

    function showToast(message, type = 'success') {
        const toast = document.getElementById('toast');
        if (toast) {
            toast.textContent = message;
            toast.className = `toast show ${type}`;
            setTimeout(() => {
                toast.classList.remove('show');
            }, 3000);
        }
    }

    function showError(message) {
        console.error(message);
        showToast(message, 'error');
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initProfile);
    } else {
        initProfile();
    }
})();
