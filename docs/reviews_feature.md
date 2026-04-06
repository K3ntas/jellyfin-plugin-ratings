# User Reviews & Like/Dislike Feature (v2.0.219.0)

This document describes the user reviews system for implementing in other clients (e.g., Android player).

## Overview

Users can:
1. Rate media items (1-10 stars) with optional review text
2. View all ratings for an item with review indicators
3. Read other users' reviews
4. Like or dislike other users' reviews (not their own)
5. Edit their own rating and review

---

## Data Models

### UserRating (Extended)
```json
{
  "Id": "guid",
  "UserId": "guid",
  "ItemId": "guid",
  "Rating": 8,
  "ReviewText": "Great movie, loved the cinematography...",
  "CreatedAt": "2026-04-06T21:00:00Z",
  "UpdatedAt": "2026-04-06T21:30:00Z",
  "TmdbId": "12345",
  "ImdbId": "tt1234567",
  "AniDbId": null
}
```

### UserRatingDetail (API Response)
```json
{
  "UserId": "guid",
  "Username": "JohnDoe",
  "Rating": 8,
  "CreatedAt": "2026-04-06T21:00:00Z",
  "ReviewText": "Great movie, loved the cinematography...",
  "HasReview": true,
  "LikeCount": 5,
  "DislikeCount": 1,
  "UserLiked": true
}
```

**UserLiked values:**
- `true` = current user liked this review
- `false` = current user disliked this review
- `null` = current user has not voted

### ReviewLike (Internal Storage)
```json
{
  "Id": "guid",
  "ReviewerUserId": "guid",
  "ItemId": "guid",
  "UserId": "guid",
  "IsLike": true,
  "CreatedAt": "2026-04-06T21:00:00Z"
}
```

---

## API Endpoints

### 1. Submit Rating with Review

**Endpoint:** `POST /Ratings/Items/{itemId}/Rating`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| rating | int | Yes | Rating value 1-10 |
| review | string | No | Review text (max 2000 chars) |

**Example Request:**
```
POST /Ratings/Items/abc123/Rating?rating=8&review=Great%20movie!
X-Emby-Authorization: MediaBrowser Client="...", Token="..."
```

**Response:** `200 OK`
```json
{
  "Id": "guid",
  "UserId": "guid",
  "ItemId": "abc123",
  "Rating": 8,
  "ReviewText": "Great movie!",
  "CreatedAt": "2026-04-06T21:00:00Z",
  "UpdatedAt": "2026-04-06T21:00:00Z"
}
```

**Notes:**
- Creates new rating or updates existing
- Review text is sanitized (HTML stripped, max 2000 chars)
- Pass empty string to clear review: `&review=`

---

### 2. Update Review Only

**Endpoint:** `PUT /Ratings/Items/{itemId}/Review`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| review | string | No | New review text (empty to clear) |

**Example Request:**
```
PUT /Ratings/Items/abc123/Review?review=Updated%20review%20text
X-Emby-Authorization: MediaBrowser Client="...", Token="..."
```

**Response:** `200 OK` - Updated UserRating object

**Error Responses:**
- `404 Not Found` - "Rating not found. Please rate the item first."
- `401 Unauthorized` - User not authenticated

**Notes:**
- Only updates review text, not the rating value
- User must have an existing rating for the item

---

### 3. Get All Ratings with Reviews

**Endpoint:** `GET /Ratings/Items/{itemId}/DetailedRatings`

**Response:** `200 OK`
```json
[
  {
    "UserId": "user1-guid",
    "Username": "JohnDoe",
    "Rating": 9,
    "CreatedAt": "2026-04-05T10:00:00Z",
    "ReviewText": "Amazing film!",
    "HasReview": true,
    "LikeCount": 12,
    "DislikeCount": 2,
    "UserLiked": null
  },
  {
    "UserId": "user2-guid",
    "Username": "JaneSmith",
    "Rating": 7,
    "CreatedAt": "2026-04-06T15:00:00Z",
    "ReviewText": null,
    "HasReview": false,
    "LikeCount": 0,
    "DislikeCount": 0,
    "UserLiked": null
  }
]
```

**Notes:**
- Ordered by Rating (descending), then Username (ascending)
- `UserLiked` shows current user's vote on each review
- `HasReview` is convenience boolean for `ReviewText != null`

---

### 4. Like/Dislike a Review

**Endpoint:** `POST /Ratings/Reviews/{reviewerUserId}/{itemId}/Like`

**Query Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| isLike | bool | Yes | `true` for like, `false` for dislike |

