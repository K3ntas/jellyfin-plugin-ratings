# API Documentation

The Ratings plugin exposes a RESTful API for all functionality. All endpoints require Jellyfin authentication.

---

## Base URL

```
http://your-jellyfin-server:8096/Ratings
```

---

## Authentication

All requests require a valid Jellyfin API key or authentication token in the header:

```
Authorization: MediaBrowser Token="your-token-here"
```

Or use the `X-Emby-Token` header.

---

## Ratings Endpoints

### Submit/Update Rating

**POST** `/Ratings/Items/{itemId}/Rating`

Submit or update a rating for a media item.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| itemId | GUID | Path | The media item ID |
| rating | int | Query | Rating value (1-10) |

**Example:**
```bash
curl -X POST "http://localhost:8096/Ratings/Items/abc123/Rating?rating=8" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:** `200 OK` on success

---

### Get Rating Statistics

**GET** `/Ratings/Items/{itemId}/Stats`

Get rating statistics for a media item.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| itemId | GUID | Path | The media item ID |

**Example:**
```bash
curl "http://localhost:8096/Ratings/Items/abc123/Stats" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:**
```json
{
  "averageRating": 7.5,
  "totalRatings": 12,
  "userRating": 8,
  "distribution": [0, 0, 1, 0, 2, 3, 2, 3, 1, 0]
}
```

---

### Get Detailed Ratings

**GET** `/Ratings/Items/{itemId}/DetailedRatings`

Get all user ratings for a media item.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| itemId | GUID | Path | The media item ID |

**Example:**
```bash
curl "http://localhost:8096/Ratings/Items/abc123/DetailedRatings" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:**
```json
{
  "ratings": [
    {
      "userId": "user-guid-1",
      "userName": "John",
      "rating": 8,
      "timestamp": "2025-12-30T10:00:00Z"
    },
    {
      "userId": "user-guid-2",
      "userName": "Jane",
      "rating": 7,
      "timestamp": "2025-12-29T15:30:00Z"
    }
  ]
}
```

---

### Delete Rating

**DELETE** `/Ratings/Items/{itemId}/Rating`

Delete the current user's rating for a media item.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| itemId | GUID | Path | The media item ID |

**Example:**
```bash
curl -X DELETE "http://localhost:8096/Ratings/Items/abc123/Rating" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:** `200 OK` on success

---

## Notification Endpoints

### Get Notifications

**GET** `/Ratings/Notifications`

Get new media notifications since a timestamp.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| since | ISO8601 | Query | Timestamp to get notifications after |

**Example:**
```bash
curl "http://localhost:8096/Ratings/Notifications?since=2025-12-30T10:00:00Z" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:**
```json
{
  "notifications": [
    {
      "id": "notif-guid",
      "itemId": "item-guid",
      "title": "The Matrix",
      "year": 1999,
      "type": "Movie",
      "imageUrl": "/Items/item-guid/Images/Primary",
      "createdAt": "2025-12-30T12:00:00Z"
    }
  ]
}
```

---

### Send Test Notification (Admin Only)

**POST** `/Ratings/Notifications/Test`

Send a test notification with a random media item from the library.

**Example:**
```bash
curl -X POST "http://localhost:8096/Ratings/Notifications/Test" \
  -H "Authorization: MediaBrowser Token=\"admin-token\""
```

**Response:** `200 OK` with notification data

---

### Get Plugin Config

**GET** `/Ratings/Config`

Get the plugin configuration.

**Example:**
```bash
curl "http://localhost:8096/Ratings/Config" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:**
```json
{
  "enableNotifications": true,
  "enableRequestButton": true,
  "enableNetflixView": false,
  "groupEpisodes": true
}
```

---

## Media Request Endpoints

### Create Request

**POST** `/Ratings/Requests`

Create a new media request.

**Body:**
```json
{
  "title": "The Matrix 5",
  "type": "Movie",
  "notes": "Coming out next year"
}
```

**Example:**
```bash
curl -X POST "http://localhost:8096/Ratings/Requests" \
  -H "Authorization: MediaBrowser Token=\"your-token\"" \
  -H "Content-Type: application/json" \
  -d '{"title":"The Matrix 5","type":"Movie","notes":"Coming out next year"}'
```

**Response:** `200 OK` with created request

---

### Get All Requests

**GET** `/Ratings/Requests`

Get all media requests (users see their own, admins see all).

**Example:**
```bash
curl "http://localhost:8096/Ratings/Requests" \
  -H "Authorization: MediaBrowser Token=\"your-token\""
```

**Response:**
```json
{
  "requests": [
    {
      "id": "request-guid",
      "userId": "user-guid",
      "userName": "John",
      "title": "The Matrix 5",
      "type": "Movie",
      "notes": "Coming out next year",
      "status": "Pending",
      "requestedAt": "2025-12-30T10:00:00Z",
      "completedAt": null,
      "mediaLink": null
    }
  ]
}
```

---

### Update Request Status (Admin Only)

**POST** `/Ratings/Requests/{requestId}/Status`

Update the status of a media request.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| requestId | GUID | Path | The request ID |
| status | string | Query | "Pending", "Processing", or "Done" |
| mediaLink | string | Query | (Optional) URL to the media in Jellyfin |

**Example:**
```bash
curl -X POST "http://localhost:8096/Ratings/Requests/req123/Status?status=Done&mediaLink=/web/index.html#!/details?id=item123" \
  -H "Authorization: MediaBrowser Token=\"admin-token\""
```

**Response:** `200 OK` on success

---

### Delete Request (Admin Only)

**DELETE** `/Ratings/Requests/{requestId}`

Delete a media request.

**Parameters:**
| Name | Type | Location | Description |
|------|------|----------|-------------|
| requestId | GUID | Path | The request ID |

**Example:**
```bash
curl -X DELETE "http://localhost:8096/Ratings/Requests/req123" \
  -H "Authorization: MediaBrowser Token=\"admin-token\""
```

**Response:** `200 OK` on success

---

## Error Responses

All endpoints return standard HTTP error codes:

| Code | Description |
|------|-------------|
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Invalid or missing authentication |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Item or request not found |
| 500 | Internal Server Error |

Error response body:
```json
{
  "error": "Error message description"
}
```
