# Installation

## Standard Installation

### Step 1: Add Plugin Repository

1. Open **Jellyfin Dashboard**
2. Go to **Plugins** → **Repositories**
3. Click **+** to add a new repository
4. Enter the repository URL:
   ```
   https://raw.githubusercontent.com/K3ntas/jellyfin-plugin-ratings/main/manifest.json
   ```
5. Click **Save**

### Step 2: Install the Plugin

1. Go to **Plugins** → **Catalog**
2. Find **"Ratings"** in the list
3. Click **Install**
4. **Restart Jellyfin server**

### Step 3: Verify Installation

After restart:
- Open any movie or TV show
- You should see the rating stars below the title
- The "Request Media" button appears in the header

---

## Manual Installation

If automatic installation doesn't work:

1. Download the latest release from [GitHub Releases](https://github.com/K3ntas/jellyfin-plugin-ratings/releases)
2. Extract `Jellyfin.Plugin.Ratings.dll` from the ZIP
3. Copy to your Jellyfin plugins folder:
   - **Linux**: `/var/lib/jellyfin/plugins/Ratings/`
   - **Windows**: `C:\ProgramData\Jellyfin\Server\plugins\Ratings\`
   - **Docker**: `/config/plugins/Ratings/`
4. Restart Jellyfin

---

## Docker Installation

The plugin works automatically in Docker. However, some Docker setups may have permission issues.

### If plugin doesn't load (Docker)

See [[Docker Troubleshooting]] for solutions.

**Quick fix** - Add to your docker-compose.yml:
```yaml
environment:
  - PUID=0
  - PGID=0
```

Or run this command:
```bash
docker exec jellyfin chmod 777 /jellyfin/jellyfin-web/index.html
docker restart jellyfin
```

---

## Updating the Plugin

1. Go to **Jellyfin Dashboard** → **Plugins** → **My Plugins**
2. Find **Ratings** and click **Update** (if available)
3. Restart Jellyfin

---

## Uninstalling

1. Go to **Jellyfin Dashboard** → **Plugins** → **My Plugins**
2. Find **Ratings** and click **Uninstall**
3. Restart Jellyfin

The plugin will clean up its injected code automatically.
