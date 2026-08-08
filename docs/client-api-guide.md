# Building a Profile Page — Client API Guide

For anyone writing a native client (Android TV, mobile, desktop) against the Ratings plugin.

**Requires plugin v1.0.369.0 / v2.0.354.0 or newer.** Earlier versions have the auth and casing bugs described in the changelog and will not work reliably from a non-web client.

---

## 0. Before anything: the two rules that used to break clients

**Authentication.** Send the token however Jellyfin normally does. All of these now work on every endpoint:

```
Authorization: MediaBrowser Client="AndroidTV", Device="Shield", DeviceId="...", Version="1.0", Token="<token>"
X-Emby-Token: <token>
X-Emby-Authorization: MediaBrowser ... Token="<token>"
Authorization: Bearer <token>
?api_key=<token>
```

API keys work too. Previously `/Social/*` accepted only the bare `X-Emby-Token` header and returned 401 for everything else — that is fixed.

**JSON casing is camelCase**, on every endpoint, always. `userId`, `totalRatings`, `averageRating`. If you built against an older version and added dual-name handling, you can drop it once you require this version.

---

## 1. Detect the plugin — one call, no auth

```http
GET /Social/Capabilities
```

```json
{
  "plugin": "Jellyfin.Plugin.Ratings",
  "version": "1.0.369.0",
  "features": ["profile", "profile.full", "ratings", "ratings.distribution",
               "reviews", "activity", "friends", "followers", "following",
               "notifications", "presence", "genres", "similarUsers",
               "lists", "chat", "directMessages", "capabilities", "pagination"],
  "enabled": { "ratings": true, "social": true, "chat": false, "requests": true },
  "presence": { "heartbeatSeconds": 30, "onlineWithinSeconds": 60,
                "awayWithinSeconds": 300, "offlineAfterSeconds": 300 },
  "jsonCasing": "camelCase"
}
```

Call this once at launch.

- **404 or connection error** → plugin not installed. Hide all plugin UI.
- **`enabled.social` is false** → the admin has social features off. Hide the profile page; don't show an error.
- **Check `features`, not the version number.** Feature strings are additive and never removed.

---

## 2. Render the profile — one call, not nine

```http
GET /Social/Profile/{userId}/Full
```

This is the endpoint to build your screen around. It replaces `MyProfile` + `Stats` + `Style` + `Likes` + `Genres` + counts.

```json
{
  "userId": "…", "username": "Kentas",
  "bio": "…", "avatarUrl": "…", "headerMediaUrl": "…", "headerMediaType": "video",
  "createdAt": "2026-04-11T…",
  "privacy": { … }, "style": { … },
  "onlineStatus": "Online",
  "watching": { "itemId": "…", "title": "…", "seriesName": "…",
                "episodeInfo": "S01E05", "positionTicks": 0, "durationTicks": 0 },
  "stats": { "totalRatings": 128, "averageRating": 7.4, "reviewCount": 12,
             "watchedMinutes": 48210, "watchedItems": 613 },
  "ratingDistribution": [2, 1, 4, 9, 14, 22, 31, 27, 12, 6],
  "topGenres": [ { "name": "Drama", "percent": 28.4 }, … ],
  "counts": { "friends": 6, "followers": 11, "following": 9, "profileLikes": 3 },
  "viewer": { "isSelf": false, "isFriend": true, "isFollowing": true }
}
```

`ratingDistribution` is a 10-element array, index 0 = rating 1. Use it directly for the histogram — do **not** use `/Ratings/RatingDistribution`, which is server-wide across all users.

Use `viewer` to decide which action buttons to show. Don't compare user IDs yourself.

---

## 3. The lists — already have posters

These come back with everything needed to draw a row. No per-item resolution.

```http
GET /Social/Profile/{userId}/Ratings?limit=30&offset=0
GET /Social/Profile/{userId}/Reviews?limit=30&offset=0
GET /Social/Profile/{userId}/Activity?limit=20
```

```json
{
  "userId": "…", "total": 128, "offset": 0, "limit": 30,
  "items": [{
    "id": "…", "itemId": "…",
    "rating": 9, "review": "…",
    "title": "Blade Runner 2049", "year": 2017, "mediaType": "Movie",
    "imageUrl": "/Items/abc…/Images/Primary",
    "inLibrary": true,
    "tmdbId": "335984", "imdbId": "tt1856101",
    "createdAt": "…", "updatedAt": "…"
  }]
}
```

