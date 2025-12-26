# Jellyfin Ratings Plugin

A professional, feature-rich rating system for Jellyfin media server with performance-optimized card overlays and a built-in media request system.

## Screenshots

### Rating Detail Page
![Rating interface on movie detail page](images/rating-detail-page.png)
*Interactive 1-10 star rating system displayed below the movie title*

### User Ratings Popup
![Hover popup showing all user ratings](images/rating-hover-popup.png)
*Hover over stars to see detailed ratings from all users*

### Rated Media
![Filled stars after rating](images/rating-filled-stars.png)
*Your rating is saved and displayed with highlighted stars*

### Card Overlays
![Rating badges on media cards](images/rating-card-badges.png)
*Rating badges automatically appear on media thumbnails throughout Jellyfin*

### Netflix-Style View
![Netflix-style genre rows](images/netflix-view.png)
*Optional Netflix-style view with horizontal genre rows and smooth scrolling*

---

> **Note:** All features are optional and can be enabled/disabled through the plugin settings in the Jellyfin Dashboard.

---

## Media Request System

The plugin includes a complete media request system that allows users to request movies and TV series from administrators.

### Request Media Button
![Request Media button in header](images/request-button.png)
*Sleek "Request Media" button with animated shine effect in the Jellyfin header*

### User Request Form
![User request form](images/request-form-user.png)
*Users can submit requests with title, type (Movie/TV Series/Anime), and additional notes*

### User Request List with Status
![User sees their requests with status](images/request-user-pending.png)
*Users can track all their requests with timestamps and current status (Pending/Processing/Done)*

### Admin Notification Badge
![Admin notification badge](images/request-admin-badge.png)
*Admins see a red notification badge showing the number of pending requests*

### Admin Request Management
![Admin request management panel](images/request-admin-panel.png)
*Admins can view all requests with user info, timestamps, and change status with one click*

### Completed Request with Watch Now
![Completed request with Watch Now button](images/request-user-done.png)
*When a request is fulfilled, users see a "Watch Now" button linking directly to the media*

---

## Features

### Star Rating System
- **1-10 star rating** for all media types (movies, TV shows, music, etc.)
- **Interactive UI** with smooth hover effects and instant feedback
- **Visual indicators** showing your rating and average community rating
- **Persistent ratings** saved per-user across all devices

### User Ratings Display
- **Hover popup** showing detailed ratings from all users
- **Username display** with individual ratings (e.g., "John: 8/10")
- **Rating statistics** including average rating and total number of ratings
- **Privacy-aware** - only shows ratings, not full user profiles

### Media Card Overlays
- **Rating badges** displayed on media cards (e.g., "7.5")
- **Lazy loading** using IntersectionObserver for optimal performance
- **Smart caching** prevents duplicate API requests
- **Optimized for large libraries** (tested with 15TB+ media collections)
- **Non-intrusive design** that doesn't interfere with Jellyfin's UI

### Media Request System
- **Request Button** - Animated "Request Media" button in the header
- **User Features**:
  - Submit requests for Movies, TV Series, or Anime
  - Add notes (e.g., "Season 2", "Need ASAP")
  - Track request status (Pending → Processing → Done)
  - See timestamps for when requested and completed
  - "Watch Now" button when request is fulfilled with link
- **Admin Features**:
  - Notification badge showing pending request count
  - View all requests with user info and notes
  - One-click status updates (Pending/Processing/Done)
  - Add media link when marking as Done
  - Delete requests permanently
  - See request and completion timestamps
- **Mobile Responsive** - Card layout and dropdown selector on mobile devices
- **Multi-user Support** - Cache clears on logout/account switch

### Performance Optimized
- **IntersectionObserver** loads ratings only for visible cards
- **Request caching** eliminates duplicate API calls
- **Efficient DOM handling** prevents UI lag
- **Minimal server load** even with thousands of media items

---

## Installation

1. **Add Plugin Repository**
   - Go to Jellyfin Dashboard → Plugins → Repositories
   - Add repository URL: `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json`

2. **Install Plugin**
   - Go to Plugins → Catalog
   - Find "Ratings" and click Install
   - Restart Jellyfin server

3. **Automatic Setup**
   - Plugin automatically injects rating UI on detail pages
   - Request Media button appears in the header
   - No manual configuration required
   - Works immediately after server restart

---

## Usage

### Rating Media
1. Open any movie, TV show, or media item
2. Find the rating stars below the title/logo
3. Click a star (1-10) to submit your rating
4. Your rating is saved immediately

