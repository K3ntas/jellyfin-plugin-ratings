# Jellyfin Plugin Release Workflow

Complete step-by-step guide for building, versioning, releasing, and publishing Jellyfin plugins.

---

## Table of Contents

1. [Making Code Changes](#making-code-changes)
2. [Version Bump](#version-bump)
3. [Building the Plugin](#building-the-plugin)
4. [Creating Release Package](#creating-release-package)
5. [Updating Documentation](#updating-documentation)
6. [Git Commit and Push](#git-commit-and-push)
7. [Creating GitHub Release](#creating-github-release)
8. [Updating Plugin Manifest](#updating-plugin-manifest)
9. [Complete Example Workflow](#complete-example-workflow)
10. [Troubleshooting](#troubleshooting)

---

## Making Code Changes

### 1. Edit Your Code Files

Edit the necessary files for your feature/fix:
- Backend: `*.cs` files
- Frontend: `Web/*.js` files
- Styles: Embedded CSS in JavaScript

### 2. Test Locally

**Install locally during development:**

```bash
# Build the plugin
dotnet build -c Release

# Copy to Jellyfin plugins folder
# Windows:
copy bin\Release\net9.0\*.dll "C:\ProgramData\Jellyfin\Server\plugins\YourPlugin\"

# Linux:
cp bin/Release/net9.0/*.dll /var/lib/jellyfin/plugins/YourPlugin/

# Restart Jellyfin to test
```

---

## Version Bump

### Update Version Number

**File**: `YourPlugin.csproj`

```xml
<PropertyGroup>
    <Version>1.0.57.0</Version>  <!-- Increment this -->
</PropertyGroup>
```

**Versioning Strategy**:
- Major version (1.x.x.x): Breaking changes
- Minor version (x.1.x.x): New features
- Patch version (x.x.1.x): Bug fixes
- Build version (x.x.x.1): Hotfixes

---

## Building the Plugin

### Clean Build

```bash
# Clean previous builds
dotnet clean

# Build in Release mode
dotnet build -c Release
```

### Verify Build Output

Check that DLL and PDB files exist:

```bash
# Windows
dir bin\Release\net9.0\*.dll
dir bin\Release\net9.0\*.pdb

# Linux
ls bin/Release/net9.0/*.dll
ls bin/Release/net9.0/*.pdb
```

**Expected output**:
- `YourPlugin.dll` - Main plugin assembly
- `YourPlugin.pdb` - Debug symbols (optional for releases)

---

## Creating Release Package

### 1. Create ZIP Archive

**Windows (PowerShell)**:
```powershell
Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.YourPlugin.dll', 'bin\Release\net9.0\Jellyfin.Plugin.YourPlugin.pdb' -DestinationPath 'jellyfin-plugin-yourplugin_1.0.57.0.zip' -Force
```

**Linux**:
```bash
cd bin/Release/net9.0/
zip ../../../jellyfin-plugin-yourplugin_1.0.57.0.zip Jellyfin.Plugin.YourPlugin.dll Jellyfin.Plugin.YourPlugin.pdb
cd ../../../
```

### 2. Generate MD5 Checksum

**Windows**:
```bash
certutil -hashfile jellyfin-plugin-yourplugin_1.0.57.0.zip MD5
```

**Linux**:
```bash
md5sum jellyfin-plugin-yourplugin_1.0.57.0.zip
```

**Save the checksum** - you'll need it for the manifest.

**Example output**:
```
5d7940dd9f5918b1701235009b570013
```

---

## Updating Documentation

### 1. Update README.md

Add version to Version History section:

```markdown
## Version History

### 1.0.57.0 (Current)
- Brief description of changes
- Feature additions
- Bug fixes

### 1.0.56.0
- Previous version details
```

### 2. Update manifest.json

**File**: `manifest.json`

```json
{
  "versions": [
    {
      "version": "1.0.57.0",
      "changelog": "Brief description of changes for catalog",
      "targetAbi": "10.11.0.0",
      "sourceUrl": "https://github.com/YourUsername/plugin/releases/download/v1.0.57.0/jellyfin-plugin-yourplugin_1.0.57.0.zip",
      "checksum": "5d7940dd9f5918b1701235009b570013",
      "timestamp": "2025-11-17T12:00:00Z"
    }
  ]
}
```

**Important fields**:
- `version`: Must match .csproj version
- `changelog`: User-facing description
- `sourceUrl`: GitHub release download URL (update username and version)
- `checksum`: MD5 hash from previous step
- `timestamp`: Current UTC time in ISO 8601 format

---

## Git Commit and Push

### 1. Stage All Changes

```bash
git add -A
```

**What gets staged**:
- Modified source files
- Updated .csproj (version)
- Updated manifest.json
- Updated README.md
- New ZIP file

### 2. Create Commit

**Template for commit message**:

```bash
git commit -m "v1.0.57.0: Brief feature description

Detailed description of changes:
- Feature 1: What it does
- Feature 2: What it fixes
- Technical improvement: Implementation details

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

**Real example**:

```bash
git commit -m "v1.0.56.0: Moved rating badge to top-left corner

Fixed conflict with series episode count badges in top-right corner.

Changes:
- Updated CSS position from right: 5px to left: 5px
- Rating badges now visible on TV series cards
- No more overlap between episode count and rating badges

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

### 3. Push to GitHub

```bash
git push
```

**Or push to specific branch**:
```bash
git push origin main
```

---

## Creating GitHub Release

### Method 1: Using GitHub CLI (gh)

**Install gh** (if not installed):
- Windows: `winget install GitHub.cli`
- Linux: `sudo apt install gh`

**Create release**:

```bash
gh release create v1.0.57.0 \
  jellyfin-plugin-yourplugin_1.0.57.0.zip \
  --title "v1.0.57.0 - Feature Name" \
  --notes "## Release Description

### Changes
- Feature 1
- Feature 2
- Bug fix 3

### Installation
1. Update through Jellyfin Dashboard
2. Restart Jellyfin server

🤖 Generated with [Claude Code](https://claude.com/claude-code)"
```

### Method 2: Using curl (GitHub API)

**Step 1: Create Release**

```bash
curl -X POST \
  -H "Authorization: token YOUR_GITHUB_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/YourUsername/YourPlugin/releases \
  -d '{
    "tag_name": "v1.0.57.0",
    "name": "v1.0.57.0 - Feature Name",
    "body": "## Release Description\n\n### Changes\n- Feature 1\n- Feature 2\n\n🤖 Generated with [Claude Code](https://claude.com/claude-code)",
    "draft": false,
    "prerelease": false
  }'
```

**Save the response** - you need the `id` field.

**Step 2: Upload ZIP Asset**

```bash
# Get release ID from previous response (e.g., 263640955)
RELEASE_ID=263640955

curl -X POST \
  -H "Authorization: token YOUR_GITHUB_TOKEN" \
  -H "Content-Type: application/zip" \
  --data-binary @jellyfin-plugin-yourplugin_1.0.57.0.zip \
  "https://uploads.github.com/repos/YourUsername/YourPlugin/releases/${RELEASE_ID}/assets?name=jellyfin-plugin-yourplugin_1.0.57.0.zip"
```

### Method 3: Manual (GitHub Web Interface)

1. Go to `https://github.com/YourUsername/YourPlugin/releases/new`
2. **Tag**: `v1.0.57.0`
3. **Release title**: `v1.0.57.0 - Feature Name`
4. **Description**: Write release notes
5. **Attach files**: Upload ZIP file
6. Click **Publish release**

---

## Updating Plugin Manifest

### After GitHub Release

**Update manifest.json `sourceUrl`** to point to the newly created release:

```json
"sourceUrl": "https://github.com/YourUsername/YourPlugin/releases/download/v1.0.57.0/jellyfin-plugin-yourplugin_1.0.57.0.zip"
```

### Commit Manifest Update

```bash
git add manifest.json
git commit -m "Update manifest.json with v1.0.57.0 release URL"
git push
```

---

## Complete Example Workflow

Here's a **complete real-world example** from start to finish:

### Scenario: Adding a new feature to move rating badge position

```bash
# 1. Make code changes
# Edit Web/ratings.js - change position from right to left

# 2. Update version in .csproj
# Change <Version>1.0.55.0</Version> to <Version>1.0.56.0</Version>

# 3. Build
dotnet clean
dotnet build -c Release

# 4. Create ZIP
powershell -Command "Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.dll', 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.pdb' -DestinationPath 'jellyfin-plugin-ratings_1.0.56.0.zip' -Force"

# 5. Generate checksum
certutil -hashfile jellyfin-plugin-ratings_1.0.56.0.zip MD5
# Output: 5d7940dd9f5918b1701235009b570013

# 6. Update README.md
# Add version history entry

# 7. Update manifest.json
# Update version, changelog, checksum, timestamp

# 8. Git commit and push
git add -A
git commit -m "v1.0.56.0: Moved rating badge to top-left corner

Fixed conflict with series episode count badges in top-right corner.

Changes:
- Updated CSS position from right: 5px to left: 5px
- Rating badges now visible on TV series cards
- No more overlap between episode count and rating badges

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"

git push

# 9. Create GitHub release
curl -X POST \
  -H "Authorization: token gho_YOUR_TOKEN_HERE" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/K3ntas/jellyfin-plugin-ratings/releases \
  -d '{
    "tag_name": "v1.0.56.0",
    "name": "v1.0.56.0 - Badge Position Fix",
    "body": "## Moved Rating Badge to Top-Left\n\nFixed conflict with series episode count badges.\n\n### Changes\n- Moved badge from top-right to top-left\n- No more overlap on series cards\n\n🤖 Generated with [Claude Code](https://claude.com/claude-code)",
    "draft": false,
    "prerelease": false
  }'

# Save the response, get release ID (e.g., 263640955)

# 10. Upload ZIP to release
curl -X POST \
  -H "Authorization: token gho_YOUR_TOKEN_HERE" \
  -H "Content-Type: application/zip" \
  --data-binary @jellyfin-plugin-ratings_1.0.56.0.zip \
  "https://uploads.github.com/repos/K3ntas/jellyfin-plugin-ratings/releases/263640955/assets?name=jellyfin-plugin-ratings_1.0.56.0.zip"

# Done! Release is now live at:
# https://github.com/K3ntas/jellyfin-plugin-ratings/releases/tag/v1.0.56.0
```

---

## Automation Scripts

### Quick Release Script (Windows PowerShell)

Save as `release.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$true)]
    [string]$Description
)

# Build
Write-Host "Building version $Version..." -ForegroundColor Green
dotnet clean
dotnet build -c Release

# Create ZIP
$zipName = "jellyfin-plugin-yourplugin_$Version.zip"
Write-Host "Creating $zipName..." -ForegroundColor Green
Compress-Archive -Path "bin\Release\net9.0\*.dll", "bin\Release\net9.0\*.pdb" -DestinationPath $zipName -Force

# Generate checksum
Write-Host "Generating checksum..." -ForegroundColor Green
$checksum = (certutil -hashfile $zipName MD5 | Select-String -Pattern "^[a-f0-9]{32}$").ToString()
Write-Host "Checksum: $checksum" -ForegroundColor Yellow

# Display next steps
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Update manifest.json with checksum: $checksum"
Write-Host "2. Update README.md version history"
Write-Host "3. Run: git add -A"
Write-Host "4. Run: git commit -m 'v$Version: $Description'"
Write-Host "5. Run: git push"
Write-Host "6. Create GitHub release with $zipName"
```

**Usage**:
```powershell
.\release.ps1 -Version "1.0.57.0" -Description "Added new feature"
```

### Quick Release Script (Linux/Mac Bash)

Save as `release.sh`:

```bash
#!/bin/bash

VERSION=$1
DESCRIPTION=$2

if [ -z "$VERSION" ] || [ -z "$DESCRIPTION" ]; then
    echo "Usage: ./release.sh <version> <description>"
    exit 1
fi

# Build
echo "Building version $VERSION..."
dotnet clean
dotnet build -c Release

# Create ZIP
ZIP_NAME="jellyfin-plugin-yourplugin_$VERSION.zip"
echo "Creating $ZIP_NAME..."
cd bin/Release/net9.0/
zip ../../../$ZIP_NAME *.dll *.pdb
cd ../../../

# Generate checksum
echo "Generating checksum..."
CHECKSUM=$(md5sum $ZIP_NAME | awk '{print $1}')
echo "Checksum: $CHECKSUM"

# Display next steps
echo ""
echo "Next steps:"
echo "1. Update manifest.json with checksum: $CHECKSUM"
echo "2. Update README.md version history"
echo "3. Run: git add -A"
echo "4. Run: git commit -m 'v$VERSION: $DESCRIPTION'"
echo "5. Run: git push"
echo "6. Create GitHub release with $ZIP_NAME"
```

**Usage**:
```bash
chmod +x release.sh
./release.sh "1.0.57.0" "Added new feature"
```

---

## Troubleshooting

### Build Fails

**Error**: Package version mismatch
```
Solution: Update package versions to match Jellyfin version
- Check Jellyfin version: Settings → Dashboard → General
- Update .csproj packages to match
```

**Error**: Missing dependencies
```bash
Solution: Restore packages
dotnet restore
dotnet build -c Release
```

### ZIP Creation Fails

**Error**: File not found
```
Solution: Ensure you built in Release mode first
dotnet build -c Release
```

**Error**: Permission denied
```
Solution: Run PowerShell/terminal as administrator
```

### GitHub Release Fails

**Error**: 401 Unauthorized
```
Solution: Check GitHub token has correct permissions
- Token needs: repo (full control)
- Generate new token at: https://github.com/settings/tokens
```

**Error**: 422 Validation Failed - Tag already exists
```
Solution: Delete existing tag or use a new version
git tag -d v1.0.57.0
git push origin :refs/tags/v1.0.57.0
```

### Manifest Issues

**Error**: Checksum mismatch when users download
```
Solution: Regenerate checksum and update manifest
- Delete ZIP
- Rebuild and create new ZIP
- Generate new checksum
- Update manifest.json
- Push manifest update
```

**Error**: Plugin doesn't appear in catalog
```
Solution: Check manifest.json format
- Validate JSON syntax
- Ensure sourceUrl is accessible
- Check targetAbi matches Jellyfin version
```

---

## Best Practices

### 1. **Version Naming Convention**

- Use semantic versioning: `MAJOR.MINOR.PATCH.BUILD`
- Git tags: Always prefix with `v` (e.g., `v1.0.56.0`)
- ZIP names: Include full version (e.g., `plugin_1.0.56.0.zip`)

### 2. **Commit Messages**

- Start with version: `v1.0.56.0: Brief description`
- Include detailed changelog in commit body
- Use present tense: "Add feature" not "Added feature"
- Reference issue numbers: "Fixes #123"

### 3. **Release Notes**

- **Clear title**: Version + main feature
- **Structure**:
  - Overview
  - What changed
  - Why it matters
  - Installation instructions
- **User-focused**: Explain impact, not implementation

### 4. **Testing Before Release**

```bash
# Always test locally first
1. Build in Release mode
2. Copy DLL to local Jellyfin
3. Restart Jellyfin
4. Test all features
5. Check browser console for errors
6. Test on different pages
```

### 5. **Manifest Management**

- Keep only **latest 3-5 versions** in manifest
- Remove very old versions to reduce file size
- Always test manifest URL accessibility
- Use consistent timestamp format (ISO 8601)

### 6. **Documentation**

- Update README before release
- Keep changelog concise but informative
- Include screenshots for visual changes
- Document breaking changes prominently

---

## Quick Reference Commands

### Build and Package
```bash
dotnet clean && dotnet build -c Release
```

### Create ZIP (Windows)
```powershell
Compress-Archive -Path "bin\Release\net9.0\*.dll","bin\Release\net9.0\*.pdb" -DestinationPath "plugin_VERSION.zip" -Force
```

### Create ZIP (Linux)
```bash
cd bin/Release/net9.0 && zip ../../../plugin_VERSION.zip *.dll *.pdb && cd ../../../
```

### Checksum (Windows)
```bash
certutil -hashfile plugin_VERSION.zip MD5
```

### Checksum (Linux)
```bash
md5sum plugin_VERSION.zip
```

### Git Workflow
```bash
git add -A
git commit -m "vVERSION: Description"
git push
```

### GitHub Release (API)
```bash
# Create release
curl -X POST -H "Authorization: token TOKEN" -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/USER/REPO/releases \
  -d '{"tag_name":"vVERSION","name":"vVERSION - Title","body":"Description"}'

# Upload asset
curl -X POST -H "Authorization: token TOKEN" -H "Content-Type: application/zip" \
  --data-binary @plugin_VERSION.zip \
  "https://uploads.github.com/repos/USER/REPO/releases/RELEASE_ID/assets?name=plugin_VERSION.zip"
```

---

## Checklist

Before releasing a new version, check:

- [ ] Code changes tested locally
- [ ] Version bumped in `.csproj`
- [ ] Build succeeds without errors
- [ ] ZIP file created successfully
- [ ] MD5 checksum generated
- [ ] README.md updated
- [ ] manifest.json updated with correct:
  - [ ] Version number
  - [ ] Changelog
  - [ ] Checksum
  - [ ] Timestamp
  - [ ] Source URL (after GitHub release)
- [ ] Git commit with descriptive message
- [ ] Pushed to GitHub
- [ ] GitHub release created
- [ ] ZIP uploaded to release
- [ ] manifest.json sourceUrl updated (if needed)
- [ ] Release tested by downloading from GitHub

---

## Additional Resources

- **Jellyfin Plugin API**: https://github.com/jellyfin/jellyfin-plugin-template
- **GitHub Releases API**: https://docs.github.com/en/rest/releases
- **Semantic Versioning**: https://semver.org/
- **Markdown Guide**: https://www.markdownguide.org/

---

**This workflow was created based on real-world experience releasing the Jellyfin Ratings Plugin from v1.0.0 to v1.0.56.0 (56 releases!).**

Good luck with your plugin releases! 🚀
