# Development Insights & Lessons Learned

## Release Workflow

### Branch Strategy
- **Main branch**: Production releases, version scheme `1.0.X.0`
- **Dev branch**: Testing releases, version scheme `2.0.X.0`
- Always commit/push to **dev** first for testing, then cherry-pick to main for production
- Dev releases use `--prerelease` flag, main releases do NOT

### Cherry-picking Between Branches
```bash
git checkout main
git cherry-pick <commit-hash> --no-commit
# Resolve conflicts - keep version files from target branch:
git checkout --ours Jellyfin.Plugin.Ratings.csproj manifest.json
git checkout --theirs Web/ratings.js
git add .
```

## Git History Rewriting

When removing files/folders from entire git history:
1. **Stash any uncommitted changes first** - filter-branch fails with dirty working directory
2. Use `FILTER_BRANCH_SQUELCH_WARNING=1` to suppress warning
3. After filter-branch, clean up refs and force push ALL branches and tags:
```bash
git filter-branch --force --index-filter 'git rm -rf --cached --ignore-unmatch <folder>/' --prune-empty --tag-name-filter cat -- --all
rm -rf .git/refs/original/
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force origin main dev --tags
```
4. **Impact on clones**: Others who cloned must re-clone or `git fetch && git reset --hard origin/<branch>`

## Jellyfin UI Structure

### Finding Media Cards
- Page can have **multiple `.itemsContainer` elements** - don't just use first one
- `CollectionFolder` type = library folders (Movies, Shows) - NOT actual media
- Media types to look for: `Movie`, `Series`, `Episode`, `Season`, `MusicAlbum`, `Audio`, `MusicVideo`, `Video`, `BoxSet`
- Find container with **MOST media cards**, not just first container with any

### Container Selectors (in order of specificity)
```javascript
const containerSelectors = [
    '.itemsContainer.vertical-wrap',
    '.itemsContainer.padded-left.padded-right',
    '.itemsContainer:not(.scrollSlider)',
    '.itemsContainer'
];
```

### Toolbar Button Styling
- Use `paper-icon-button-light` class to match Jellyfin's native buttons
- Button container size and SVG icon size can be different (e.g., 42px button, 28px icon)
- Simple, clean icons work better than complex ones
- Match the opacity/color of existing toolbar icons (`rgba(255, 255, 255, 0.5)`)

## Code Quality

### Console Logging
- Remove all debug `console.log` statements before production release
- Keep only essential error logging
- Debug logs clutter the console and can expose implementation details

### DOM Manipulation
- When reordering DOM elements, disable CSS transitions temporarily for instant visual update
- Force reflow after DOM changes: `void element.offsetHeight`
- Restore transitions after changes are applied

## Common Mistakes to Avoid

1. **Wrong branch**: Always confirm which branch to commit/push to
2. **First container**: Don't assume first `.itemsContainer` is the right one
3. **Debug logs**: Clean them up before release
4. **Version conflicts**: When cherry-picking, keep version files from target branch
5. **Force push scope**: When rewriting history, push ALL affected branches and tags
6. **Icon sizing**: Button container and icon can have different sizes - clarify with user

## Plugin Configuration

### Settings that control UI elements
- `ShowNotificationToggle` - controls bell icon visibility (NOT `EnableNewMediaNotifications`)
- `NotificationsEnabledByDefault` - controls default notification state for users
- Always check the correct config property is being used

## Testing Checklist

Before releasing:
1. Test on actual library pages (inside Movies/Series), not just dashboard
2. Verify buttons appear in correct location
3. Test sorting works with multiple media items
4. Check console for any remaining debug logs
5. Verify icon styling matches existing Jellyfin buttons

## Plugin Integration Limitations

### Jellyfin Reports Plugin
- Has **no extension API or hooks** - closed architecture
- All columns defined in hardcoded `HeaderMetadata` enum
- Column data extraction uses fixed switch statement
- **Cannot integrate** without forking and maintaining custom version
- Alternative: Add export feature directly to your plugin instead

### Cross-Plugin Communication
- Jellyfin plugins are isolated - no direct data sharing
- Options for integration:
  1. Fork target plugin (maintenance burden)
  2. Request extensibility from plugin maintainers
  3. Store data in Jellyfin's native systems (`UserItemData`)
  4. Add export/report feature to your own plugin

## Feature Implementation Patterns

### User Request/Voting System (Keep Request Feature)
- Store requests in separate JSON file (e.g., `keep_requests.json`)
- Track: `ItemId`, `UserId`, `Username`, `RequestedAt`
- Rate limiting: Check if user already requested today
- Clean up requests when parent action is cancelled/completed
- Include counts and user status in API response for frontend

### API Response Design for Frontend
Include all data frontend needs in single response:
```csharp
new {
    // Entity data
    d.Id, d.ItemId, d.ItemTitle, ...
    // Aggregated counts
    KeepRequestCount = counts.TryGetValue(d.ItemId, out var count) ? count : 0,
    // Config values
    AutoCancelThreshold = config?.AutoCancelDeletionThreshold ?? 0,
    // User-specific state
    UserHasRequested = repo.HasUserRequestedKeep(d.ItemId, userId),
    UserCanRequestToday = !repo.HasUserRequestedKeepToday(d.ItemId, userId),
    // Role info
    IsAdmin = user?.HasPermission(PermissionKind.IsAdministrator) ?? false
}
```

