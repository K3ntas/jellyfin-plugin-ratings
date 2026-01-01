# Usage Guide

## Rating Media

### How to Rate

1. Open any movie, TV show, or media item in Jellyfin
2. Find the rating stars below the title/logo
3. Hover over the stars to preview your rating
4. Click a star (1-10) to submit your rating
5. Your rating is saved immediately

![Rating filled stars](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-filled-stars.png)

### Viewing Ratings

- **Your rating**: Displayed with highlighted/filled stars
- **Average rating**: Shown as "X.X/10" with total count
- **All user ratings**: Hover over stars to see detailed popup

### Changing Your Rating

Simply click a different star to update your rating.

### Deleting Your Rating

Click your currently selected star again to remove your rating.

---

## Card Overlays

Rating badges automatically appear on media cards throughout Jellyfin.

![Card badges](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/rating-card-badges.png)

- Badges show the average rating (e.g., "7.5")
- Only cards with ratings display badges
- Badges load lazily for performance

---

## New Media Notifications

### Enabling/Disabling

Use the toggle switch in the Jellyfin header (near the search field) to enable or disable notifications.

### What Gets Notified

- New movies added to your library
- New TV series added
- New episodes (grouped if multiple at once)

### Notification Format

- **Movies**: Title (Year)
- **Series**: Series Name (Year)
- **Episodes**: "Series Name S01 - Episode X" or "Episodes X-Y"

### Clicking Notifications

Click a notification to navigate directly to that media item.

---

## Media Request System

### For Users

#### Submitting a Request

1. Click the **"Request Media"** button in the header
2. Fill in the form:
   - **Title** (required): Name of the movie or show
   - **Type**: Movie, TV Series, Anime, Documentary, etc.
   - **Notes** (optional): Season info, year, any details
3. Click **Submit Request**

![User request form](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-form-user.png)

#### Tracking Your Requests

Your requests appear in the "Your Requests" section:
- **Pending**: Request submitted, waiting for admin
- **Processing**: Admin is working on it
- **Done**: Request fulfilled!

![User pending request](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-user-pending.png)

#### When Request is Fulfilled

When an admin marks your request as "Done" with a media link:
- Status changes to "Done" with timestamp
- A **"Watch Now"** button appears
- Click to go directly to the media

![Completed request](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-user-done.png)

---

### For Admins

#### Notification Badge

Admins see a red badge on the "Request Media" button showing the number of pending requests.

![Admin badge](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-admin-badge.png)

#### Managing Requests

1. Click the **"Request Media"** button
2. View all user requests with:
   - User who requested
   - Title and type
   - Notes
   - Request timestamp
3. Use the dropdown to change status:
   - **Pending** → **Processing** → **Done**

![Admin panel](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/request-admin-panel.png)

#### Fulfilling a Request

1. Add the requested media to Jellyfin
2. Copy the media's URL from Jellyfin
3. In the request panel, paste the URL in the "Media Link" field
4. Change status to **Done**
5. User will see a "Watch Now" button

#### Deleting Requests

Click the trash icon to permanently delete a request.

---

## Netflix-Style View

If enabled in plugin settings, Movies and TV Shows pages display in Netflix-style horizontal rows organized by genre.

![Netflix view](https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/images/netflix-view.png)

- Use arrow buttons to scroll each row
- Rating badges appear on each card
- Click any card to open the media

---

## Settings

Access plugin settings via **Dashboard → Plugins → Ratings → Settings**

Available options:
- Enable/disable Netflix view
- Enable/disable Request Media button
- Enable/disable notifications
- Group episodes in notifications
