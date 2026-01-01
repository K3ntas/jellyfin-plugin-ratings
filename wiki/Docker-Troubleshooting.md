# Docker Troubleshooting

## Plugin Not Loading in Docker?

The Ratings plugin needs to inject a script into Jellyfin's web interface. In some Docker setups, this can fail due to file permission issues.

**Version 1.0.174.0+** includes an HTTP middleware that works without file write permissions. Update to the latest version first!

---

## How the Plugin Works

The plugin uses two injection methods:

1. **HTTP Middleware** (Primary) - Injects script dynamically into HTTP responses. Works without file permissions.
2. **File Injection** (Fallback) - Modifies `index.html` directly. Requires write permissions.

If both methods fail, the plugin won't load.

---

## Troubleshooting Steps

### Step 1: Update to Latest Version

Make sure you have **v1.0.174.0 or higher**:
1. Go to Jellyfin Dashboard → Plugins → My Plugins
2. Check the Ratings plugin version
3. Update if available, then restart Jellyfin

### Step 2: Check if Plugin is Installed

```bash
docker exec jellyfin ls -la /config/plugins/ | grep -i rating
```

You should see a folder like `Ratings_1.0.174.0`.

### Step 3: Check if Script is Injected

```bash
curl -s http://localhost:8096/web/index.html | grep -i "ratings.js"
```

If you see `<script defer src="/Ratings/ratings.js"></script>`, the plugin is working!

---

## Fixes for Permission Issues

If the plugin still doesn't work after updating, try these solutions:

### Option 1: Run as Root (Easiest)

Add to your `docker-compose.yml`:
```yaml
services:
  jellyfin:
    environment:
      - PUID=0
      - PGID=0
```

Then restart:
```bash
docker-compose down
docker-compose up -d
```

### Option 2: Fix File Permissions

```bash
docker exec jellyfin chmod 777 /jellyfin/jellyfin-web/index.html
docker restart jellyfin
```

### Option 3: Fix Folder Permissions

```bash
docker exec jellyfin chmod -R 777 /jellyfin/jellyfin-web
docker restart jellyfin
```

### Option 4: Use Entrypoint Script

Add to your `docker-compose.yml`:
```yaml
services:
  jellyfin:
    entrypoint: /bin/sh -c "chmod 777 /jellyfin/jellyfin-web/index.html 2>/dev/null; exec /jellyfin/jellyfin"
```

### Option 5: Manual Script Injection

If all else fails, manually inject the script:
```bash
docker exec jellyfin sed -i 's|</body>|<script defer src="/Ratings/ratings.js"></script></body>|' /jellyfin/jellyfin-web/index.html
docker restart jellyfin
```

**Note:** This needs to be repeated after Jellyfin updates.

---

## Verify the Fix

After applying any fix:

1. Restart Jellyfin:
   ```bash
   docker restart jellyfin
   ```

2. Check if script is loaded:
   ```bash
   curl -s http://localhost:8096/web/index.html | grep -i "ratings.js"
   ```

3. Open Jellyfin web interface and verify:
   - Rating stars appear on media detail pages
   - "Request Media" button appears in header

---

## Common Docker Setups

### Linuxserver.io Image

The linuxserver.io Jellyfin image uses PUID/PGID. Try:
```yaml
environment:
  - PUID=0
  - PGID=0
```

Or use the official Jellyfin image which runs as root by default.

### Synology NAS

Synology Docker typically runs as root, so the plugin should work automatically. If not:
```bash
docker exec jellyfin chmod 777 /jellyfin/jellyfin-web/index.html
docker restart jellyfin
```

### Unraid

For Unraid, add this to "Extra Parameters":
```
--user 0:0
```

---

## Still Having Issues?

1. Check Jellyfin logs for errors:
   ```bash
   docker logs jellyfin | grep -i rating
   ```

2. Open an issue with your:
   - Docker setup (compose file or run command)
   - Jellyfin version
   - Plugin version
   - Any error messages

https://github.com/K3ntas/jellyfin-plugin-ratings/issues