**`imageUrl` is server-relative** — prefix your server address. Append sizing:
`{server}{imageUrl}?maxHeight=270&quality=90`

**Handle `inLibrary: false`.** The media was deleted from the server. `title` and `year` still come from the snapshot stored with the rating, and `imageUrl` may be an absolute TMDB URL. Show the entry, disable playback.

**Paginate.** Default 50, max 200. Use `total` for your scroll indicator.

---

## 4. Presence

```http
POST /Social/Heartbeat        every 30 seconds while the app is in the foreground
POST /Social/Offline          on logout or app exit
```

Server-side thresholds, from `capabilities.presence`:

| Since last heartbeat | Status |
|---|---|
| < 60s | `Online` |
| 60–300s | `Away` |
| > 300s | `Offline` |

**Stop the heartbeat when the app backgrounds** — otherwise a TV left on shows the user online forever.

To report what's playing:

```http
POST /Social/Watching     { "itemId": "…", "positionTicks": 0, "durationTicks": 0 }
POST /Social/StopWatching
```

Send `Watching` on play/resume/seek and roughly every 30s during playback; the plugin treats a record with no heartbeat for 5 minutes as stale and stops showing it.

---

## 5. Live updates

Use Jellyfin's existing WebSocket (`/socket`). The plugin sends:

| `MessageType` | Meaning |
|---|---|
| `SocialInitialStatus` | Full presence snapshot on connect |
| `SocialStatusUpdate` | One user went online/offline |
| `SocialWatchingUpdate` | One user started/stopped watching |
| `SocialProfileStatsUpdate` | Stats changed for a profile |

Payload is in `SocialData`. Refresh from these instead of polling. If you must poll, 30 seconds is plenty — and stop while backgrounded.

---

## 6. Everything else

```http
GET  /Social/Users?limit=200                       browse all users
GET  /Social/Friends
GET  /Social/Profile/{id}/Followers?limit=&offset=
GET  /Social/Profile/{id}/Following?limit=&offset=
GET  /Social/Notifications  ·  /Notifications/UnreadCount
GET  /Social/Profile/{id}/Genres?limit=8           genre breakdown by watch time
GET  /Social/Profile/{id}/SimilarUsers?limit=5     taste matching
POST /Social/Follow/{id}  ·  /Social/Block/{id}
POST /Ratings/Items/{itemId}/Rating?rating=8       rate an item
GET  /Ratings/Items/BatchStats?itemIds=a,b,c       up to 100 ids per call
```

**Followers/Following shape** — this was guessed wrong before, so to confirm:

```json
{ "followers": [ { "userId": "…", "username": "…", "avatarUrl": "…", "followedAt": "…" } ],
  "count": 20, "total": 137, "offset": 0, "limit": 20 }
```

The key is `followers` / `following` respectively — **not** `users`. `count` is the page size, `total` is the full count.

**Use `BatchStats`, never per-item `Stats`,** when drawing a grid. One call for 100 items.

---

## 7. Suggested startup sequence

```
1. GET /Social/Capabilities              once, at launch, cache for the session
2. if (!enabled.social) → hide profile UI, stop
3. GET /Social/Profile/{me}/Full         renders the whole page
4. GET …/Ratings?limit=30                first page only, lazily
5. connect WebSocket, start 30s heartbeat
```

That's **three** requests to first paint, versus nine before.

---

## 8. Known gaps

Honest about what isn't solved:

- **`style` is CSS-oriented.** `UserProfileStyle` carries ~30 CSS properties (`box-shadow`, font stacks) that won't map to a native client. Use `accentColor`, `backgroundColor` and text colours; ignore the rest. A portable subset is on the list, not done.
- **Error bodies are inconsistent.** Some endpoints return `{ "error": "…" }`, others `{ "message": "…" }`. Read both. Being unified, not done yet.
- **`SocialController` is one large file.** Cosmetic, but it's why the auth bug hid for so long.

Found something else? Open an issue — the report that prompted this guide was specific enough to act on directly, and that's exactly what's useful.
