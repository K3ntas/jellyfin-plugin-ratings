# Architecture Overview - Jellyfin Ratings Plugin

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       Jellyfin Web Client                        │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │               Media Detail Page (Item View)                │  │
│  │  ┌─────────────────────────────────────────────────────┐  │  │
│  │  │         Ratings Plugin UI (ratings.js)              │  │  │
│  │  │  ┌────────────────────────────────────────────┐     │  │  │
│  │  │  │    Star Rating Component (1-10 stars)      │     │  │  │
│  │  │  │  ★ ★ ★ ★ ★ ★ ★ ★ ★ ★                        │     │  │  │
│  │  │  │                                             │     │  │  │
│  │  │  │  Average: 8.5/10  -  12 ratings            │     │  │  │
│  │  │  │  Your rating: 9/10                          │     │  │  │
│  │  │  └────────────────────────────────────────────┘     │  │  │
│  │  │  ┌────────────────────────────────────────────┐     │  │  │
│  │  │  │   Hover Popup (on mouse over stars)        │     │  │  │
│  │  │  │  ┌──────────────────────────────────────┐  │     │  │  │
│  │  │  │  │ User Ratings                         │  │     │  │  │
│  │  │  │  │  Alice .................... 10/10   │  │     │  │  │
│  │  │  │  │  Bob ........................ 9/10   │  │     │  │  │
│  │  │  │  │  Charlie ................... 8/10    │  │     │  │  │
│  │  │  │  └──────────────────────────────────────┘  │     │  │  │
│  │  │  └────────────────────────────────────────────┘     │  │  │
│  │  └─────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            │ HTTP/REST API Calls
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Jellyfin Server (ASP.NET)                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              RatingsController (REST API)                 │  │
│  │  ┌────────────────────────────────────────────────────┐   │  │
│  │  │  POST   /Ratings/Items/{id}/Rating?rating={1-10}   │   │  │
│  │  │  GET    /Ratings/Items/{id}/Stats                  │   │  │
│  │  │  GET    /Ratings/Items/{id}/UserRating             │   │  │
│  │  │  GET    /Ratings/Items/{id}/DetailedRatings        │   │  │
│  │  │  DELETE /Ratings/Items/{id}/Rating                 │   │  │
│  │  └────────────────────────────────────────────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │                                     │
│                            ▼                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │              RatingsRepository (Data Layer)               │  │
│  │  ┌────────────────────────────────────────────────────┐   │  │
│  │  │  • SetRatingAsync(userId, itemId, rating)          │   │  │
│  │  │  • GetUserRating(userId, itemId)                   │   │  │
│  │  │  • GetItemRatings(itemId)                          │   │  │
│  │  │  • GetRatingStats(itemId, userId?)                 │   │  │
│  │  │  • DeleteRatingAsync(userId, itemId)               │   │  │
│  │  └────────────────────────────────────────────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
│                            │                                     │
│                            ▼                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                  JSON File Storage                        │  │
│  │         {jellyfin-data}/ratings/ratings.json              │  │
│  │  ┌────────────────────────────────────────────────────┐   │  │
│  │  │  [                                                  │   │  │
│  │  │    {                                                │   │  │
│  │  │      "Id": "guid",                                  │   │  │
│  │  │      "UserId": "user-guid",                         │   │  │
│  │  │      "ItemId": "item-guid",                         │   │  │
│  │  │      "Rating": 9,                                   │   │  │
│  │  │      "CreatedAt": "2024-01-15T10:30:00Z",          │   │  │
│  │  │      "UpdatedAt": "2024-01-15T10:30:00Z"           │   │  │
│  │  │    }                                                │   │  │
│  │  │  ]                                                  │   │  │
│  │  └────────────────────────────────────────────────────┘   │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Component Breakdown

### Frontend Layer (Web/ratings.js)

**Responsibilities:**
- Inject rating UI into media detail pages
- Handle user interactions (clicks, hovers)
- Display rating statistics
- Show popup with detailed user ratings
- Make API calls to backend

