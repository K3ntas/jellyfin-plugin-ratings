# Jellyfin Ratings Plugin - Installation Fix

## The Problem

The Custom CSS injection was trying to load `/Ratings/ratings.js` but this API controller endpoint was failing with a 500 error.

## The Solution

The plugin already implements `IHasWebPages` which registers the ratings.js file with Jellyfin's plugin page system. We need to use the correct URL for accessing plugin web pages.

## Updated Custom CSS

Go to **Dashboard → General → Custom CSS** and update the injection to:

```html
</style><script src="/web/configurationpage?name=ratings.js"></script><style>
```

**IMPORTANT:** Remove the old injection if it exists and replace it with the above.

## How It Works

1. The Plugin.cs file implements `IHasWebPages.GetPages()` which registers:
   - Name: `ratings.js`
   - Embedded Resource: `Jellyfin.Plugin.Ratings.Web.ratings.js`

2. Jellyfin serves registered plugin pages at:
   `/web/configurationpage?name={page-name}`

3. The Custom CSS injection adds a script tag that loads the JavaScript from this URL

4. The JavaScript automatically:
   - Detects media detail pages
   - Injects rating stars (1-10)
   - Shows hover popup with all user ratings
   - Saves ratings via the API

## Verification

After updating the Custom CSS:

1. Save the Custom CSS changes
2. Hard refresh your browser (Ctrl+F5 or Cmd+Shift+R)
3. Open any movie or series page
4. You should see rating stars appear
5. Check browser console for `[Ratings Plugin] Initializing...`

## Alternative: Remove the RatingsController Endpoint

If the above doesn't work, we can remove the conflicting `/Ratings/ratings.js` API endpoint since it's not needed - Jellyfin's plugin page system handles serving the JavaScript file.

The API endpoints for setting/getting ratings (`/Ratings/Items/{itemId}/Rating`, etc.) will still work as they use different routes.
