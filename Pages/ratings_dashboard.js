const getConfigurationPageUrl = (name) => {
    return 'configurationpage?name=' + encodeURIComponent(name);
};

function getTabs() {
    return [
        {
            href: getConfigurationPageUrl('ratings_dashboard'),
            name: 'Dashboard'
        },
        {
            href: getConfigurationPageUrl('Ratings'),
            name: 'Settings'
        }
    ];
}

export default function (view, params) {
    view.addEventListener('viewshow', function (e) {
        LibraryMenu.setTabs('ratings_dashboard', 0, getTabs);

        // Load all dashboard data
        loadStats();
        loadRecentActivity();
        loadTopRated();
        loadMostActiveUsers();
        loadRatingDistribution();
        loadRecentRequests();
    });

    function loadStats() {
        const url = window.ApiClient.getUrl('Ratings/Stats');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            document.getElementById('stat-total-ratings').textContent = data.TotalRatings || 0;
            document.getElementById('stat-total-users').textContent = data.TotalUsers || 0;
            document.getElementById('stat-total-reviews').textContent = data.TotalReviews || 0;
            document.getElementById('stat-avg-rating').textContent = data.AverageRating ? data.AverageRating.toFixed(1) : '-';
        })
        .catch(err => console.error('Failed to load stats:', err));
    }

    function loadRecentActivity() {
        const url = window.ApiClient.getUrl('Ratings/RecentActivity?limit=10');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            const container = document.getElementById('recent-activity');
            if (!data || data.length === 0) {
                container.innerHTML = '<div class="activity-empty">No recent activity</div>';
                return;
            }

            let html = '';
            data.forEach(item => {
                html += renderActivityItem(item);
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('recent-activity').innerHTML = '<div class="activity-empty">Failed to load</div>';
        });
    }

    function renderActivityItem(item) {
        const icon = getActivityIcon(item.Type);
        const color = getActivityColor(item.Type);
        const description = getActivityDescription(item);
        const detail = getActivityDetail(item);

        return `
            <div class="activity-item">
                <div class="activity-icon" style="background: ${color}20; color: ${color};">${icon}</div>
                <div class="activity-content">
                    <div class="activity-main">
                        <span class="activity-user">${escapeHtml(item.UserName)}</span>
                        <span class="activity-action">${description}</span>
                    </div>
                    ${detail ? `<div class="activity-detail">${detail}</div>` : ''}
                    <div class="activity-time">${formatDate(item.Timestamp)}</div>
                </div>
                ${item.Rating ? `<div class="activity-rating">${item.Rating}<span>/10</span></div>` : ''}
            </div>
        `;
    }

    function getActivityIcon(type) {
        switch (type) {
            case 'rating': return '&#9733;';
            case 'review': return '&#9997;';
            case 'request': return '&#10010;';
            case 'comment': return '&#128172;';
            default: return '&#8226;';
        }
    }

    function getActivityColor(type) {
        switch (type) {
            case 'rating': return '#ffd700';
            case 'review': return '#00a4dc';
            case 'request': return '#4caf50';
            case 'comment': return '#9c27b0';
            default: return '#888';
        }
    }

    function getActivityDescription(item) {
        switch (item.Type) {
            case 'rating':
                return `rated <strong>${escapeHtml(item.ItemName)}</strong>`;
            case 'review':
                return `reviewed <strong>${escapeHtml(item.ItemName)}</strong>`;
            case 'request':
                return `requested <strong>${escapeHtml(item.ItemName)}</strong>`;
            case 'comment':
                return `commented on <strong>${escapeHtml(item.ItemName)}</strong>`;
            default:
                return item.ItemName;
        }
    }

    function getActivityDetail(item) {
        if (item.Type === 'review' && item.ReviewPreview) {
            return `<span class="preview-text">"${escapeHtml(item.ReviewPreview)}"</span>`;
        }
        if (item.Type === 'comment' && item.CommentPreview) {
            return `<span class="preview-text">"${escapeHtml(item.CommentPreview)}"</span>`;
        }
        return null;
    }

    function loadTopRated() {
        const url = window.ApiClient.getUrl('Ratings/TopRated?limit=8');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            const container = document.getElementById('top-rated-items');
            if (!data || data.length === 0) {
                container.innerHTML = '<div class="activity-empty">No rated items yet</div>';
                return;
            }

            let html = '';
            data.forEach((item, index) => {
                const imageUrl = item.ImageUrl
                    ? window.ApiClient.getUrl(item.ImageUrl + '?fillHeight=80&fillWidth=54&quality=96')
                    : '';
                html += `
                    <a href="#!/details?id=${item.Id}" class="top-item" style="text-decoration: none; color: inherit;">
                        <div class="top-rank">${index + 1}</div>
                        ${imageUrl
                            ? `<img src="${imageUrl}" class="top-poster" alt="">`
                            : '<div class="top-poster"></div>'
                        }
                        <div class="top-info">
                            <div class="top-title">${escapeHtml(item.Name)}</div>
                            <div class="top-meta">${item.Year || ''} &bull; ${item.TotalRatings} rating${item.TotalRatings !== 1 ? 's' : ''}</div>
                        </div>
                        <div class="top-score">${item.AverageRating.toFixed(1)}</div>
                    </a>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('top-rated-items').innerHTML = '<div class="activity-empty">Failed to load</div>';
        });
    }

    function loadMostActiveUsers() {
        const url = window.ApiClient.getUrl('Ratings/MostActiveUsers?limit=6');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            const container = document.getElementById('most-active-users');
            if (!data || data.length === 0) {
                container.innerHTML = '<div class="activity-empty">No active users yet</div>';
                return;
            }

            let html = '';
            data.forEach(user => {
                const initial = user.UserName.charAt(0).toUpperCase();
                html += `
                    <div class="user-item">
                        <div class="user-avatar">${initial}</div>
                        <div class="user-info">
                            <div class="user-name">${escapeHtml(user.UserName)}</div>
                            <div class="user-stats">${user.ReviewCount} review${user.ReviewCount !== 1 ? 's' : ''} &bull; avg ${user.AverageRating}</div>
                        </div>
                        <div class="user-count">${user.RatingCount}</div>
                    </div>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('most-active-users').innerHTML = '<div class="activity-empty">Failed to load</div>';
        });
    }

    function loadRatingDistribution() {
        const url = window.ApiClient.getUrl('Ratings/RatingDistribution');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            const container = document.getElementById('rating-distribution');
            if (!data || data.length === 0) {
                container.innerHTML = '<div class="activity-empty">No data</div>';
                return;
            }

            const maxCount = Math.max(...data.map(d => d.Count), 1);
            let html = '';

            // Show from 10 down to 1
            for (let i = data.length - 1; i >= 0; i--) {
                const item = data[i];
                const percentage = (item.Count / maxCount) * 100;
                const barClass = item.Rating >= 8 ? 'high' : item.Rating >= 5 ? 'mid' : 'low';

                html += `
                    <div class="dist-row">
                        <div class="dist-label">${item.Rating}</div>
                        <div class="dist-bar-container">
                            <div class="dist-bar ${barClass}" style="width: ${percentage}%;"></div>
                        </div>
                        <div class="dist-count">${item.Count}</div>
                    </div>
                `;
            }
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('rating-distribution').innerHTML = '<div class="activity-empty">Failed to load</div>';
        });
    }

    function loadRecentRequests() {
        const url = window.ApiClient.getUrl('Ratings/RecentRequests?limit=6');
        fetch(url, {
            method: 'GET',
            headers: { 'X-Emby-Authorization': getAuthHeader() }
        })
        .then(response => response.json())
        .then(data => {
            const container = document.getElementById('recent-requests');
            if (!data || data.length === 0) {
                container.innerHTML = '<div class="activity-empty">No requests yet</div>';
                return;
            }

            let html = '';
            data.forEach(request => {
                const typeIcon = request.Type === 'movie' ? '&#127916;' : '&#128250;';
                const typeClass = request.Type === 'movie' ? 'movie' : 'tv';
                const statusClass = (request.Status || 'pending').toLowerCase();

                html += `
                    <div class="request-item">
                        <div class="request-type-icon ${typeClass}">${typeIcon}</div>
                        <div class="request-info">
                            <div class="request-title">${escapeHtml(request.Title)}</div>
                            <div class="request-meta">by ${escapeHtml(request.UserName)} &bull; ${formatDate(request.CreatedAt)}</div>
                        </div>
                        <div class="request-status ${statusClass}">${request.Status || 'pending'}</div>
                    </div>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('recent-requests').innerHTML = '<div class="activity-empty">Failed to load</div>';
        });
    }

    function getAuthHeader() {
        const token = window.ApiClient.accessToken();
        const deviceId = localStorage.getItem('_deviceId2') || 'unknown';
        return `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${token}"`;
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function formatDate(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'Just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;
        return date.toLocaleDateString();
    }

    view.addEventListener('viewhide', function (e) {});
    view.addEventListener('viewdestroy', function (e) {});
};
