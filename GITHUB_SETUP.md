# GitHub Repository Setup Guide

Your Jellyfin Ratings Plugin is ready to be pushed to GitHub!

## Step 1: Create GitHub Repository

1. Go to https://github.com/new
2. **Repository name**: `jellyfin-plugin-ratings`
3. **Description**: `Jellyfin plugin for rating movies, series, and media with 1-10 star system and hover popup showing user ratings`
4. **Visibility**: Public
5. **DO NOT** initialize with README, .gitignore, or license (we already have them)
6. Click **Create repository**

## Step 2: Push to GitHub

After creating the repository, run these commands:

```bash
cd /c/Users/karol/Desktop/K3ntas

# Add GitHub as remote (replace YOUR_USERNAME with your actual GitHub username)
git remote add origin https://github.com/YOUR_USERNAME/jellyfin-plugin-ratings.git

# Push to GitHub
git push -u origin master
```

**Alternative with SSH:**
```bash
git remote add origin git@github.com:YOUR_USERNAME/jellyfin-plugin-ratings.git
git push -u origin master
```

## Step 3: Verify Upload

1. Go to your repository: `https://github.com/YOUR_USERNAME/jellyfin-plugin-ratings`
2. You should see all files
3. Check that README.md displays correctly

## Step 4: Update Repository URLs

If you're using a different username than "K3ntas", you'll need to update URLs in:

1. **README.md** - Update all GitHub links
2. **manifest.json** - Update sourceUrl and imageUrl
3. **CONTRIBUTING.md** - Update repository links
4. **PROJECT_SUMMARY.md** - Update repository link

Use Find & Replace:
- Find: `K3ntas`
- Replace: `YOUR_USERNAME`

## Step 5: Create First Release

1. Go to your repository on GitHub
2. Click **Releases** → **Create a new release**
3. **Tag version**: `v1.0.0.0`
4. **Release title**: `v1.0.0.0 - Initial Release`
5. **Description**:
   ```
   ## Features
   - 1-10 star rating system for all media types
   - Hover popup displaying all user ratings with usernames
   - Average rating and statistics display
   - REST API endpoints for rating management
   - Configuration page in Jellyfin Dashboard
   - Real-time UI updates

   ## Installation
   See [README.md](README.md) for installation instructions.
   ```

6. **Attach files**:
   - Build the plugin: `dotnet build --configuration Release`
   - Create a ZIP file containing:
     - `bin/Release/net8.0/Jellyfin.Plugin.Ratings.dll`
   - Name it: `jellyfin-plugin-ratings_1.0.0.0.zip`
   - Upload to release

7. Click **Publish release**

## Step 6: Update manifest.json

After creating the release:

1. Edit [manifest.json](manifest.json)
2. Update the `checksum` field:
   ```bash
   # On Linux/Mac
   md5sum jellyfin-plugin-ratings_1.0.0.0.zip

   # On Windows (PowerShell)
   Get-FileHash jellyfin-plugin-ratings_1.0.0.0.zip -Algorithm MD5
   ```
3. Replace `"checksum": "00000000000000000000000000000000"` with actual MD5 hash
4. Commit and push:
   ```bash
   git add manifest.json
   git commit -m "Update manifest with release checksum"
   git push
   ```

## Step 7: Add Plugin Logo (Optional)

1. Create a logo image (512x512px recommended)
2. Save as `images/logo.png`
3. Commit and push:
   ```bash
   git add images/logo.png
   git commit -m "Add plugin logo"
   git push
   ```

## Step 8: Test Plugin Installation

Add your repository to Jellyfin:

1. Open Jellyfin Dashboard
2. Go to **Plugins** → **Repositories**
3. Click **+** to add repository
4. **Repository Name**: `Ratings Plugin`
5. **Repository URL**:
   ```
   https://raw.githubusercontent.com/YOUR_USERNAME/jellyfin-plugin-ratings/master/manifest.json
   ```
   (Use `main` instead of `master` if that's your default branch)
6. Click **Save**
7. Go to **Catalog** and verify your plugin appears
8. Try installing it!

## Jellyfin Plugin Repository Structure

Your repository now has the correct structure for Jellyfin:

```
jellyfin-plugin-ratings/
├── manifest.json              ← Required: Plugin catalog manifest
├── Api/                       ← Plugin source code
├── Configuration/
├── Data/
├── Models/
├── Web/
├── Plugin.cs
├── PluginServiceRegistrator.cs
├── Jellyfin.Plugin.Ratings.csproj
├── README.md                  ← Documentation
├── LICENSE                    ← MIT License
└── images/
    └── logo.png              ← Plugin icon (optional)
```

## Common Issues

### manifest.json not loading
- Make sure it's in the root directory
- Check JSON syntax is valid
- Verify the raw URL is accessible

### Plugin not appearing in catalog
- Check manifest.json is on the `master` or `main` branch
- Verify the repository URL ends with `/manifest.json`
- Clear Jellyfin plugin cache

### Download fails
- Verify release ZIP file exists
- Check sourceUrl in manifest.json matches release URL
- Update checksum after creating release

## Next Steps

1. Share your repository URL with the Jellyfin community
2. Add to unofficial plugin lists
3. Consider submitting to official Jellyfin plugin repository
4. Monitor issues and respond to user feedback
5. Plan future features and improvements

## Support

If you need help:
- Check [CONTRIBUTING.md](CONTRIBUTING.md) for contribution guidelines
- See [INSTALLATION.md](INSTALLATION.md) for installation help
- Read [README.md](README.md) for full documentation

---

**Repository**: https://github.com/YOUR_USERNAME/jellyfin-plugin-ratings
**Manifest**: https://raw.githubusercontent.com/YOUR_USERNAME/jellyfin-plugin-ratings/master/manifest.json

Replace `YOUR_USERNAME` with your actual GitHub username!
