# Jellyfin Ratings Plugin

[![GitHub release](https://img.shields.io/github/v/release/K3ntas/jellyfin-plugin-ratings)](https://github.com/K3ntas/jellyfin-plugin-ratings/releases)
[![License](https://img.shields.io/github/license/K3ntas/jellyfin-plugin-ratings)](https://github.com/K3ntas/jellyfin-plugin-ratings/blob/main/LICENSE)
[![Jellyfin Version](https://img.shields.io/badge/Jellyfin-10.11.0-blue)](https://jellyfin.org)

A comprehensive rating plugin for Jellyfin 10.11.0 that allows users to rate movies, series, episodes, music, and any other media with a 1-10 star rating system.

## Features

- **Star Rating System**: Rate any media item from 1 to 10 stars
- **User Ratings Display**: Hover over the rating area to see who rated what and their scores
- **Rating Statistics**: View average ratings and total rating count for each item
- **Personal Ratings**: See your own rating highlighted
- **Real-time Updates**: Ratings update immediately without page refresh
- **Configurable Settings**: Customize minimum/maximum rating values and permissions
- **Support for All Media Types**: Works with movies, series, episodes, music albums, tracks, and more

## Screenshots

The plugin adds a rating component to all media detail pages with:
- Interactive star rating UI
- Average rating display
- Total rating count
- Hover popup showing all users and their ratings

## Installation

### From Repository (Recommended)

1. Open Jellyfin Dashboard
2. Go to **Plugins** → **Repositories**
3. Add this repository URL:
   ```
   https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json
   ```
4. Go to **Catalog**
5. Find **Ratings** plugin
6. Click **Install**
7. Restart Jellyfin

### Manual Installation

1. Download the latest release DLL from [GitHub Releases](https://github.com/K3ntas/jellyfin-plugin-ratings/releases)
2. Place it in your Jellyfin plugins folder:
   - **Windows**: `%AppData%\Jellyfin\Server\plugins\Ratings_1.0.0.0\Jellyfin.Plugin.Ratings.dll`
   - **Linux**: `/var/lib/jellyfin/plugins/Ratings_1.0.0.0/Jellyfin.Plugin.Ratings.dll`
   - **Docker**: `/config/plugins/Ratings_1.0.0.0/Jellyfin.Plugin.Ratings.dll`
3. Restart Jellyfin

## Building from Source

### Prerequisites

- .NET 8.0 SDK
- Jellyfin 10.11.0 references

### Build Steps

1. Clone this repository:
   ```bash
   git clone https://github.com/K3ntas/jellyfin-plugin-ratings.git
   cd jellyfin-plugin-ratings
   ```

2. Build the project:
   ```bash
   dotnet build --configuration Release
   ```

3. The compiled DLL will be in `bin/Release/net8.0/Jellyfin.Plugin.Ratings.dll`

4. Copy to your Jellyfin plugins folder and restart Jellyfin

## Configuration

1. Go to **Dashboard** → **Plugins** → **Ratings**
2. Configure the following options:
   - **Enable Ratings**: Turn the rating system on/off
   - **Minimum Rating**: Set the minimum rating value (default: 1)
   - **Maximum Rating**: Set the maximum rating value (default: 10)
   - **Allow Guest Ratings**: Enable/disable ratings for guest users

## Usage

### Rating an Item

1. Navigate to any media item (movie, series, episode, etc.)
2. Scroll to the "Rate This" section
3. Click on a star (1-10) to submit your rating
4. Your rating is saved immediately

### Viewing Ratings

- **Average Rating**: Displayed next to the stars
- **Total Ratings**: Shows how many users have rated the item
- **Your Rating**: Your personal rating is highlighted and shown below
- **Detailed Ratings**: Hover over the stars area to see a popup with all users and their ratings

### Changing Your Rating

Simply click on a different star value to update your rating.

### Removing Your Rating

Use the DELETE endpoint via API or set a new rating.

## API Endpoints

The plugin provides REST API endpoints for integration:

### Set Rating
```
POST /Ratings/Items/{itemId}/Rating?rating={1-10}
Authorization: Required
```

### Get Rating Statistics
```
GET /Ratings/Items/{itemId}/Stats
Returns: Average rating, total count, distribution, and user's rating
```

### Get User's Rating
```
GET /Ratings/Items/{itemId}/UserRating
Authorization: Required
```

### Get Detailed Ratings (with usernames)
```
GET /Ratings/Items/{itemId}/DetailedRatings
Returns: List of all ratings with usernames
```

### Delete Rating
```
DELETE /Ratings/Items/{itemId}/Rating
Authorization: Required
```

## Data Storage

Ratings are stored in JSON format in the Jellyfin data directory:
- **Location**: `<jellyfin-data>/ratings/ratings.json`
- **Format**: JSON array of rating objects
- **Backup**: Recommended to include in your Jellyfin backup strategy

## Troubleshooting

### Ratings not appearing

1. Check that the plugin is enabled in Dashboard → Plugins
2. Verify JavaScript is enabled in your browser
3. Clear browser cache and reload the page
4. Check Jellyfin logs for any errors

### Cannot submit ratings

1. Ensure you are logged in (guest ratings may be disabled)
2. Check plugin configuration is set to "Enable Ratings"
3. Verify rating value is within configured min/max range
4. Check browser console for JavaScript errors

### Hover popup not showing

1. Ensure JavaScript is not blocked
2. Check that there are ratings to display
3. Try hovering directly over the stars container
4. Check browser console for errors

## Development

### Project Structure

```
jellyfin-plugin-ratings/
├── Api/
│   └── RatingsController.cs       # REST API endpoints
├── Configuration/
│   ├── PluginConfiguration.cs     # Plugin settings
│   └── configPage.html            # Configuration UI
├── Data/
│   └── RatingsRepository.cs       # Data access layer
├── Models/
│   ├── UserRating.cs              # Rating model
│   ├── RatingStats.cs             # Statistics model
│   └── UserRatingDetail.cs        # Detailed rating with username
├── Web/
│   └── ratings.js                 # Frontend JavaScript
├── Plugin.cs                       # Main plugin class
├── PluginServiceRegistrator.cs    # Dependency injection
└── Jellyfin.Plugin.Ratings.csproj # Project file
```

### Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This plugin is released under the MIT License. See LICENSE file for details.

## Support

For issues, questions, or feature requests, please:
- Open an issue on [GitHub Issues](https://github.com/K3ntas/jellyfin-plugin-ratings/issues)
- Check [existing issues](https://github.com/K3ntas/jellyfin-plugin-ratings/issues) for solutions
- Provide Jellyfin version and plugin version when reporting bugs

## Repository

- **GitHub**: https://github.com/K3ntas/jellyfin-plugin-ratings
- **Manifest**: https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json
- **Releases**: https://github.com/K3ntas/jellyfin-plugin-ratings/releases

## Changelog

### Version 1.0.0.0
- Initial release
- Star rating system (1-10)
- User ratings with username display
- Rating statistics and aggregation
- Hover popup showing all user ratings
- Configuration page
- REST API endpoints
- Support for all media types

## Acknowledgments

Built for Jellyfin 10.11.0. Thanks to the Jellyfin community for their support and contributions.