### Viewing Ratings
- **Your rating**: Displayed with highlighted stars
- **Average rating**: Shown as "X.X/10" with total count
- **All user ratings**: Hover over stars to see detailed popup
- **Card badges**: Rating badges appear on media thumbnails automatically

### Requesting Media (Users)
1. Click the "Request Media" button in the header
2. Fill in the media title (required)
3. Select the type (Movie, TV Series, Anime, etc.)
4. Add any additional notes (season number, year, etc.)
5. Click "Submit Request"
6. Track your requests in the "Your Requests" section

### Managing Requests (Admins)
1. Click the "Request Media" button (shows pending count)
2. View all user requests with details
3. Update status: Pending → Processing → Done
4. When marking as Done, paste the media URL first
5. Users will see a "Watch Now" button linking to the media
6. Delete requests using the trash button

---

## Technical Details

### Requirements
- **Jellyfin**: 10.11.0 or higher
- **.NET**: 9.0
- **Browser**: Modern browser with JavaScript enabled

### Architecture
- **Backend**: ASP.NET Core controller with RESTful API
- **Frontend**: Vanilla JavaScript (no dependencies)
- **Storage**: JSON-based file storage in plugin data directory
- **Authentication**: Jellyfin's built-in authentication system

### API Endpoints

#### Ratings
- `POST /Ratings/Items/{itemId}/Rating?rating={1-10}` - Submit rating
- `GET /Ratings/Items/{itemId}/Stats` - Get rating statistics
- `GET /Ratings/Items/{itemId}/DetailedRatings` - Get all user ratings
- `DELETE /Ratings/Items/{itemId}/Rating` - Delete your rating

#### Media Requests
- `POST /Ratings/Requests` - Create new request
- `GET /Ratings/Requests` - Get all requests
- `POST /Ratings/Requests/{requestId}/Status?status={status}&mediaLink={url}` - Update status
- `DELETE /Ratings/Requests/{requestId}` - Delete request

### Performance Characteristics
- **Initial load**: ~1.5 seconds delay for page stability
- **Per-card overhead**: Single cached API request per unique item
- **Memory usage**: Minimal (~1MB for 1000 cached ratings)
- **Server load**: Negligible (lazy loading prevents request storms)

---

## Development

### Building from Source
```bash
git clone https://github.com/K3ntas/jellyfin-plugin-ratings.git
cd jellyfin-plugin-ratings
dotnet build -c Release
```

### Project Structure
```
├── Api/                    # API controllers
│   └── RatingsController.cs
├── Data/                   # Data layer
│   └── RatingsRepository.cs
├── Models/                 # Data models
│   ├── Rating.cs
│   └── MediaRequest.cs
├── Web/                    # Frontend assets
│   └── ratings.js         # Main client-side script
├── Configuration/          # Plugin config pages
├── images/                 # README screenshots
└── manifest.json          # Plugin catalog manifest
```

---

## License

Licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## Support

**Issues**: https://github.com/K3ntas/jellyfin-plugin-ratings/issues

---

## Version History

### 1.0.150.0 (Current)
- Fix notifications not showing in real-time
- Now listens to both ItemAdded and ItemUpdated events
- Catches metadata/image updates after initial item creation

### 1.0.149.0
- Fix duplicate notifications: only notify when image/metadata is ready
- Prevent same item from being notified twice within 1 hour

### 1.0.148.0
- Clean titles: automatically removes IMDB IDs like `[tt14364480]` from notification titles

### 1.0.147.0
- Removed test notification button from web UI
- Use TV app (JellyPush) for testing notifications instead

### 1.0.78.0
- Added "Watch Now" button for admins on completed requests
- Updated README with comprehensive Request Media documentation
- Added new screenshots showing the request system

### 1.0.77.0
- Fixed button initialization with retry logic
- Smarter login page detection
- SPA navigation support

### 1.0.76.0
- Button hides on login/startup pages
- Cache clears on logout and account switch

### 1.0.75.0
- Full request management: delete, timestamps, media links
- "Watch Now" button for users when request fulfilled

### 1.0.74.0
- Mobile-friendly admin table with card layout
- Dropdown status selector on mobile

### 1.0.73.0
- Animation fixes for shine effect

### 1.0.70.0 - 1.0.72.0
- Request Media button positioning and styling
- Mobile responsive design
- Badge notification system

### 1.0.61.0
- Production-ready ratings release
- Optimized performance

---

## Contributing

This is a personal project created for the Jellyfin community. Bug reports and feature requests are welcome via GitHub Issues.

## Acknowledgments

Built for the Jellyfin community with love.

Special thanks to the Jellyfin team for creating an amazing open-source media server platform.