### Auto-Action on Threshold
When implementing "do X when Y reaches threshold":
1. Check threshold in config (0 = disabled)
2. After action, get new count
3. If count >= threshold, trigger auto-action
4. Return `AutoCancelled: true` in response so frontend can react

## Frontend Patterns

### Tooltips for User Education
Add descriptive tooltips to explain features:
- Button tooltips: What clicking does
- Counter tooltips: What the numbers mean
- Dynamic values in tooltips: `{current} of {threshold} requests needed`

### Button State Management
```javascript
// Before action
btn.disabled = true;
btn.textContent = '...';

// On success
btn.classList.add('asked');
btn.textContent = self.t('alreadyAsked');

// On error - restore
btn.textContent = self.t('askToKeep');
btn.disabled = false;
```

### Updating Related Elements
When action affects multiple UI elements (button + counter):
```javascript
const wrapper = btn.closest('.container-class');
if (wrapper) {
    let counter = wrapper.querySelector('.counter-class');
    if (counter) {
        counter.textContent = `${newCount}/${threshold}`;
    }
}
```

## Cherry-picking Alternative

When `git cherry-pick --no-commit` fails with conflicts:
```bash
# Instead of resolving complex conflicts, checkout specific files:
git checkout dev -- Api/File.cs Web/file.js Models/NewModel.cs
# Then manually update version files for target branch
```

## Translation Management

### Adding New Feature Strings
When adding translatable strings:
1. Add to ALL language sections (16 languages currently)
2. Keep key names consistent across languages
3. Use placeholders for dynamic values: `{current}`, `{threshold}`, `{count}`
4. Replace placeholders in JS: `str.replace('{current}', value)`

### Tooltip Translations
Tooltips need more context than button labels:
- `askToKeep: 'Ask to keep'` (short label)
- `askToKeepTooltip: 'Click to request that this media is not deleted...'` (full explanation)

## JSON Serialization Case Sensitivity

### Problem
C# API returns PascalCase properties (`FromUsername`, `Id`), but JavaScript expects camelCase.

### Solutions
1. **Backend fix** - Add `PropertyNameCaseInsensitive = true` to deserializer:
```csharp
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var data = JsonSerializer.Deserialize<List<MyModel>>(json, options);
```

2. **Frontend fix** - Handle both cases:
```javascript
var username = item.FromUsername || item.fromUsername || '?';
var id = item.Id || item.id;
```

3. **Best practice**: Use `PropertyNameCaseInsensitive` on backend to avoid fragile frontend code.

## WebSocket Implementation in Jellyfin Plugins

### WRONG Approach (doesn't work)
```
Custom WebSocket controller endpoint (/Social/WebSocket) → FAILS
ASP.NET Core WebSocket middleware not enabled for plugins
Connection fails with code 1006
```

### CORRECT Approach: Use IWebSocketListener
Jellyfin has a built-in WebSocket system at `/socket`. Plugins must use `IWebSocketListener` interface.

### Architecture
```
IWebSocketListener (interface) → implement this
WebSocketManager (Jellyfin) → discovers and calls all listeners
Client connects to /socket → Jellyfin routes messages to listeners
```

### Implementation Steps
1. **Create listener class**:
```csharp
public class SocialWebSocketListener : IWebSocketListener
{
    public Task ProcessWebSocketConnectedAsync(IWebSocketConnection connection, HttpContext context)
    {
        var userId = connection.AuthorizationInfo?.UserId ?? Guid.Empty;
        // Store connection for later broadcasts
    }

    public Task ProcessMessageAsync(WebSocketMessageInfo message)
    {
        // Handle incoming messages
    }
}
```

2. **Register in PluginServiceRegistrator**:
```csharp
serviceCollection.AddSingleton<SocialWebSocketListener>();
serviceCollection.AddSingleton<IWebSocketListener>(sp => sp.GetRequiredService<SocialWebSocketListener>());
```

3. **Send messages using OutboundWebSocketMessage**:
```csharp
using MediaBrowser.Controller.Net.WebSocketMessages;

await connection.SendAsync(
    new OutboundWebSocketMessage<MyPayload>
    {
        MessageType = SessionMessageType.KeepAlive, // Use as carrier
        Data = payload
    },
    CancellationToken.None);
```

4. **Client connects to Jellyfin's socket**:
```javascript
var wsUrl = baseUrl.replace('http://', 'ws://') + '/socket?api_key=' + token;
var ws = new WebSocket(wsUrl);
ws.onmessage = function(e) {
    var msg = JSON.parse(e.data);
    if (msg.Data && msg.Data.SocialType) {
        // Handle our custom message
    }
};
```

