# Installation Instructions for Docker/Synology

## Current Issue

The automatic JavaScript injection via `IHostedService` is not working in Jellyfin 10.11.0 Docker containers. This requires **manual Custom CSS injection**.

## Installation Steps

### 1. Install the Plugin

In Jellyfin:
1. Go to **Dashboard** → **Plugins** → **Repositories**
2. Add repository: `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json`
3. Go to **Catalog** tab
4. Find "Ratings" plugin and click **Install**
5. Restart Jellyfin

### 2. Configure Custom CSS (REQUIRED)

The plugin cannot automatically inject JavaScript in Docker, so you must add it manually via Custom CSS:

1. Go to **Dashboard** → **General** → **Branding**
2. Scroll to **Custom CSS** field
3. Add this code:

```html
</style><script src="/web/configurationpage?name=ratings.js"></script><style>
```

4. Click **Save**
5. **Hard refresh your browser** (Ctrl+F5 or Cmd+Shift+R)

### 3. Verify Installation

1. Open any movie or series page
2. You should see rating stars (1-10) appear
3. Check browser console (F12) for: `[Ratings Plugin] Initializing...`

### 4. Troubleshooting

**If rating stars don't appear:**

1. Check the Custom CSS is saved correctly
2. Hard refresh browser (Ctrl+F5)
3. Check browser console (F12) for JavaScript errors
4. Verify the plugin is installed and enabled in Dashboard → Plugins

**Check if JavaScript file is accessible:**
- Open: `http://your-jellyfin-url/web/configurationpage?name=ratings.js`
- You should see JavaScript code (not an error)

**For Synology/Docker users:**
- Ensure Jellyfin has restarted after plugin installation
- The plugin DLL should be in `/config/plugins/Ratings_1.0.7.0/` inside the container
- Check Jellyfin logs for any plugin loading errors

## Why Manual Injection is Required

Jellyfin 10.11.0 in Docker does not support `IPluginServiceRegistrator` or `IHostedService` for automatically injecting JavaScript into the web client. The Custom CSS workaround is the only reliable method until Jellyfin adds official plugin JavaScript injection support.

## API Endpoints

The plugin provides these REST API endpoints (these work automatically):

- `POST /Ratings/Items/{itemId}/Rating?rating=X` - Set rating (1-10)
- `GET /Ratings/Items/{itemId}/Stats` - Get rating statistics
- `GET /Ratings/Items/{itemId}/DetailedRatings` - Get all user ratings with usernames
- `GET /Ratings/Items/{itemId}/UserRating` - Get current user's rating
- `DELETE /Ratings/Items/{itemId}/Rating` - Delete rating

## Configuration

Go to **Dashboard** → **Plugins** → **Ratings** to configure:
- Enable/disable ratings
- Set min/max rating values (default: 1-10)
- Show average ratings to all users
