# Dev Branch Release Workflow

## Overview

The dev branch uses separate versioning (2.x.x.x) for testing before merging to main.

**Dev Manifest URL:**
```
https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/dev/manifest.json
```

---

## Version Numbering

| Branch | Version Format | Example |
|--------|---------------|---------|
| main   | 1.0.x.0       | 1.0.306.0, 1.0.307.0 |
| dev    | 2.0.x.0       | 2.0.1.0, 2.0.2.0 |

---

## Release Steps

### 1. Update Version
```xml
<!-- Jellyfin.Plugin.Ratings.csproj -->
<Version>2.0.X.0</Version>
```

### 2. Build
```bash
cd /path/to/jellyfinratings
rm -rf bin obj
dotnet build -c Release
```

### 3. Package
```powershell
Compress-Archive -Path 'bin/Release/net9.0/Jellyfin.Plugin.Ratings.dll','bin/Release/net9.0/Jellyfin.Plugin.Ratings.pdb' -DestinationPath 'release/jellyfin-plugin-ratings_2.0.X.0.zip' -Force
```

### 4. Get Checksum
```bash
certutil -hashfile release/jellyfin-plugin-ratings_2.0.X.0.zip MD5
```

### 5. Update manifest.json
```json
{
  "version": "2.0.X.0",
  "sourceUrl": "https://github.com/K3ntas/jellyfin-plugin-ratings/releases/download/v2.0.X.0/jellyfin-plugin-ratings_2.0.X.0.zip",
  "checksum": "<MD5_HASH>",
  "targetAbi": "10.11.0.0"
}
```

### 6. Commit & Push
```bash
git add -A
git commit -m "Dev vX.X.X.X - Description"
git push origin dev
```

### 7. Create GitHub Release
```bash
gh release create v2.0.X.0 release/jellyfin-plugin-ratings_2.0.X.0.zip \
  --target dev \
  --prerelease \
  --title "v2.0.X.0 (Dev)" \
  --notes "Dev release notes..."
```

---

## Common Issues & Fixes

### Issue 1: "NotSupported" Status in Jellyfin

**Symptoms:**
- Plugin shows "NotSupported" in Jellyfin catalog
- Plugin installs but doesn't load

**Cause:**
Plugin compiled with newer Jellyfin packages than the server version.

**Diagnosis:**
Check Jellyfin server logs for:
```
Could not load file or assembly 'MediaBrowser.Controller, Version=10.11.6.0'
```

**Fix:**
Downgrade packages in csproj to match server:
```xml
<PackageReference Include="Jellyfin.Controller" Version="10.11.0" />
<PackageReference Include="Jellyfin.Model" Version="10.11.0" />
```

**Prevention:**
Dependabot is configured to ignore Jellyfin packages (see `.github/dependabot.yml`).

---

### Issue 2: Dependabot Updates Breaking Compatibility

**Problem:**
Dependabot auto-updates Jellyfin packages to latest version, which may be newer than users' servers.

**Current Protection (.github/dependabot.yml):**
```yaml
ignore:
  - dependency-name: "Jellyfin.Controller"
  - dependency-name: "Jellyfin.Model"
```

**Best Practice:**
- Keep Jellyfin packages at minimum required version (10.11.0)
- Only update manually when dropping support for older servers
- Test on oldest supported server version

---

### Issue 3: targetAbi Mismatch

**What is targetAbi?**
Minimum Jellyfin server version required by the plugin.

**Correct Setting:**
```json
"targetAbi": "10.11.0.0"
```

**Rule:**
- Set to the MINIMUM version you support
- NOT the version you compiled with
- Allows users with 10.11.0, 10.11.1, 10.11.2, etc. to use the plugin

---

### Issue 4: Same GUID Conflict

**Problem:**
Dev and main plugins share the same GUID. Users cannot have both installed.

**Current Design:**
- Same GUID intentionally (same plugin, different versions)
- Users must uninstall one before installing the other

**If Separate Plugins Needed:**
Change GUID in dev branch:
- `manifest.json` - guid field
- `Plugin.cs` - Id property
- `Web/ratings.js` - pluginId
- `Configuration/configPage.html` - pluginId

---

### Issue 5: Plugin Won't Load After Install

**Symptoms:**
- Plugin installed successfully
- Shows in "My Plugins" but doesn't work
- No errors in UI

**Diagnosis:**
1. Check server logs for assembly load errors
2. Verify checksum matches
3. Check .NET version compatibility

**Common Fixes:**
1. Delete plugin folder manually and reinstall
2. Restart Jellyfin completely
3. Verify packages match server version

---

## Package Version Compatibility Matrix

| Jellyfin Server | Recommended Package Version |
|----------------|----------------------------|
| 10.11.0        | 10.11.0                    |
| 10.11.1        | 10.11.0                    |
| 10.11.2+       | 10.11.0                    |
| 10.12.x        | 10.12.0 (when available)   |

**Rule:** Always use the OLDEST package version that works.

---

## Merging Dev to Main

When dev is tested and ready:

1. **Update main branch packages** (if needed)
2. **Merge dev to main:**
   ```bash
   git checkout main
   git merge dev
   ```
3. **Update version to main numbering:**
   ```xml
   <Version>1.0.307.0</Version>
   ```
4. **Update main manifest.json** with new version
5. **Create production release** (not prerelease)

---

## Cleanup Commands

**Delete old dev releases:**
```bash
gh release delete v2.0.X.0 --yes
```

**Delete plugin from server (if broken):**
```bash
rm -rf /config/plugins/Ratings\ \(Dev\)_2.0.X.0/
```

**Force Jellyfin to re-download manifest:**
- Dashboard → Plugins → Repositories → Remove → Re-add
