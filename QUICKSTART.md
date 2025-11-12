# Quick Start Guide

## 5-Minute Setup

### Step 1: Build (30 seconds)

**Windows:**
```powershell
.\build.ps1
```

**Linux/Mac:**
```bash
chmod +x build.sh && ./build.sh
```

### Step 2: Install (1 minute)

**Windows:**
```powershell
# Copy to Jellyfin
Copy-Item "bin\Release\net8.0\Jellyfin.Plugin.Ratings.dll" `
  "$env:APPDATA\Jellyfin\Server\plugins\Ratings_1.0.0.0\" -Force

# Restart Jellyfin service
Restart-Service JellyfinServer
```

**Linux:**
```bash
# Copy to Jellyfin
sudo mkdir -p /var/lib/jellyfin/plugins/Ratings_1.0.0.0
sudo cp bin/Release/net8.0/Jellyfin.Plugin.Ratings.dll \
  /var/lib/jellyfin/plugins/Ratings_1.0.0.0/
sudo chown -R jellyfin:jellyfin /var/lib/jellyfin/plugins/Ratings_1.0.0.0

# Restart Jellyfin
sudo systemctl restart jellyfin
```

### Step 3: Verify (30 seconds)

1. Open Jellyfin web interface
2. Go to **Dashboard** → **Plugins**
3. Verify "Ratings" plugin is listed
4. Click on it to configure if needed

### Step 4: Use (1 minute)

1. Navigate to any movie or series
2. Look for "Rate This" section
3. Click a star (1-10) to rate
4. Hover over stars to see all user ratings

## That's it! 🎉

Your Jellyfin instance now has a fully functional rating system!

---

## Troubleshooting

**Plugin not showing?**
- Check you restarted Jellyfin completely
- Verify DLL is in the correct folder
- Check Dashboard → Advanced → Logs for errors

**Can't rate items?**
- Make sure you're logged in
- Check Dashboard → Plugins → Ratings → "Enable Ratings" is checked

**Need help?**
- Read [README.md](README.md) for full documentation
- See [INSTALLATION.md](INSTALLATION.md) for detailed setup
- Check [ARCHITECTURE.md](ARCHITECTURE.md) for technical details

---

## What You Get

✅ 1-10 star rating system
✅ Hover popup showing usernames and their ratings
✅ Average ratings display
✅ Personal rating tracking
✅ Works on all media types
✅ Configuration page
✅ REST API

## File Locations

- **Plugin**: `{jellyfin}/plugins/Ratings_1.0.0.0/`
- **Data**: `{jellyfin}/data/ratings/ratings.json`
- **Config**: `{jellyfin}/config/plugins/Jellyfin.Plugin.Ratings.xml`

## Quick Commands

**Check if running:**
```bash
curl http://localhost:8096/Ratings/Items/{some-item-id}/Stats
```

**View logs:**
- Dashboard → Advanced → Logs
- Look for "[Ratings Plugin]" entries

**Backup ratings:**
```bash
# Windows
copy "%AppData%\Jellyfin\Server\data\ratings\ratings.json" backup.json

# Linux
cp /var/lib/jellyfin/data/ratings/ratings.json backup.json
```

---

**Questions?** Check the [README.md](README.md) for more details!
