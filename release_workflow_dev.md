# Jellyfin Plugin Dev Branch Release Workflow

Development/testing workflow using the `dev` branch. Test releases go here before being merged to `main` for production.

---

## Overview

| Branch | Purpose | Manifest URL |
|--------|---------|--------------|
| `main` | Stable releases for all users | `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json` |
| `dev` | Test releases for development | `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/dev/manifest.json` |

---

## Initial Setup (One Time)

### 1. Create Dev Branch

```bash
git checkout main
git pull
git checkout -b dev
git push -u origin dev
```

### 2. Create Dev Manifest

Copy `manifest.json` to use dev branch URLs:

```json
{
  "versions": [
    {
      "version": "1.0.307.0",
      "changelog": "Testing new feature",
      "targetAbi": "10.11.0.0",
      "sourceUrl": "https://github.com/K3ntas/jellyfin-plugin-ratings/releases/download/v1.0.307.0-dev/jellyfin-plugin-ratings_1.0.307.0.zip",
      "checksum": "YOUR_MD5_CHECKSUM",
      "timestamp": "2026-02-19T12:00:00Z"
    }
  ]
}
```

### 3. Add Dev Repository to Jellyfin Test Server

In your test Jellyfin server:
1. Go to **Dashboard** → **Plugins** → **Repositories**
2. Add: `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/dev/manifest.json`
3. Name it "Ratings Plugin (Dev)"

---

## Dev Release Workflow

### 1. Make Changes on Dev Branch

```bash
# Switch to dev branch
git checkout dev

# Make your code changes
# Edit files...

# Update version in .csproj (use same version with -dev suffix conceptually)
# <Version>1.0.307.0</Version>
```

### 2. Build and Package

```bash
# Build
dotnet clean
dotnet build -c Release

# Create ZIP (Windows PowerShell)
Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.dll', 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.pdb' -DestinationPath 'jellyfin-plugin-ratings_1.0.307.0.zip' -Force

# Generate checksum
certutil -hashfile jellyfin-plugin-ratings_1.0.307.0.zip MD5
```

### 3. Update Dev Manifest

Edit `manifest.json` on dev branch with new version:

```json
{
  "version": "1.0.307.0",
  "changelog": "DEV: Testing chat improvements",
  "targetAbi": "10.11.0.0",
  "sourceUrl": "https://github.com/K3ntas/jellyfin-plugin-ratings/releases/download/v1.0.307.0-dev/jellyfin-plugin-ratings_1.0.307.0.zip",
  "checksum": "NEW_CHECKSUM_HERE",
  "timestamp": "2026-02-19T12:00:00Z"
}
```

### 4. Commit and Push to Dev

```bash
git add -A
git commit -m "DEV v1.0.307.0: Testing new feature

Work in progress - not for production

Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"

git push origin dev
```

### 5. Create Dev Release on GitHub

Use `-dev` suffix for pre-release tags:

```bash
gh release create v1.0.307.0-dev \
  jellyfin-plugin-ratings_1.0.307.0.zip \
  --title "v1.0.307.0-dev - Testing" \
  --notes "## Development Release

**DO NOT USE IN PRODUCTION**

### Changes Being Tested
- Feature X
- Bug fix Y

### Known Issues
- May have bugs
- Not fully tested" \
  --prerelease \
  --target dev
```

### 6. Test on Jellyfin Test Server

1. Go to **Dashboard** → **Plugins** → **Catalog**
2. Find "Ratings" (from dev repository)
3. Click **Update** or **Install**
4. Restart Jellyfin
5. Test your changes

### 7. Iterate as Needed

Repeat steps 1-6 until the feature is ready. Each iteration:
- Increment version: 1.0.307.0 → 1.0.308.0 → 1.0.309.0
- Create new `-dev` release
- Test on dev server

---

## Promoting Dev to Production

When testing is complete and the feature is ready:

### 1. Merge Dev to Main

```bash
git checkout main
git pull
git merge dev
git push
```

### 2. Create Production Release

Follow the standard `release_workflow.md` process:
- Create release **without** `-dev` suffix
- Update main `manifest.json`
- Create proper release notes

### 3. Clean Up Dev Releases (Optional)

Delete old dev releases to keep things tidy:

```bash
# Delete a specific dev release
gh release delete v1.0.307.0-dev --yes

# Delete the tag too
git push origin :refs/tags/v1.0.307.0-dev
```

---

## Quick Reference

### Switch Between Branches

```bash
# Switch to dev for testing
git checkout dev

# Switch to main for production
git checkout main
```

### Dev Release Command

```bash
gh release create v1.0.XXX.0-dev \
  jellyfin-plugin-ratings_1.0.XXX.0.zip \
  --title "v1.0.XXX.0-dev - Testing" \
  --notes "Development release - testing only" \
  --prerelease \
  --target dev
```

### Production Release Command

```bash
gh release create v1.0.XXX.0 \
  jellyfin-plugin-ratings_1.0.XXX.0.zip \
  --title "v1.0.XXX.0 - Feature Name" \
  --notes "Production release notes" \
  --target main
```

---

## Best Practices

1. **Always test on dev first** - Never push untested code to main
2. **Use `-dev` suffix** for dev release tags to distinguish them
3. **Mark as prerelease** - GitHub will show them differently
4. **Clean up old dev releases** - Don't let them pile up
5. **Keep dev manifest separate** - Dev manifest points to dev releases
6. **Version numbers** - Dev and main can have same versions, distinguished by tag suffix

---

## Manifest URLs Summary

| Environment | URL |
|-------------|-----|
| **Production** | `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json` |
| **Development** | `https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/dev/manifest.json` |

Users add the production URL. You add the development URL to your test server.

---

## Troubleshooting

### Plugin not showing in dev catalog
- Check dev manifest.json is valid JSON
- Verify sourceUrl points to existing release
- Verify checksum matches the ZIP file

### Dev changes appearing in production
- Make sure you're on the correct branch
- Check which manifest URL your servers are using

### Merge conflicts
```bash
git checkout dev
git merge main  # Get latest main changes into dev first
# Resolve conflicts
git commit
git push origin dev
```
