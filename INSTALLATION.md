# Installation Guide - Jellyfin Ratings Plugin

## Quick Installation

### Windows

1. **Build the plugin**:
   ```powershell
   .\build.ps1
   ```

2. **Copy to Jellyfin plugins folder**:
   ```powershell
   # Create the plugin directory
   New-Item -ItemType Directory -Force "$env:APPDATA\Jellyfin\Server\plugins\Ratings_1.0.0.0"

   # Copy the DLL
   Copy-Item "bin\Release\net8.0\Jellyfin.Plugin.Ratings.dll" "$env:APPDATA\Jellyfin\Server\plugins\Ratings_1.0.0.0\"
   ```

3. **Restart Jellyfin Server**

### Linux/macOS

1. **Build the plugin**:
   ```bash
   chmod +x build.sh
   ./build.sh
   ```

2. **Copy to Jellyfin plugins folder**:
   ```bash
   # Create the plugin directory
   sudo mkdir -p /var/lib/jellyfin/plugins/Ratings_1.0.0.0

   # Copy the DLL
   sudo cp bin/Release/net8.0/Jellyfin.Plugin.Ratings.dll /var/lib/jellyfin/plugins/Ratings_1.0.0.0/

   # Set proper permissions
   sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/Ratings_1.0.0.0
   ```

3. **Restart Jellyfin**:
   ```bash
   sudo systemctl restart jellyfin
   ```

### Docker

1. **Build the plugin** on your host machine:
   ```bash
   dotnet build --configuration Release
   ```

2. **Copy to Docker volume**:
   ```bash
   # Find your Jellyfin config volume
   docker volume inspect jellyfin-config

   # Copy the plugin (adjust path based on your volume location)
   docker cp bin/Release/net8.0/Jellyfin.Plugin.Ratings.dll jellyfin-container:/config/plugins/Ratings_1.0.0.0/
   ```

3. **Restart container**:
   ```bash
   docker restart jellyfin-container
   ```

## Verification

After installation and restart:

1. Open Jellyfin Dashboard
2. Go to **Dashboard** → **Plugins**
3. You should see "Ratings" in the installed plugins list
4. Click on it to access configuration

## Configuration

1. Go to **Dashboard** → **Plugins** → **Ratings**
2. Configure settings:
   - ✓ Enable Ratings (checked by default)
   - Min Rating: 1
   - Max Rating: 10
   - Allow Guest Ratings: (your preference)
3. Click **Save**

## Testing

1. Navigate to any movie, series, or media item
2. Look for the "Rate This" section on the detail page
3. Click on a star (1-10) to rate
4. Hover over the stars to see the popup with all user ratings

## Troubleshooting

### Plugin not appearing in Dashboard

- Check file permissions (especially on Linux)
- Verify the DLL is in the correct location
- Check Jellyfin logs: `Dashboard → Advanced → Logs`
- Make sure you restarted Jellyfin completely

### Build errors

- Ensure .NET 8.0 SDK is installed:
  ```bash
  dotnet --version  # Should be 8.0.x or higher
  ```
- Install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0

### Jellyfin version compatibility

This plugin requires **Jellyfin 10.11.0** or higher. Check your version:
```
Dashboard → About
```

## Uninstallation

To remove the plugin:

1. Delete the plugin folder:
   - Windows: `%AppData%\Jellyfin\Server\plugins\Ratings_1.0.0.0`
   - Linux: `/var/lib/jellyfin/plugins/Ratings_1.0.0.0`

2. (Optional) Delete the ratings data:
   - Windows: `%AppData%\Jellyfin\Server\data\ratings`
   - Linux: `/var/lib/jellyfin/data/ratings`

3. Restart Jellyfin

## Support

If you encounter issues:
- Check the [README.md](README.md) for common solutions
- Review Jellyfin logs for error messages
- Open an issue on GitHub with:
  - Jellyfin version
  - Plugin version
  - Error messages from logs
  - Steps to reproduce
