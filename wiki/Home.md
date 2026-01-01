# Jellyfin Ratings Plugin

A professional, feature-rich rating system for Jellyfin media server with performance-optimized card overlays, a built-in media request system, and real-time new media notifications.

![Rating interface on movie detail page](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-detail-page.png)

---

## Quick Links

- [[Installation]] - How to install the plugin
- [[Features]] - All features with screenshots
- [[Usage Guide]] - How to use ratings, requests, and notifications
- [[Docker Troubleshooting]] - Fix permission issues on Docker
- [[API Documentation]] - REST API endpoints for developers

---

## Features Overview

| Feature | Description |
|---------|-------------|
| **Star Ratings** | 1-10 star rating system for all media |
| **User Ratings Popup** | Hover to see all user ratings |
| **Card Overlays** | Rating badges on media thumbnails |
| **Netflix View** | Optional Netflix-style genre rows |
| **Media Requests** | Users request, admins manage |
| **Notifications** | Real-time alerts for new media |

---

## Quick Start

### 1. Add Repository
Go to **Jellyfin Dashboard** → **Plugins** → **Repositories** → Add:
```
https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json
```

### 2. Install Plugin
Go to **Plugins** → **Catalog** → Find **"Ratings"** → **Install** → **Restart Jellyfin**

### 3. Done!
The plugin works automatically after restart. No configuration needed.

---

## Screenshots

### Rating System
![Rating hover popup](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-hover-popup.png)
*Hover to see ratings from all users*

### Card Badges
![Card badges](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-card-badges.png)
*Rating badges on media thumbnails*

### Notifications
![Notification popup](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/notification-popup.png)
*Real-time notifications for new media*

---

## Requirements

- **Jellyfin**: 10.11.0 or higher
- **Browser**: Modern browser with JavaScript enabled

---

## Support

Having issues? Check the [[Docker Troubleshooting]] page or open an issue:
https://github.com/K3ntas/jellyfin-plugin-ratings/issues

---

## License

MIT License - Free to use and modify.
