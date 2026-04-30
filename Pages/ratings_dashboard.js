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

        // Load statistics
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
        const url = window.ApiClient.getUrl('Ratings/RecentActivity?limit=10');
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
                container.innerHTML = '<div style="color: #999; text-align: center;">No recent activity</div>';
                return;
            }

            let html = '';
            data.forEach(item => {
                html += `
                    <div style="display: flex; align-items: center; padding: 10px 0; border-bottom: 1px solid rgba(255,255,255,0.1);">
                        <div style="flex: 1;">
                            <div style="font-weight: 500;">${escapeHtml(item.UserName)} rated ${escapeHtml(item.ItemName)}</div>
                            <div style="color: #999; font-size: 12px;">${formatDate(item.Timestamp)}</div>
                        </div>
                        <div style="color: #ffd700; font-size: 18px; font-weight: bold;">${item.Rating}/10</div>
                    </div>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('recent-activity').innerHTML = '<div style="color: #999; text-align: center;">Failed to load</div>';
        });
    }

    function loadTopRated() {
        const url = window.ApiClient.getUrl('Ratings/TopRated?limit=5');
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
                container.innerHTML = '<div style="color: #999;">No rated items yet</div>';
                return;
            }

            let html = '';
            data.forEach(item => {
                const imageUrl = item.ImageUrl
                    ? window.ApiClient.getUrl(item.ImageUrl + '?fillHeight=150&fillWidth=100&quality=96')
                    : '';
                html += `
                    <a href="#!/details?id=${item.Id}" style="text-decoration: none; color: inherit;">
                        <div style="background: rgba(255,255,255,0.05); border-radius: 8px; padding: 10px; width: 120px; text-align: center;">
                            ${imageUrl
                                ? `<img src="${imageUrl}" style="width: 100px; height: 150px; object-fit: cover; border-radius: 4px;">`
                                : '<div style="width: 100px; height: 150px; background: #333; border-radius: 4px;"></div>'
                            }
                            <div style="margin-top: 8px; font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${escapeHtml(item.Name)}</div>
                            <div style="color: #ffd700; font-weight: bold;">${item.AverageRating.toFixed(1)}/10</div>
                        </div>
                    </a>
                `;
            });
            container.innerHTML = html;
        })
        .catch(err => {
            document.getElementById('top-rated-items').innerHTML = '<div style="color: #999;">Failed to load</div>';
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
