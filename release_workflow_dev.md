# Dev Branch Release Workflow

Quick reference for releasing on the **dev** branch for testing.

---

## Important: Known Issues

### Old Plugin Folders Not Being Deleted (2026-02-20)

**Problem**: After updating the plugin, Jellyfin loads BOTH old and new versions, causing:
- `AmbiguousMatchException` (routes registered twice)
- `InvalidCastException` (configuration type conflicts)
- Plugin constantly asking for restart

**Root Cause**: The release ZIP was missing the `.pdb` file. Jellyfin uses this for proper plugin identification and upgrade handling.

**Solution**: ALWAYS include both `.dll` AND `.pdb` files in the release ZIP:

```powershell
# CORRECT - includes both files
Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.dll', 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.pdb' -DestinationPath 'jellyfin-plugin-ratings_VERSION.zip' -Force

# WRONG - missing PDB causes upgrade issues
Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.dll' -DestinationPath 'jellyfin-plugin-ratings_VERSION.zip' -Force
```

**Manual Fix** (if old folders remain):
```bash
# List plugin folders
docker exec -it jf-test ls /config/plugins/ | grep Ratings

# Delete old version(s) - keep only the newest
docker exec -it jf-test rm -rf "/config/plugins/Ratings (Dev)_OLD_VERSION"

# Restart
docker restart jf-test
```

---

## Quick Release Checklist

1. **Update version** in `Jellyfin.Plugin.Ratings.csproj`
2. **Clean build**:
   ```bash
   dotnet clean && dotnet build -c Release
   ```
3. **Create ZIP with BOTH dll and pdb**:
   ```powershell
   Compress-Archive -Path 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.dll', 'bin\Release\net9.0\Jellyfin.Plugin.Ratings.pdb' -DestinationPath 'jellyfin-plugin-ratings_VERSION.zip' -Force
   ```
4. **Get checksum**:
   ```powershell
   (Get-FileHash jellyfin-plugin-ratings_VERSION.zip -Algorithm MD5).Hash.ToLower()
   ```
5. **Update manifest.json** with new version entry
6. **Commit and push**:
   ```bash
   git add -A && git commit -m "vVERSION: Description" && git push origin dev
   ```
7. **Create GitHub release** (prerelease for dev):
   ```bash
   gh release create vVERSION jellyfin-plugin-ratings_VERSION.zip --title "vVERSION - Title" --prerelease --notes "Release notes"
   ```

---

## Dev vs Main Branch

| Aspect | Main (Production) | Dev (Testing) |
|--------|-------------------|---------------|
| Version prefix | `1.0.x.x` | `2.0.x.x` |
| Plugin name | `Ratings` | `Ratings (Dev)` |
| Prerelease | No | Yes (`--prerelease`) |
| Target users | All users | Testers only |

---

## Test Server Commands

```bash
# Check which container
docker ps | grep -i jellyfin
# jf-test = test server
# jellyfin = production server

# Check loaded plugins
docker logs jf-test 2>&1 | grep -i "loaded plugin"

# List plugin folders
docker exec -it jf-test ls /config/plugins/

# Force update (delete and restart)
docker exec -it jf-test rm -rf "/config/plugins/Ratings (Dev)_*"
docker restart jf-test
```

---

## Troubleshooting

### Multiple plugin versions loaded
```
[INF] Loaded plugin: "Ratings (Dev)" "2.0.14.0"
[INF] Loaded plugin: "Ratings (Dev)" "2.0.15.0"
```
**Fix**: Delete older version folder, restart Jellyfin.

### 500 errors after update
Check logs for specific error:
```bash
docker logs jf-test --tail 100 2>&1 | grep -i error
```

### Plugin not updating
1. Check if manifest.json was pushed
2. Clear Jellyfin plugin cache
3. Manually delete plugin folder and reinstall

---

**Last updated**: 2026-02-20
