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
        // Set up tabs
        LibraryMenu.setTabs('ratings_dashboard', 0, getTabs);

        // Load all data
        loadStats();
        loadRecentActivity();
        loadTopRated();
    });

    function loadStats() {
        const url = window.ApiClient.getUrl('Ratings/Stats');
        fetch(url, {
            method: 'GET',
            headers: {
                'X-Emby-Authorization': getAuthHeader()
            }
        })
        .then(response => response.json())
        .then(data => {
            document.getElementById('stat-total-ratings').textContent = data.TotalRatings || 0;
            document.getElementById('stat-total-users').textContent = data.TotalUsers || 0;
            document.getElementById('stat-total-reviews').textContent = data.TotalReviews || 0;
            document.getElementById('stat-avg-rating').textContent = data.AverageRating ? data.AverageRating.toFixed(1) : '-';
        })
        .catch(err => {
            console.error('Failed to load stats:', err);
        });
    }

    function loadRecentActivity() {
        const url = window.ApiClient.getUrl('Ratings/RecentActivity?limit=15');
        fetch(url, {
            method: 'GET',
            headers: {
                'X-Emby-Authorization': getAuthHeader()
            }
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
            case 'rating': return '&#9733;'; // star
            case 'review': return '&#9997;'; // pencil
            case 'request': return '&#10010;'; // plus
            case 'comment': return '&#128172;'; // speech bubble
            default: return '&#8226;'; // bullet
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
                const typeLabel = item.RequestType === 'movie' ? 'movie' : item.RequestType === 'tv' ? 'TV show' : 'media';
                return `requested ${typeLabel} <strong>${escapeHtml(item.ItemName)}</strong>`;
            case 'comment':
                return `commented on <strong>${escapeHtml(item.TargetUserName)}</strong>'s review of <strong>${escapeHtml(item.ItemName)}</strong>`;
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
        if (item.Type === 'request' && item.RequestStatus) {
            const statusClass = item.RequestStatus === 'completed' ? 'status-completed' :
                               item.RequestStatus === 'pending' ? 'status-pending' : 'status-default';
            return `<span class="request-status ${statusClass}">${item.RequestStatus}</span>`;
        }
        return null;
    }

    function loadTopRated() {
        const url = window.ApiClient.getUrl('Ratings/TopRated?limit=6');
        fetch(url, {
            method: 'GET',
            headers: {
                'X-Emby-Authorization': getAuthHeader()
            }
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
                    ? window.ApiClient.getUrl(item.ImageUrl + '?fillHeight=180&fillWidth=120&quality=96')
                    : '';
                html += `
                    <a href="#!/details?id=${item.Id}" class="top-rated-card">
                        <div class="top-rated-rank">#${index + 1}</div>
                        ${imageUrl
                            ? `<img src="${imageUrl}" class="top-rated-poster" alt="${escapeHtml(item.Name)}">`
                            : '<div class="top-rated-poster top-rated-placeholder"></div>'
                        }
                        <div class="top-rated-info">
                            <div class="top-rated-title">${escapeHtml(item.Name)}</div>
                            ${item.Year ? `<div class="top-rated-year">${item.Year}</div>` : ''}
                            <div class="top-rated-rating">
                                <span class="star">&#9733;</span>
                                <span class="score">${item.AverageRating.toFixed(1)}</span>
                                <span class="count">(${item.TotalRatings})</span>
                            </div>
                        </div>
                    </a>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('top-rated-items').innerHTML = '<div class="activity-empty">Failed to load</div>';
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

    view.addEventListener('viewhide', function (e) {
    });

    view.addEventListener('viewdestroy', function (e) {
    });
};