**Key Features:**
- Auto-detection of media detail pages
- Dynamic star highlighting
- Real-time updates
- XSS protection for usernames

### API Layer (Api/RatingsController.cs)

**Responsibilities:**
- Handle HTTP requests
- Validate user authentication
- Validate rating values
- Coordinate with repository layer
- Return JSON responses

**Endpoints:**
1. **Set Rating**: Create or update a user's rating
2. **Get Stats**: Retrieve aggregated statistics
3. **Get User Rating**: Get current user's rating
4. **Get Detailed Ratings**: Get all ratings with usernames
5. **Delete Rating**: Remove a user's rating

### Data Layer (Data/RatingsRepository.cs)

**Responsibilities:**
- Manage in-memory rating cache
- Persist ratings to disk (JSON)
- Calculate statistics
- Handle concurrent access with locking

**Key Operations:**
- Thread-safe read/write operations
- Automatic persistence
- Fast in-memory lookups
- Rating aggregation

### Models

**UserRating:**
- Basic rating information
- User-item relationship
- Timestamps

**RatingStats:**
- Aggregated statistics
- Average rating
- Distribution data
- User's personal rating

**UserRatingDetail:**
- Extended rating information
- Includes username
- For detailed displays

## Data Flow

### Rating Submission Flow

```
User clicks star (7/10)
        ↓
ratings.js sends POST request
        ↓
RatingsController.SetRating()
        ↓
Validate user & item
        ↓
RatingsRepository.SetRatingAsync()
        ↓
Update in-memory cache
        ↓
Save to JSON file (async)
        ↓
Return success to client
        ↓
ratings.js updates UI
        ↓
User sees updated rating
```

### Hover Popup Flow

```
User hovers over stars
        ↓
ratings.js detects hover event
        ↓
Send GET request to /DetailedRatings
        ↓
RatingsController.GetDetailedRatings()
        ↓
RatingsRepository.GetItemRatings()
        ↓
Lookup usernames via IUserManager
        ↓
Return list of {Username, Rating}
        ↓
ratings.js displays popup
        ↓
User sees all ratings with usernames
```

## Configuration Flow

```
Admin opens Dashboard → Plugins → Ratings
        ↓
Load configPage.html
        ↓
Fetch current PluginConfiguration
        ↓
Display settings form
        ↓
Admin changes settings
        ↓
Submit form
        ↓
Update PluginConfiguration
        ↓
Save to plugin config XML
        ↓
Settings applied immediately
```

## Integration Points

### Jellyfin Core Integration

1. **IUserManager**: Get user information and usernames
2. **ILibraryManager**: Verify media items exist
3. **IApplicationPaths**: Store data in Jellyfin data directory
4. **IPluginServiceRegistrator**: Register repository as singleton

### Web Client Integration

1. **Detail Pages**: Auto-detect and inject UI
2. **ApiClient**: Use Jellyfin's API client for requests
3. **Dashboard**: Display notifications
4. **Router**: Monitor navigation changes

## Security Considerations

1. **Authentication**: All write operations require user authentication
2. **Authorization**: Users can only modify their own ratings
3. **Input Validation**: Rating values validated against config
4. **XSS Protection**: Usernames escaped in HTML output
5. **CSRF Protection**: Leverages Jellyfin's built-in protection

## Performance Considerations

1. **In-Memory Cache**: Fast read access to ratings
2. **Async Persistence**: Non-blocking writes to disk
3. **Lazy Loading**: Ratings loaded only when needed
4. **Efficient Queries**: O(n) lookups with LINQ filtering

## Scalability

- **Memory**: ~100 bytes per rating
- **Disk**: JSON file, scales to thousands of ratings
- **Future**: Could migrate to SQLite for very large datasets

## Future Enhancements

Possible improvements:
- Database backend (SQLite/PostgreSQL)
- Rating analytics and trends
- Rating import/export
- Integration with recommendation engines
- Email notifications for new ratings
- Rating moderation features
- Half-star ratings (0.5 increments)