**Example Request:**
```
POST /Ratings/Reviews/user1-guid/abc123/Like?isLike=true
X-Emby-Authorization: MediaBrowser Client="...", Token="..."
```

**Response:** `200 OK`
```json
{
  "LikeCount": 13,
  "DislikeCount": 2,
  "UserLiked": true
}
```

**Error Responses:**
- `400 Bad Request` - "Cannot like your own review"
- `404 Not Found` - "Review not found" (no review text exists)
- `401 Unauthorized` - User not authenticated

**Behavior:**
- **First click:** Creates like/dislike
- **Same action again:** Removes vote (toggle off)
- **Opposite action:** Changes vote (like → dislike or vice versa)

**Example Flow:**
1. User clicks Like → `UserLiked: true`, LikeCount++
2. User clicks Like again → `UserLiked: null`, LikeCount-- (removed)
3. User clicks Dislike → `UserLiked: false`, DislikeCount++
4. User clicks Like → `UserLiked: true`, DislikeCount--, LikeCount++

---

### 5. Delete Rating (Existing)

**Endpoint:** `DELETE /Ratings/Items/{itemId}/Rating`

**Notes:**
- Deletes rating AND review together
- Also removes all likes/dislikes on that review

---

## UI Flow (Reference Implementation)

### Rating Modal (When clicking stars)
```
┌─────────────────────────────────────┐
│  Rate This                      ✕   │
├─────────────────────────────────────┤
│     ★ ★ ★ ★ ★ ★ ★ ★ ☆ ☆           │
│            8/10                     │
│                                     │
│  Write a review (optional):         │
│  ┌─────────────────────────────┐   │
│  │ Great cinematography and... │   │
│  │                             │   │
│  └─────────────────────────────┘   │
│                                     │
│           [Cancel] [Submit Rating]  │
└─────────────────────────────────────┘
```

### All Ratings Modal (When clicking "X ratings")
```
┌─────────────────────────────────────┐
│  All Ratings                    ✕   │
├─────────────────────────────────────┤
│  JohnDoe (you)    9/10   💬  [Edit] │
│  JaneSmith        8/10   💬         │
│  BobWilson        7/10   —          │
│  AliceJones       6/10   💬         │
└─────────────────────────────────────┘

💬 = has review (clickable)
—  = no review (not clickable)
```

### Review Mini-Modal (When clicking 💬)
```
┌─────────────────────────────────────┐
│  Review                         ✕   │
├─────────────────────────────────────┤
│  JaneSmith                   8/10   │
│                                     │
│  ┌─────────────────────────────┐   │
│  │ Really enjoyed this film.   │   │
│  │ The plot twists were great  │   │
│  │ and the acting was superb.  │   │
│  └─────────────────────────────┘   │
│                                     │
│  [👍 12]  [👎 2]                    │
└─────────────────────────────────────┘

Buttons show active state when user has voted.
Disabled (grayed) for own review.
```

---

## Storage Files

### ratings.json
Contains all ratings with optional ReviewText field:
```json
[
  {
    "Id": "...",
    "UserId": "...",
    "ItemId": "...",
    "Rating": 8,
    "ReviewText": "My review...",
    "CreatedAt": "...",
    "UpdatedAt": "..."
  }
]
```

### review_likes.json
Contains all like/dislike votes:
```json
[
  {
    "Id": "...",
    "ReviewerUserId": "...",
    "ItemId": "...",
    "UserId": "...",
    "IsLike": true,
    "CreatedAt": "..."
  }
]
```

---

## Authentication

All endpoints require Jellyfin authentication via `X-Emby-Authorization` header:

```
X-Emby-Authorization: MediaBrowser Client="Android App", Device="Pixel 6", DeviceId="xxx", Version="1.0.0", Token="user-access-token"
```

---

## Important Notes for Android Implementation

1. **Review text sanitization** - Backend strips HTML and limits to 2000 chars, but client should also validate

2. **Can't like own review** - API returns 400, client should disable like/dislike buttons for own reviews

3. **Toggle behavior** - Clicking same action removes vote, UI should update accordingly

4. **HasReview convenience** - Use `HasReview` boolean instead of checking `ReviewText != null`

5. **UserLiked states:**
   - `true` → highlight like button
   - `false` → highlight dislike button
   - `null` → no highlight (no vote yet)

6. **Keyboard isolation** - On mobile, ensure review textarea doesn't trigger media playback shortcuts

7. **Offline consideration** - Ratings/reviews require network; cache DetailedRatings for offline viewing if needed