### Key Learnings
- **IWebSocketConnection.AuthorizationInfo.UserId** - NOT AuthenticatedUser
- **WebSocketState** is in `System.Net.WebSockets` namespace
- **OutboundWebSocketMessage** is in `MediaBrowser.Controller.Net.WebSocketMessages`
- Use `SessionMessageType.KeepAlive` as carrier, put custom type in Data
- Client connects to `/socket?api_key=TOKEN` (Jellyfin's endpoint)

### Security (still applies)
1. Rate limiting broadcasts (2s minimum)
2. Privacy settings check before broadcasting
3. ConcurrentDictionary for thread-safe connection tracking
4. Cleanup dead connections periodically

## Release Workflow Pitfalls

### Path Issues in Git Bash on Windows
- `~/Desktop` resolves differently in Git Bash vs Windows
- **Always use full Windows paths** when calculating checksums:
```powershell
# PowerShell - correct
certutil -hashfile 'C:\Users\karol\Desktop\file.zip' MD5

# Git Bash - may fail
certutil -hashfile ~/Desktop/file.zip MD5  # WRONG
```

### Checksum Verification
After creating release, verify checksum matches manifest before pushing:
```bash
# Calculate actual checksum
certutil -hashfile 'full\path\to\file.zip' MD5

# Compare with manifest.json entry
```

## Polling vs WebSocket

### When to Use Each
| Feature | Polling | WebSocket |
|---------|---------|-----------|
| Server load | Higher (repeated requests) | Lower (persistent connection) |
| Latency | Depends on interval | Near-instant |
| Complexity | Simple | More setup |
| Battery/Mobile | Worse | Better |

### Hybrid Approach
- Use WebSocket when panel is open
- Fall back to polling on WebSocket failure
- Close WebSocket when panel closes to save resources

## Separating Concerns: Status vs Watching (v2.0.132.0)

### The Problem
When implementing "online status" and "currently watching" features together, they kept interfering with each other:
- Playing media would set user to "Offline"
- Closing browser while watching would clear movie but keep status "Online"
- Multiple race conditions between heartbeat and playback events

### Root Cause
Mixing two independent concepts in the same code paths:
1. **Online/Offline status** - should ONLY depend on heartbeat timing
2. **Watching info** - should ONLY depend on playback events

### The Solution: Complete Separation

**Backend - Separate repository methods:**
```csharp
// Status system - NEVER touches watching
UpdateHeartbeatOnlyAsync(userId)  // Updates LastHeartbeat, calls GetEffectiveStatus()

// Watching system - NEVER touches status
SetWatchingOnlyAsync(userId, watching)   // ONLY updates Watching field
ClearWatchingOnlyAsync(userId)           // ONLY clears Watching field
```

**Backend - Separate WebSocket broadcasts:**
```csharp
// For status changes
BroadcastStatusUpdateAsync() → sends "SocialStatusUpdate"

// For watching changes
BroadcastWatchingUpdateAsync() → sends "SocialWatchingUpdate"
```

**Frontend - Separate message handlers:**
```javascript
case 'SocialStatusUpdate':
    self.updateFriendStatusOnly(data);  // Only updates status dot
    break;

case 'SocialWatchingUpdate':
    self.updateFriendWatchingOnly(data);  // Only updates movie title
    break;
```

**Frontend - Separate update functions:**
```javascript
updateFriendStatusOnly()   // Updates status dot and status text ONLY
updateFriendWatchingOnly() // Updates watching info ONLY
```

### Key Principle
**If user is online and presses play, they CANNOT be offline** - they're actively watching media. The watching system should never affect the online status.

### Remaining Bug (as of v2.0.132.0)
When pressing play, user status briefly changes to "Offline" before correcting. This suggests:
- Something is still calculating status incorrectly during playback start
- The status field might have stale "Offline" value from before user was online
- Need to ensure status is ONLY updated by heartbeat, never read from stale cache

### Architecture Summary
```
┌─────────────────────────────────────────────────────────────┐
│                    STATUS SYSTEM                             │
│  Heartbeat → UpdateHeartbeatOnlyAsync → GetEffectiveStatus  │
│           → BroadcastStatusUpdateAsync → SocialStatusUpdate │
│           → updateFriendStatusOnly                           │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                   WATCHING SYSTEM                            │
│  Playback Start → SetWatchingOnlyAsync                      │
│                 → BroadcastWatchingUpdateAsync              │
│                 → SocialWatchingUpdate                      │
│                 → updateFriendWatchingOnly                  │
│                                                              │
│  Playback Stop → ClearWatchingOnlyAsync                     │
│                → BroadcastWatchingUpdateAsync(null)         │
│                → updateFriendWatchingOnly (removes)         │
└─────────────────────────────────────────────────────────────┘

These two systems should NEVER interact or share code paths.
```

### Lessons Learned
1. **Listen to the user** - when they say "make it completely separate", do exactly that
2. **Don't over-engineer** - simple separation beats complex state management
3. **One function, one job** - status methods touch status, watching methods touch watching
4. **Different message types** - don't mix concerns in WebSocket messages
5. **Test each system independently** - verify status works without playback, playback works without status

## Browser Offline Detection in Jellyfin SPA (v2.0.133.0 - v2.0.144.0)

### The Problem
Detecting when user closes browser/tab to mark them offline, WITHOUT triggering offline when they press play button.

### Why Browser Events Failed (v2.0.133.0 - v2.0.140.0)

This took **many versions** and failed attempts because:

1. **Jellyfin is an SPA** - internal navigation doesn't reload the page
2. **`beforeunload`/`pagehide` fire on INTERNAL navigation** - when user clicks play, these events fire
3. **`visibilitychange`** - doesn't reliably fire on browser close
4. **Any client-side approach is unreliable** - browser may close before code executes

### Failed Approaches

| Attempt | Problem |
|---------|---------|
| `beforeunload` + `pagehide` | Fires during internal SPA navigation (play button) |
| `visibilitychange` with delay | Browser closes before timeout fires |
| `visibilitychange` only | Doesn't reliably detect browser close |
| `beforeunload` + clear ForceOffline on playback | Hacky, causes "offline flash" notifications |
| Polling fallback | Not scalable with many users |

### The Correct Solution: WebSocket Disconnection Detection (v2.0.141.0+)

**Key insight**: Don't use browser events at all. Use WebSocket connection state on the SERVER.

When browser closes:
- WebSocket connection closes
- Server detects closed connections
- Server marks user offline
- Server broadcasts to friends

**No client-side code needed for offline detection!**

### Implementation

**Server - Track connections in IWebSocketListener:**
```csharp
private static readonly ConcurrentDictionary<Guid, ConcurrentBag<IWebSocketConnection>> _userConnections = new();

public async Task ProcessWebSocketConnectedAsync(IWebSocketConnection connection, HttpContext httpContext)
{
    var userId = connection.AuthorizationInfo?.UserId ?? Guid.Empty;
    if (userId == Guid.Empty) return;

    // Track connection
    var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentBag<IWebSocketConnection>());
    connections.Add(connection);

    // Mark user online
    await _socialRepository.UpdateHeartbeatOnlyAsync(userId);
}
```

**Server - Periodic disconnection check (every 5 seconds):**
```csharp
private readonly Timer _disconnectionCheckTimer = new Timer(
    CheckForDisconnectedUsers, null,
    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

private async void CheckForDisconnectedUsers(object? state)
{
    foreach (var kvp in _userConnections)
    {
        var userId = kvp.Key;
        var connections = kvp.Value;

        // Check if all connections are closed
        var activeConnections = connections.Where(c => c.State == WebSocketState.Open).ToList();

        if (activeConnections.Count == 0)
        {
            // User disconnected - mark offline
            _userConnections.TryRemove(userId, out _);
            var offlineStatus = _socialRepository.SetUserOffline(userId);

            // Broadcast to friends
            await BroadcastStatusUpdateAsync(userId, username, offlineStatus, null, skipRateLimit: true);
        }
    }
}
```

**Client - Only handle logout explicitly:**
```javascript
registerOfflineHandler: function () {
    // Handle Jellyfin logout only - server detects browser close via WebSocket
    if (window.Events) {
        Events.on(ApiClient, 'logout', function () {
            fetch(baseUrl + '/Social/Offline', {
                method: 'POST',
                headers: { 'X-Emby-Token': token }
            });
        });
    }
    // NO beforeunload, NO pagehide, NO visibilitychange
}
```

### How It Works

| Scenario | What Happens |
|----------|--------------|
| **User opens Jellyfin** | WebSocket connects → server tracks in `_userConnections` → marked online |
| **User navigates inside Jellyfin** | WebSocket stays open → stays online ✓ |
| **User presses play** | WebSocket stays open → stays online ✓ |
| **User closes browser** | WebSocket closes → server detects within 5s → marked offline ✓ |
| **User logs out** | Explicit `/Social/Offline` call → marked offline ✓ |

### Debug Endpoint (v2.0.144.0+)

To verify WebSocket connections are being tracked:
```javascript
fetch('/Social/Debug', {headers: {'X-Emby-Token': ApiClient.accessToken()}})
    .then(r => r.json()).then(console.log)
```

Returns:
```json
{
    "WebSocket": {
        "ConnectedUsers": 2,
        "TotalConnections": 4,
        "ConnectedUserNames": ["user1", "user2"]
    },
    "IsCurrentUserConnected": true
}
```

### Key Takeaways

1. **Don't fight browser events** - they're unreliable in SPAs
2. **WebSocket state is the source of truth** - if connected, user is online
3. **Server-side detection is reliable** - doesn't depend on browser cooperation
4. **Periodic check (5s)** - balances responsiveness with performance
5. **No polling fallback needed** - WebSocket handles everything

### Why This Works Better

| Aspect | Browser Events | WebSocket Detection |
|--------|----------------|---------------------|
| SPA navigation | Triggers false offline | No effect |
| Browser close | May not fire | Always detected |
| Tab switch | Complex handling | No change |
| Network issues | Can't detect | Connection closes |
| Scalability | Client polling = bad | Server push = good |

## Plugin Initialization - Wait for Authentication (v1.0.325.0)

### The Problem
Plugin initializes immediately on page load, causing UI to flash briefly on the login page before user is authenticated.

### The Solution
Wait for user authentication before initializing:

```javascript
// Check if user is authenticated
function isUserAuthenticated() {
    try {
        if (typeof ApiClient !== 'undefined' && ApiClient.getCurrentUserId && ApiClient.getCurrentUserId()) {
            return true;
        }
        if (window.ApiClient && window.ApiClient.getCurrentUserId && window.ApiClient.getCurrentUserId()) {
            return true;
        }
    } catch (e) {
        return false;
    }
    return false;
}

// Check if on login page
function isLoginPage() {
    var hash = window.location.hash || '';
    return hash.includes('login') || hash.includes('Login') || hash === '' || hash === '#' || hash === '#!/login.html';
}

// Poll for authentication
function waitForAuth() {
    if (isUserAuthenticated() && !isLoginPage()) {
        initPluginWhenReady();
    } else {
        setTimeout(waitForAuth, 500);
    }
}
```

### Prevention of Multiple Initialization
Add flag at start of init():
```javascript
init: function () {
    if (this._initialized) return;
    this._initialized = true;
    // ... rest of init
}
```

## SPA Navigation Issues with Injected Elements (v1.0.322.0 - v1.0.324.0)

### The Problem
Injected toolbar buttons (sort buttons) fail to load reliably during SPA navigation:
- First load works
- Home → Movies fails
- Refresh works again

### Root Causes
1. **Multiple elements with same class** - `document.querySelector('.btnSort')` returns first (hidden) one
2. **Orphaned DOM elements** - After SPA navigation, old elements exist in detached DOM
3. **Flex container collapsing** - Wrapper elements collapse to 0x0 dimensions
4. **Race conditions** - Multiple injection attempts happen simultaneously

### Solutions

**1. Find VISIBLE elements using getBoundingClientRect():**
```javascript
const allBtnSort = document.querySelectorAll('.btnSort');
let visibleBtnSort = null;
for (const btn of allBtnSort) {
    const rect = btn.getBoundingClientRect();
    if (rect.width > 0 && rect.height > 0) {
        visibleBtnSort = btn;
        break;
    }
}
```

**2. Check for container in CURRENT toolbar, not just any container:**
```javascript
const toolbar = visibleBtnSort ? visibleBtnSort.parentElement : null;
const containerInToolbar = toolbar ? toolbar.querySelector('#librarySortContainer') : null;
```

**3. Remove ALL existing elements globally before injecting:**
```javascript
document.querySelectorAll('#librarySortContainer, #librarySortDesc, #librarySortAsc').forEach(el => el.remove());
```

**4. Inject buttons directly, not wrapped in container:**
```javascript
// BAD - wrapper can collapse
const wrapper = document.createElement('span');
wrapper.innerHTML = '<button>...</button>';
container.appendChild(wrapper);

// GOOD - inject directly
const btn = document.createElement('button');
btn.style.cssText = 'flex-shrink: 0;'; // Prevent flex collapse
container.insertBefore(btn, insertBefore);
```

**5. Use MutationObserver for reliable injection:**
```javascript
const observer = new MutationObserver(() => {
    // Find visible .btnSort
    // If no container in toolbar, inject
});
observer.observe(document.body, { childList: true, subtree: true });
```

## CSS Flexbox Fixes

### Long Text Pushing Elements Off Screen
When text in a flex item is too long, it can push sibling elements off screen.

**Fix 1: min-width: 0 on flex items**
```css
.flex-child {
    flex: 1;
    min-width: 0;  /* Allow item to shrink below content size */
}
```

**Fix 2: Multi-line text with line clamp**
```css
.title {
    display: -webkit-box;
    -webkit-line-clamp: 2;  /* Max 2 lines */
    -webkit-box-orient: vertical;
    overflow: hidden;
    word-break: break-word;
}
```

### Flex Items Collapsing to 0x0
When injecting elements into flex containers, they may collapse.

**Fix: flex-shrink: 0**
```css
.injected-button {
    flex-shrink: 0;
}
```

## Synology NAS - Finding Jellyfin Docker Plugins

### Finding Docker Container Config Path
```bash
# List Jellyfin containers
docker ps | grep -i jellyfin

# Get volume mounts
docker inspect <container-name> --format '{{range .Mounts}}{{.Source}} -> {{.Destination}}{{println}}{{end}}'
```

### Typical Paths
```bash
# Config path (from docker inspect)
/volume4/jf-test-config -> /config

# Plugins location
/volume4/jf-test-config/plugins/

# Find plugin folders
ls -la /volume4/jf-test-config/plugins/Ratings*/
```

### Removing Duplicate Plugin Versions
```bash
# List duplicates
ls -la /volume4/jf-test-config/plugins/

# Remove older version
rm -rf "/volume4/jf-test-config/plugins/Ratings (Dev)_2.0.182.0"

# Restart container
docker restart jf-test
```

## GitHub Release Workflow - Critical Order

### Why Order Matters
Wrong order causes duplicate plugin installations in Jellyfin.

### CORRECT Order
1. Update version in csproj
2. Update manifest with PLACEHOLDER checksum
3. Build
4. Calculate checksum
5. Replace PLACEHOLDER with real checksum
6. **Commit and push ALL changes**
7. **THEN create GitHub release** with correct flags

### Critical Flags
```bash
# Dev release
gh release create vX.X.X.0 file.zip --target dev --prerelease

# Main release (NO --prerelease)
gh release create vX.X.X.0 file.zip --target main
```

### Common Mistakes
- Creating release BEFORE committing manifest → duplicate installs
- Missing `--target dev` flag → release on wrong branch
- Missing `--prerelease` for dev → confuses users

## GitHub Issue Linking in Commits

### Auto-Link Commits to Issues
Include issue number in commit message:

```bash
# Links AND closes issue
git commit -m "v1.0.325.0: Fix sort buttons (fixes #28)"

# Links without closing
git commit -m "v1.0.325.0: Partial fix (refs #28)"
```

### Keywords
- `fixes #123` - closes issue
- `closes #123` - closes issue
- `resolves #123` - closes issue
- `refs #123` - links only (no close)

### Result
GitHub automatically shows commit in issue timeline with version label

## Social Features - Privacy Settings (v2.0.185.0 - v2.0.190.0)

### Default Privacy Settings
Set defaults to **public/everyone** for social discovery:
```csharp
public string ShowOnlineStatus { get; set; } = "Everyone";
public string ShowWatchedHistory { get; set; } = "Everyone";
public string AllowMessages { get; set; } = "Everyone";
```
Users can restrict later if needed. Restrictive defaults block testing/discovery.

### Privacy Enforcement in APIs
When returning data, check privacy settings:
```csharp
var showStatus = profile == null ||
    profile.Privacy.ShowOnlineStatus == "Everyone" ||
    (profile.Privacy.ShowOnlineStatus == "Friends" && isFriend);

var displayStatus = showStatus ? effectiveStatus : "Offline";
```

### Null Check for Privacy Object
Profiles loaded from JSON may have null Privacy:
```csharp
if (profile.Privacy == null)
{
    profile.Privacy = new UserPrivacySettings();
}
```

## Draggable & Resizable UI Panels (v2.0.191.0 - v2.0.192.0)

### Making Panel Draggable by Header
```javascript
header.addEventListener('mousedown', function (e) {
    if (e.target.classList.contains('close-btn')) return; // Don't drag on close
    isDragging = true;
    dragOffsetX = e.clientX - panel.offsetLeft;
    dragOffsetY = e.clientY - panel.offsetTop;
});

document.addEventListener('mousemove', function (e) {
    if (!isDragging) return;
    panel.style.left = (e.clientX - dragOffsetX) + 'px';
    panel.style.top = (e.clientY - dragOffsetY) + 'px';
    panel.style.right = 'auto';
    panel.style.bottom = 'auto';
});
```

### Saving Panel Position to localStorage
```javascript
savePanelPosition: function () {
    var pos = {
        left: panel.style.left,
        top: panel.style.top,
        width: panel.style.width,
        height: panel.style.height
    };
    localStorage.setItem('panelPosition', JSON.stringify(pos));
}
```

### Custom Resize Handle (Bottom-Left)
CSS `resize: both` only works on bottom-right. For custom corner, use JavaScript:
```javascript
resizeHandle.addEventListener('mousedown', function (e) {
    isResizing = true;
    startX = e.clientX;
    startWidth = panel.offsetWidth;
    startLeft = panel.offsetLeft;
});

document.addEventListener('mousemove', function (e) {
    if (!isResizing) return;
    var deltaX = startX - e.clientX; // Inverted for left-side
    var newWidth = startWidth + deltaX;
    var newLeft = startLeft - deltaX;
    panel.style.width = newWidth + 'px';
    panel.style.left = newLeft + 'px';
});
```

### Resize Handle Styling
Don't use ugly gradients. Use proper icon with pseudo-elements:
```css
.resize-handle::before,
.resize-handle::after {
    content: '';
    position: absolute;
    background: #666;
    width: 10px;
    height: 2px;
    transform: rotate(-45deg);
}
```

## Online Users Tab (v2.0.186.0 - v2.0.188.0)

### Showing Only Truly Online Users
Filter by actual status, not by presence in friends list:
```csharp
if (displayStatus != "Online")
{
    continue; // Skip non-online users
}
```

### Click-to-Navigate Pattern
Make list items clickable with stopPropagation on buttons:
```javascript
html += '<div onclick="Plugin.navigateToProfile(\'' + userId + '\')">' +
    '<button onclick="event.stopPropagation();Plugin.addFriend(\'' + userId + '\')">Add</button>' +
    '</div>';
```

## UI Tab Badges (v2.0.191.0)

### Badge Positioning
Don't position badge absolutely - it overlaps text. Use inline:
```css
.tab-badge {
    display: inline-block;
    margin-left: 2px;
    vertical-align: middle;
}
```

### Fixed Panel Height with Scroll
Instead of `max-height`, use fixed `height` with overflow:
```css
.panel {
    height: 450px;
}
.panel-content {
    flex: 1;
    overflow-y: auto;
    min-height: 200px;
}
```

## Jellyfin Search API (v2.0.217.0)

### Include Music Types in Search
The `/Search/Hints` endpoint requires explicit `IncludeItemTypes` to return music:

**Before (missing music):**
```javascript
const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${query}&UserId=${userId}&IncludeItemTypes=Movie,Series,Episode&Limit=50`;
```

**After (includes music):**
```javascript
const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${query}&UserId=${userId}&IncludeItemTypes=Movie,Series,Episode,Audio,MusicAlbum,MusicArtist&Limit=50`;
```

### Available Search Types
- `Movie`, `Series`, `Episode` - Video content
- `Audio` - Individual songs/tracks
- `MusicAlbum` - Albums
- `MusicArtist` - Artists
- `BoxSet`, `MusicVideo`, `Video` - Other media

## Keyboard Event Isolation in SPA (v2.0.216.0)

### Problem
When typing in chat input while video is playing, keyboard events bubble up to video player. Pressing space to add a space between words pauses the video.

### Solution: stopPropagation on All Keyboard Events
```javascript
input.onkeydown = function (e) {
    e.stopPropagation();  // Prevent video player from receiving event
    if (self.handleAutocompleteKeydown(e)) return;
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        self.sendCurrentMessage();
    }
};
input.onkeyup = function (e) { e.stopPropagation(); };
input.onkeypress = function (e) { e.stopPropagation(); };
```

### Why All Three Events
- `keydown` - Initial key press (most video players listen here)
- `keyup` - Key release (some players listen here)
- `keypress` - Character input (deprecated but some legacy code uses it)

All three need stopPropagation to ensure complete isolation.

## localStorage Position Bounds Checking (v2.0.218.0)

### Problem
Draggable UI elements save position to localStorage. If user moves element to edge, then window shrinks (or different device), element can be positioned off-screen and become unreachable.

### Solution: Validate Position on Load
```javascript
var savedPos = localStorage.getItem('socialFriendsBtnPos');
if (savedPos) {
    try {
        var pos = JSON.parse(savedPos);
        var maxBottom = window.innerHeight - 60;  // Element height + margin
        var maxRight = window.innerWidth - 60;

        // If off-screen, reset to default
        if (pos.bottom > maxBottom || pos.right > maxRight || pos.bottom < 10 || pos.right < 10) {
            localStorage.removeItem('socialFriendsBtnPos');
            btn.style.bottom = '20px';
            btn.style.right = '20px';
        } else {
            btn.style.bottom = pos.bottom + 'px';
            btn.style.right = pos.right + 'px';
        }
    } catch (e) {
        btn.style.bottom = '20px';
        btn.style.right = '20px';
    }
}
```

### Also: Window Resize Listener
Reposition elements if window shrinks after load:
```javascript
window.addEventListener('resize', function () {
    var btn = document.getElementById('my-draggable-btn');
    if (!btn) return;

    var bottom = parseInt(btn.style.bottom) || 20;
    var right = parseInt(btn.style.right) || 20;
    var maxBottom = window.innerHeight - 60;
    var maxRight = window.innerWidth - 60;

    if (bottom > maxBottom || right > maxRight) {
        btn.style.bottom = Math.min(bottom, maxBottom) + 'px';
        btn.style.right = Math.min(right, maxRight) + 'px';
        // Update saved position
        localStorage.setItem('myBtnPos', JSON.stringify({
            bottom: Math.min(bottom, maxBottom),
            right: Math.min(right, maxRight)
        }));
    }
});
```

### Key Points
- Always validate saved positions against current viewport
- Reset to safe default if position is invalid
- Listen for resize to handle viewport changes after load
- Use element dimensions + margin for boundary calculations

## Linux Disk Detection in Docker/LVM Environments

### Problem
On Linux, especially in Docker containers with device mapper or LVM:
- `DriveInfo.GetDrives()` returns all mount points, not physical disks
- Device mapper abstracts physical devices (`/dev/mapper/...`)
- Multiple mount points may share the same underlying storage
- Docker bind mounts (/etc/resolv.conf, /etc/hosts) appear as separate "drives"

### Wrong Approaches
1. **Grouping by TotalSize** - Different physical disks can have same capacity
2. **Parsing /dev/mapper names** - Groups everything under one device
3. **Reading /proc/mounts device names** - Device mapper obscures actual devices

### Correct Approach
Group filesystems by their **actual storage identity**: (TotalSize + AvailableFreeSpace)

```csharp
// Same physical disk/partition = same total AND same free space
var grouped = meaningfulDrives
    .GroupBy(d => (d.TotalSize, d.AvailableFreeSpace))
    .ToList();
```

### Filter Out Docker Bind Mounts
```csharp
var meaningfulDrives = allDrives.Where(d =>
{
    var name = d.Name.TrimEnd('/');
    if (name.StartsWith("/etc/")) return false;  // Docker bind mounts
    if (name.StartsWith("/proc")) return false;
    if (name.StartsWith("/sys")) return false;
    if (name.StartsWith("/run")) return false;
    return true;
}).ToList();
```

### Naming Strategy
```csharp
if (primaryMount == "/") {
    driveName = "System";
} else if (primaryMount.Contains("media")) {
    driveName = $"Media Storage {diskNumber++}";
} else {
    driveName = $"Disk {diskNumber++}";
}
```

## Modal State Management

### Problem
When closing and reopening a modal, visual state (active tab) persists but content loads for default tab.

### Solution
Reset both state AND visual selection when opening modal:
```javascript
openModal: function () {
    // Reset internal state
    this.currentTab = 'default';

    // Reset visual tab selection
    const tabs = modal.querySelectorAll('.tab-button');
    tabs.forEach((tab, index) => {
        if (index === 0) {
            tab.classList.add('active');
        } else {
            tab.classList.remove('active');
        }
    });

    // Reset panel visibility
    controls.style.display = 'flex';
    settings.style.display = 'none';

    // Load content for default tab
    this.loadDefaultContent();
}
```

## Notifications: Toast vs Overlay

### When to Use Each

**Full-screen Overlay** - NEVER for informational notifications
- Only for critical blocking actions requiring immediate user decision
- Example: "Are you sure you want to delete?" confirmation

**Toast Notification (bottom-left corner)** - For status updates
- Server restart countdown
- Action confirmations
- Background process status
- Non-blocking, user can continue working

### Toast Implementation
```css
.toast-notification {
    position: fixed;
    bottom: 20px;
    left: 20px;
    background: linear-gradient(135deg, #1a1a1a, #2d2d2d);
    border: 1px solid #e74c3c;
    border-radius: 12px;
    padding: 15px 20px;
    z-index: 99999999;
    animation: slideInLeft 0.3s ease;
}

@keyframes slideInLeft {
    from { transform: translateX(-100%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
}
```

## API Response Structure for Frontend

### Common Mistake
Backend returns object with nested data, frontend treats response as array:
```javascript
// WRONG - response is {Disks: [...], TotalGB: ...}
const disks = await response.json();
disks.forEach(disk => ...)  // ERROR: forEach is not a function
```

### Correct Pattern
```javascript
// CORRECT - access the nested array
const data = await response.json();
data.Disks.forEach(disk => ...)
```

### Best Practice for API Design
Include summary data alongside array:
```csharp
return Ok(new {
    Disks = diskList,              // Array of items
    TotalStorageGB = totalStorage, // Summary
    TotalUsedGB = totalUsed,       // Summary
    TotalFreeGB = totalFree        // Summary
});
```

Frontend can then show both list and summary:
```javascript
const data = await response.json();
showSummary(data.TotalStorageGB, data.TotalUsedGB, data.TotalFreeGB);
data.Disks.forEach(disk => renderDisk(disk));
```

## Security Hardening (v1.0.330.0 / v2.0.228.0)

### ReDoS (Regular Expression Denial of Service) Protection

#### The Problem
Inline regex with unbounded patterns can hang the server with crafted input:
```csharp
// VULNERABLE - no timeout, compiled fresh every call
Regex.Replace(input, @"<[^>]*?>", "", RegexOptions.None);
```

Malicious input like `<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<` can cause exponential backtracking.

#### The Fix
Pre-compile regex with timeout at class level:
```csharp
// SAFE - compiled once, 100ms timeout prevents DoS
private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
private static readonly Regex HtmlTagRegex = new(@"<[^>]*?>", RegexOptions.Compiled, RegexTimeout);

// Usage
sanitized = HtmlTagRegex.Replace(input, string.Empty);
```

#### Key Points
- **Compiled** = faster execution, loaded once
- **Timeout** = prevents infinite backtracking
- **100ms** is generous for any legitimate input

### Memory Leak Prevention in Rate Limiting

#### The Problem
ConcurrentDictionary grows unbounded if entries aren't cleaned:
```csharp
// VULNERABLE - can grow infinitely under attack
private static readonly ConcurrentDictionary<Guid, RateLimitEntry> _rateLimits = new();
```

#### The Fix
Add maximum size limit with emergency cleanup:
```csharp
private const int MaxRateLimitEntries = 10000;

if (_rateLimits.Count > MaxRateLimitEntries)
{
    lock (_cleanupLock)
    {
        if (_rateLimits.Count > MaxRateLimitEntries)
        {
            // Remove oldest half of entries
            var toRemove = _rateLimits
                .OrderBy(x => x.Value.ResetTime)
                .Take(_rateLimits.Count / 2)
                .Select(x => x.Key)
                .ToList();
            foreach (var key in toRemove)
            {
                _rateLimits.TryRemove(key, out _);
            }
        }
    }
}
```

### IDOR (Insecure Direct Object Reference) Protection

#### The Problem
API endpoints that accept user IDs without verifying authorization:
```csharp
// VULNERABLE - anyone can view any user's ratings
[HttpGet("Users/{userId}/Ratings")]
public ActionResult GetUserRatings(Guid userId)
{
    return Ok(_repository.GetUserRatings(userId));
}
```

#### The Fix
Verify the requester is authorized to access the resource:
```csharp
// SAFE - only own ratings or admin can view
var targetUserId = userId ?? authUserId;

if (targetUserId != authUserId && !IsJellyfinAdmin(authUserId))
{
    return Forbid("Cannot view another user's ratings");
}

return Ok(_repository.GetUserRatings(targetUserId));
```

### XSS Protection in JavaScript

#### The Problem
Using innerHTML with user-controlled data:
```javascript
// VULNERABLE - allows script injection
element.innerHTML = '<button onclick="doAction(\'' + userId + '\')">Click</button>';
```

#### The Fix
Use escapeJs() for onclick handler parameters:
```javascript
// Helper function
escapeJs: function (text) {
    if (text == null) return '';
    return String(text)
        .replace(/\\/g, '\\\\')
        .replace(/'/g, "\\'")
        .replace(/"/g, '\\"')
        .replace(/\n/g, '\\n')
        .replace(/\r/g, '\\r')
        .replace(/</g, '\\x3c')
        .replace(/>/g, '\\x3e');
}

// Usage
'<button onclick="doAction(\'' + self.escapeJs(userId) + '\')">Click</button>'
```

For text content, use escapeHtml():
```javascript
escapeHtml: function (text) {
    if (text == null) return '';
    return String(text)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
```

### Security Scanner False Positives

Automated scanners often flag ALL innerHTML usage as XSS. In reality:
- **Static HTML** (templates, icons, loading states) = SAFE
- **Dynamic content with escapeHtml()** = SAFE
- **onclick params with escapeJs()** = SAFE
- **Only unescaped user data in innerHTML** = VULNERABLE

Don't rewrite all innerHTML to DOM APIs just to satisfy a scanner - evaluate actual risk.

### Information Disclosure Prevention

#### The Problem
Detailed error messages leak implementation details:
```csharp
// VULNERABLE - exposes internal structure
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing request");
    return StatusCode(500, ex.Message); // Leaks stack trace info
}
```

#### The Fix
Use generic messages for clients, log details server-side:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing request");
    return StatusCode(500, "Internal server error"); // Generic
}
```

Also use appropriate log levels:
```csharp
// User activity = Debug (not exposed to users, only for troubleshooting)
_logger.LogDebug("Retrieved {Count} ratings for user", ratings.Count);

// Errors = Error (important for monitoring)
_logger.LogError(ex, "Failed to process request");
```
