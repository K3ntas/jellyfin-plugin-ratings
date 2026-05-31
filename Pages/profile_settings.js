/**
 * Profile Settings Page
 */

(function() {
    'use strict';

    let currentStyle = null;
    let currentProfile = null;
    let currentPrivacy = null;

    // Get auth header
    function getAuthHeader() {
        const token = window.ApiClient?.accessToken();
        return token ? { 'X-Emby-Token': token } : {};
    }

    // API helper
    async function api(endpoint, options = {}) {
        const url = window.ApiClient?.getUrl(endpoint) || `/${endpoint}`;
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

    // Show toast
    function showToast(message, type = 'success') {
        const toast = document.getElementById('toast');
        toast.textContent = message;
        toast.className = `toast show ${type}`;
        setTimeout(() => {
            toast.classList.remove('show');
        }, 3000);
    }

    // Initialize
    async function init() {
        setupNavigation();
        setupColorPickers();
        setupRangeInputs();
        await loadSettings();
    }

    // Setup navigation
    function setupNavigation() {
        const navBtns = document.querySelectorAll('.nav-btn');
        navBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                const section = btn.dataset.section;

                // Update nav
                navBtns.forEach(b => b.classList.remove('active'));
                btn.classList.add('active');

                // Update sections
                document.querySelectorAll('.settings-section').forEach(s => {
                    s.classList.remove('active');
                });
                document.getElementById(`section-${section}`).classList.add('active');
            });
        });
    }

    // Setup color pickers
    function setupColorPickers() {
        const colorPickers = document.querySelectorAll('.color-picker');
        colorPickers.forEach(picker => {
            const hexInput = document.getElementById(picker.id + 'Hex');
            if (hexInput) {
                picker.addEventListener('input', () => {
                    hexInput.value = picker.value;
                    updatePreview();
                });
                hexInput.addEventListener('input', () => {
                    if (/^#[0-9A-Fa-f]{6}$/.test(hexInput.value)) {
                        picker.value = hexInput.value;
                        updatePreview();
                    }
                });
            }
        });
    }

    // Setup range inputs
    function setupRangeInputs() {
        document.getElementById('backgroundBlur').addEventListener('input', (e) => {
            document.getElementById('blurValue').textContent = e.target.value + 'px';
        });

        document.getElementById('backgroundOverlay').addEventListener('input', (e) => {
            document.getElementById('overlayValue').textContent = e.target.value + '%';
        });

        document.getElementById('cardBorderRadius').addEventListener('input', (e) => {
            document.getElementById('radiusValue').textContent = e.target.value + 'px';
        });
    }

    // Load settings
    async function loadSettings() {
        try {
            // Load profile
            currentProfile = await api('Social/MyProfile');
            document.getElementById('bio').value = currentProfile.bio || '';

            // Load style
            currentStyle = await api('Social/MyProfile/Style');
            applyStyleToForm(currentStyle);

            // Load privacy (from profile)
            currentPrivacy = currentProfile.privacy || {};
            applyPrivacyToForm(currentPrivacy);

        } catch (error) {
            console.error('Error loading settings:', error);
            showToast('Failed to load settings', 'error');
        }
    }

    // Apply style to form
    function applyStyleToForm(style) {
        if (!style) return;

        // Theme
        document.getElementById('theme').value = style.theme || 'dark';
        document.getElementById('fontFamily').value = style.fontFamily || 'system-ui, -apple-system, sans-serif';

        // Background
        document.getElementById('backgroundType').value = style.backgroundType || 'solid';
        updateBackgroundOptions();

        if (style.backgroundColor) {
            document.getElementById('backgroundColor').value = style.backgroundColor;
            document.getElementById('backgroundColorHex').value = style.backgroundColor;
        }

        if (style.backgroundBlur !== undefined) {
            document.getElementById('backgroundBlur').value = style.backgroundBlur;
            document.getElementById('blurValue').textContent = style.backgroundBlur + 'px';
        }

        if (style.backgroundOverlayOpacity !== undefined) {
            document.getElementById('backgroundOverlay').value = style.backgroundOverlayOpacity;
            document.getElementById('overlayValue').textContent = style.backgroundOverlayOpacity + '%';
        }

        // Colors
        const colorFields = [
            'accentColor', 'usernameColor', 'bioColor', 'statsNumberColor', 'statsLabelColor',
            'tabActiveColor', 'tabInactiveColor', 'sectionHeaderColor', 'cardBackgroundColor',
            'cardBorderColor', 'ratingStarsColor', 'likeColor'
        ];

        colorFields.forEach(field => {
            const value = style[field];
            if (value) {
                const picker = document.getElementById(field);
                const hex = document.getElementById(field + 'Hex');
                if (picker) picker.value = value;
                if (hex) hex.value = value;
            }
        });

        // Card styling
        if (style.cardBorderRadius !== undefined) {
            document.getElementById('cardBorderRadius').value = style.cardBorderRadius;
            document.getElementById('radiusValue').textContent = style.cardBorderRadius + 'px';
        }

        document.getElementById('posterHoverEffect').value = style.posterHoverEffect || 'scale';

        updatePreview();
    }

    // Apply privacy to form
    function applyPrivacyToForm(privacy) {
        const fields = [
            'ratingsVisibleRegular', 'ratingsVisibleFriends',
            'reviewsVisibleRegular', 'reviewsVisibleFriends',
            'listsVisibleRegular', 'listsVisibleFriends',
            'diaryVisibleRegular', 'diaryVisibleFriends',
            'statsVisibleRegular', 'statsVisibleFriends',
            'followersVisibleRegular', 'followersVisibleFriends',
            'followingVisibleRegular', 'followingVisibleFriends',
            'profileLikersVisibleRegular', 'profileLikersVisibleFriends',
            'onlineStatusVisibleRegular', 'onlineStatusVisibleFriends',
            'watchHistoryVisibleRegular', 'watchHistoryVisibleFriends',
            'notifyOnProfileLike', 'notifyOnNewFollower',
            'notifyOnReviewLike', 'notifyOnReviewComment'
        ];

        fields.forEach(field => {
            const el = document.getElementById(field);
            if (el) {
                // Convert field name to match API property name
                const apiField = field.charAt(0).toUpperCase() + field.slice(1);
                el.checked = privacy[apiField] !== false && privacy[field] !== false;
            }
        });
    }

    // Update background options visibility
    window.updateBackgroundOptions = function() {
        const type = document.getElementById('backgroundType').value;
        document.getElementById('bgColorGroup').style.display = type === 'solid' ? 'block' : 'none';
        document.getElementById('bgGradientGroup').style.display = type === 'gradient' ? 'block' : 'none';
        document.getElementById('bgImageGroup').style.display = type === 'image' ? 'block' : 'none';
    };

    // Update preview
    function updatePreview() {
        const usernameColor = document.getElementById('usernameColor').value;
        const bioColor = document.getElementById('bioColor').value;
        const accentColor = document.getElementById('accentColor').value;

        document.getElementById('previewUsername').style.color = usernameColor;
        document.getElementById('previewBio').style.color = bioColor;
        document.getElementById('previewAvatar').style.background = accentColor;
    }

    // Preview avatar
    window.previewAvatar = function(input) {
        if (input.files && input.files[0]) {
            const reader = new FileReader();
            reader.onload = function(e) {
                document.getElementById('avatarPreview').innerHTML = `<img src="${e.target.result}">`;
            };
            reader.readAsDataURL(input.files[0]);
        }
    };

    // Preview background image
    window.previewBgImage = function(input) {
        if (input.files && input.files[0]) {
            const reader = new FileReader();
            reader.onload = function(e) {
                document.getElementById('bgImagePreview').innerHTML = `<img src="${e.target.result}">`;
            };
            reader.readAsDataURL(input.files[0]);
        }
    };

    // Build gradient string
    function buildGradient() {
        const start = document.getElementById('gradientStart').value;
        const end = document.getElementById('gradientEnd').value;
        const direction = document.getElementById('gradientDirection').value;

        if (direction === 'circle') {
            return `radial-gradient(circle, ${start}, ${end})`;
        }
        return `linear-gradient(${direction}, ${start}, ${end})`;
    }

    // Save settings
    window.saveSettings = async function() {
        try {
            // Build style object
            const style = {
                theme: document.getElementById('theme').value,
                fontFamily: document.getElementById('fontFamily').value,
                backgroundType: document.getElementById('backgroundType').value,
                backgroundColor: document.getElementById('backgroundColor').value,
                backgroundGradient: buildGradient(),
                backgroundBlur: parseInt(document.getElementById('backgroundBlur').value),
                backgroundOverlayOpacity: parseInt(document.getElementById('backgroundOverlay').value),
                accentColor: document.getElementById('accentColor').value,
                usernameColor: document.getElementById('usernameColor').value,
                bioColor: document.getElementById('bioColor').value,
                statsNumberColor: document.getElementById('statsNumberColor').value,
                statsLabelColor: document.getElementById('statsLabelColor').value,
                tabActiveColor: document.getElementById('tabActiveColor').value,
                tabInactiveColor: document.getElementById('tabInactiveColor').value,
                sectionHeaderColor: document.getElementById('sectionHeaderColor').value,
                cardBackgroundColor: document.getElementById('cardBackgroundColor').value,
                cardBorderColor: document.getElementById('cardBorderColor').value,
                ratingStarsColor: document.getElementById('ratingStarsColor').value,
                likeColor: document.getElementById('likeColor').value,
                cardBorderRadius: parseInt(document.getElementById('cardBorderRadius').value),
                posterHoverEffect: document.getElementById('posterHoverEffect').value
            };

            // Save style
            await api('Social/MyProfile/Style', {
                method: 'PUT',
                body: JSON.stringify(style)
            });

            // Build privacy object
            const privacy = {
                ratingsVisibleRegular: document.getElementById('ratingsVisibleRegular').checked,
                ratingsVisibleFriends: document.getElementById('ratingsVisibleFriends').checked,
                reviewsVisibleRegular: document.getElementById('reviewsVisibleRegular').checked,
                reviewsVisibleFriends: document.getElementById('reviewsVisibleFriends').checked,
                listsVisibleRegular: document.getElementById('listsVisibleRegular').checked,
                listsVisibleFriends: document.getElementById('listsVisibleFriends').checked,
                diaryVisibleRegular: document.getElementById('diaryVisibleRegular').checked,
                diaryVisibleFriends: document.getElementById('diaryVisibleFriends').checked,
                statsVisibleRegular: document.getElementById('statsVisibleRegular').checked,
                statsVisibleFriends: document.getElementById('statsVisibleFriends').checked,
                followersVisibleRegular: document.getElementById('followersVisibleRegular').checked,
                followersVisibleFriends: document.getElementById('followersVisibleFriends').checked,
                followingVisibleRegular: document.getElementById('followingVisibleRegular').checked,
                followingVisibleFriends: document.getElementById('followingVisibleFriends').checked,
                profileLikersVisibleRegular: document.getElementById('profileLikersVisibleRegular').checked,
                profileLikersVisibleFriends: document.getElementById('profileLikersVisibleFriends').checked,
                onlineStatusVisibleRegular: document.getElementById('onlineStatusVisibleRegular').checked,
                onlineStatusVisibleFriends: document.getElementById('onlineStatusVisibleFriends').checked,
                watchHistoryVisibleRegular: document.getElementById('watchHistoryVisibleRegular').checked,
                watchHistoryVisibleFriends: document.getElementById('watchHistoryVisibleFriends').checked,
                notifyOnProfileLike: document.getElementById('notifyOnProfileLike').checked,
                notifyOnNewFollower: document.getElementById('notifyOnNewFollower').checked,
                notifyOnReviewLike: document.getElementById('notifyOnReviewLike').checked,
                notifyOnReviewComment: document.getElementById('notifyOnReviewComment').checked
            };

            // Save profile with bio and privacy
            await api('Social/Profile', {
                method: 'PUT',
                body: JSON.stringify({
                    bio: document.getElementById('bio').value,
                    privacy: privacy
                })
            });

            showToast('Settings saved successfully!');

        } catch (error) {
            console.error('Error saving settings:', error);
            showToast('Failed to save settings', 'error');
        }
    };

    // Reset to defaults
    window.resetToDefaults = function() {
        if (confirm('Reset all settings to defaults?')) {
            const defaultStyle = {
                theme: 'dark',
                fontFamily: 'system-ui, -apple-system, sans-serif',
                backgroundType: 'solid',
                backgroundColor: '#1a1a2e',
                backgroundBlur: 0,
                backgroundOverlayOpacity: 50,
                accentColor: '#00d4ff',
                usernameColor: '#ffffff',
                bioColor: '#a0a0a0',
                statsNumberColor: '#ffffff',
                statsLabelColor: '#808080',
                tabActiveColor: '#00d4ff',
                tabInactiveColor: '#808080',
                sectionHeaderColor: '#a0a0a0',
                cardBackgroundColor: '#2a2a3e',
                cardBorderColor: '#3a3a4e',
                ratingStarsColor: '#ffd700',
                likeColor: '#ff6b6b',
                cardBorderRadius: 8,
                posterHoverEffect: 'scale'
            };

            applyStyleToForm(defaultStyle);
            showToast('Reset to defaults');
        }
    };

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
