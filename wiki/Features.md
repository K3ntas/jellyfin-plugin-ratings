# Features

All features are optional and can be enabled/disabled through the plugin settings in Jellyfin Dashboard.

---

## Star Rating System

![Rating interface](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-detail-page.png)

- **1-10 star rating** for all media types (movies, TV shows, music, etc.)
- **Interactive UI** with smooth hover effects and instant feedback
- **Visual indicators** showing your rating and average community rating
- **Persistent ratings** saved per-user across all devices

---

## User Ratings Popup

![Rating popup](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-hover-popup.png)

- **Hover popup** showing detailed ratings from all users
- **Username display** with individual ratings (e.g., "John: 8/10")
- **Rating statistics** including average rating and total count
- **Privacy-aware** - only shows ratings, not full user profiles

---

## Media Card Overlays

![Card badges](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-card-badges.png)

- **Rating badges** displayed on media cards (e.g., "7.5")
- **Lazy loading** using IntersectionObserver for optimal performance
- **Smart caching** prevents duplicate API requests
- **Optimized for large libraries** (tested with 15TB+ media collections)
- **Non-intrusive design** that doesn't interfere with Jellyfin's UI

---

## Netflix-Style View

![Netflix view](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/netflix-view.png)

- **Horizontal genre rows** like Netflix
- **Smooth scrolling** with navigation arrows
- **Rating badges** on each card
- Enable in plugin settings

---

## New Media Notifications

![Notification](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/notification-popup.png)

- **Real-time notifications** when new movies, series, or episodes are added
- **Beautiful popup UI** with media poster, title, and year
- **Episode grouping** - multiple episodes show as single notification (e.g., "Episodes 4-8")
- **Smart timing** - 2-10 minute random delay between notifications to avoid spam
- **24-hour duplicate prevention** - same item won't notify twice
- **Toggle control** - users can enable/disable via header toggle
- **Works during playback** - notifications appear even in fullscreen mode
- **Fire TV/Android TV support** - native app notifications via DisplayMessage

---

## Media Request System

### Request Button
![Request button](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-button.png)

Animated "Request Media" button with shine effect in the header.

### User Features
![User request form](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-form-user.png)

- Submit requests for Movies, TV Series, or Anime
- Add notes (e.g., "Season 2", "Need ASAP")
- Track request status (Pending → Processing → Done)
- See timestamps for when requested and completed
- "Watch Now" button when request is fulfilled
- Multi-language support (EN/LT)

### Admin Features
![Admin panel](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-admin-panel.png)

- Notification badge showing pending request count
- View all requests with user info and notes
- One-click status updates (Pending/Processing/Done)
- Add media link when marking as Done
- Delete requests permanently
- See request and completion timestamps

### Completed Requests
![Completed request](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-user-done.png)

When a request is fulfilled, users see a "Watch Now" button linking directly to the media.

---

## Performance

- **IntersectionObserver** loads ratings only for visible cards
- **Request caching** eliminates duplicate API calls
- **Efficient DOM handling** prevents UI lag
- **Minimal server load** even with thousands of media items
- **Silent logging** - server only logs errors, not routine operations
