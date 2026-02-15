# Live Chat Feature - Implementation Plan

## Overview

Add a real-time live chat system for connected Jellyfin users. The chat button replaces the "Cast to Device" button position and inherits all its responsive behavior.

---

## Phase 1: Backend Infrastructure

### 1.1 New Model Files

**File: `Models/ChatMessage.cs`**
```csharp
public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string? UserAvatar { get; set; }  // User profile image URL
    public string Content { get; set; }       // Sanitized message text
    public string? GifUrl { get; set; }       // Optional GIF URL (Tenor)
    public DateTime Timestamp { get; set; }
    public bool IsDeleted { get; set; }       // Soft delete for moderation
    public Guid? ReplyToId { get; set; }      // Reply to another message
}
```

**File: `Models/ChatUser.cs`**
```csharp
public class ChatUser
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string? Avatar { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsTyping { get; set; }
    public DateTime? TypingStarted { get; set; }
}
```

**File: `Models/ChatMessageDto.cs`**
```csharp
public class ChatMessageDto
{
    public string Content { get; set; }
    public string? GifUrl { get; set; }
    public Guid? ReplyToId { get; set; }
}
```

### 1.2 Repository Extension

**File: `Data/RatingsRepository.cs`** (add to existing)

Add new dictionaries and methods:
```csharp
private List<ChatMessage> _chatMessages;
private Dictionary<Guid, ChatUser> _chatUsers;

// Methods:
- LoadChatMessages() / SaveChatMessages()
- AddChatMessage(ChatMessage message)
- GetRecentMessages(int count = 100, DateTime? since = null)
- DeleteMessage(Guid messageId, Guid adminUserId)
- UpdateUserPresence(Guid userId, string userName, string? avatar)
- SetTypingStatus(Guid userId, bool isTyping)
- GetOnlineUsers(TimeSpan activeWindow = 5 minutes)
- CleanOldMessages(int retentionDays)
```

**Data file:** `ratings/chat-messages.json`

### 1.3 API Controller

**File: `Api/ChatController.cs`** (new file)

```csharp
[ApiController]
[Route("Ratings/Chat")]
[Produces(MediaTypeNames.Application.Json)]
public class ChatController : ControllerBase
{
    // GET /Ratings/Chat/Messages?since={timestamp}&limit=100
    [HttpGet("Messages")]
    [Authorize]
    public ActionResult<List<ChatMessage>> GetMessages(DateTime? since, int limit = 100)

    // POST /Ratings/Chat/Messages
    [HttpPost("Messages")]
    [Authorize]
    public ActionResult<ChatMessage> SendMessage(ChatMessageDto dto)

    // DELETE /Ratings/Chat/Messages/{id} (admin only)
    [HttpDelete("Messages/{id}")]
    [Authorize(Policy = "RequiresElevation")]
    public ActionResult DeleteMessage(Guid id)

    // GET /Ratings/Chat/Users/Online
    [HttpGet("Users/Online")]
    [Authorize]
    public ActionResult<List<ChatUser>> GetOnlineUsers()

    // POST /Ratings/Chat/Typing
    [HttpPost("Typing")]
    [Authorize]
    public ActionResult SetTyping(bool isTyping)

    // POST /Ratings/Chat/Heartbeat
    [HttpPost("Heartbeat")]
    [Authorize]
    public ActionResult Heartbeat()

    // GET /Ratings/Chat/Config
    [HttpGet("Config")]
    [AllowAnonymous]
    public ActionResult GetChatConfig()
}
```

### 1.4 Security Layer

**Input Sanitization (in ChatController):**
```csharp
private string SanitizeMessage(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return string.Empty;

    // 1. Trim and limit length (max 500 chars)
    input = input.Trim();
    if (input.Length > 500) input = input.Substring(0, 500);

    // 2. HTML encode to prevent XSS
    input = System.Web.HttpUtility.HtmlEncode(input);

    // 3. Remove potential script patterns
    input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
    input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

    return input;
}
```

**Rate Limiting:**
- Track messages per user per minute (max 10 messages/minute)
- Typing status updates limited to 1 per 2 seconds
- Store rate limit data in memory dictionary

**GIF URL Validation:**
- Only allow URLs from trusted domains: `tenor.com`, `media.tenor.com`, `giphy.com`, `media.giphy.com`

### 1.5 Configuration

**File: `Configuration/PluginConfiguration.cs`** (add properties)

```csharp
// Chat settings
public bool EnableChat { get; set; } = true;
public int ChatMessageRetentionDays { get; set; } = 7;
public int ChatRateLimitPerMinute { get; set; } = 10;
public int ChatMaxMessageLength { get; set; } = 500;
public bool ChatAllowGifs { get; set; } = true;
public bool ChatAllowEmojis { get; set; } = true;
public string TenorApiKey { get; set; } = "";  // For GIF search
```

**File: `Configuration/configPage.html`** (add section)

Add "Chat Settings" section with toggles for all chat options.

---

## Phase 2: Frontend - Chat Button

### 2.1 Replace Cast Button

**Target button HTML:**
```html
<button is="paper-icon-button-light"
        class="headerCastButton castButton headerButton headerButtonRight paper-icon-button-light"
        title="Cast to Device">
    <span class="material-icons cast" aria-hidden="true"></span>
</button>
```

**New chat button (replaces cast):**
```javascript
// In initChatButton()
const castBtn = document.querySelector('.headerCastButton, .castButton');
if (castBtn) {
    // Hide or remove cast button
    castBtn.style.display = 'none';

    // Create chat button with same classes for positioning
    const chatBtn = document.createElement('button');
    chatBtn.id = 'liveChatBtn';
    chatBtn.className = 'headerChatButton headerButton headerButtonRight paper-icon-button-light';
    chatBtn.setAttribute('is', 'paper-icon-button-light');
    chatBtn.setAttribute('title', self.t('liveChat'));
    chatBtn.innerHTML = '<span class="material-icons chat" aria-hidden="true"></span>';

    // Insert where cast button was
    castBtn.parentNode.insertBefore(chatBtn, castBtn);
}
```

### 2.2 Chat Button Styling

```css
/* Chat button - inherits headerButton styles */
#liveChatBtn {
    position: relative;
}

/* Online indicator dot */
#liveChatBtn .online-dot {
    position: absolute;
    top: 4px;
    right: 4px;
    width: 8px;
    height: 8px;
    background: #4CAF50;
    border-radius: 50%;
    border: 2px solid #1a1a1a;
}

/* Unread badge */
#liveChatBtn .unread-badge {
    position: absolute;
    top: 2px;
    right: 2px;
    min-width: 16px;
    height: 16px;
    background: #e91e63;
    border-radius: 8px;
    font-size: 10px;
    color: white;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 0 4px;
}

/* Responsive - match cast button behavior */
@media (max-width: 600px) {
    #liveChatBtn {
        /* Same as cast button on mobile */
    }
}
```

---

## Phase 3: Frontend - Chat Window

### 3.1 Chat Window Structure

```html
<div id="liveChatWindow" class="chat-window">
    <div class="chat-header">
        <span class="chat-title">Live Chat</span>
        <div class="chat-online-count">
            <span class="online-dot"></span>
            <span class="count">3 online</span>
        </div>
        <button class="chat-minimize">−</button>
        <button class="chat-close">×</button>
    </div>

    <div class="chat-users-bar">
        <!-- Online user avatars -->
        <div class="chat-user-avatar" title="Username">
            <img src="/Users/{id}/Images/Primary" />
        </div>
        ...
    </div>

    <div class="chat-messages" id="chatMessages">
        <!-- Messages rendered here -->
    </div>

    <div class="chat-typing-indicator" id="chatTyping">
        <!-- "User is typing..." -->
    </div>

    <div class="chat-input-area">
        <button class="chat-emoji-btn" title="Emoji">😀</button>
        <button class="chat-gif-btn" title="GIF">GIF</button>
        <input type="text" id="chatInput" placeholder="Type a message..." maxlength="500" />
        <button class="chat-send-btn" id="chatSendBtn">
            <span class="material-icons">send</span>
        </button>
    </div>
</div>
```

### 3.2 Chat Window Positioning

```css
.chat-window {
    position: fixed;
    bottom: 20px;
    right: 20px;
    width: 360px;
    height: 500px;
    max-height: 70vh;
    background: #1a1a1a;
    border-radius: 12px;
    box-shadow: 0 8px 32px rgba(0,0,0,0.4);
    display: flex;
    flex-direction: column;
    z-index: 10000;
    border: 1px solid rgba(255,255,255,0.1);
    overflow: hidden;
}

/* Minimized state */
.chat-window.minimized {
    height: 48px;
}

.chat-window.minimized .chat-messages,
.chat-window.minimized .chat-input-area,
.chat-window.minimized .chat-users-bar,
.chat-window.minimized .chat-typing-indicator {
    display: none;
}

/* Mobile responsive */
@media (max-width: 600px) {
    .chat-window {
        position: fixed;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        width: 100%;
        height: 100%;
        max-height: 100%;
        border-radius: 0;
    }
}
```

### 3.3 Message Rendering

```javascript
renderMessage: function(msg) {
    const isOwn = msg.UserId === this.currentUserId;
    const time = this.formatTime(msg.Timestamp);

    let contentHtml = '';
    if (msg.GifUrl) {
        contentHtml = `<img class="chat-gif" src="${this.escapeHtml(msg.GifUrl)}" alt="GIF" />`;
    } else {
        contentHtml = `<span class="chat-text">${this.renderEmojis(this.escapeHtml(msg.Content))}</span>`;
    }

    // Reply preview if replying
    let replyHtml = '';
    if (msg.ReplyToId && msg.ReplyTo) {
        replyHtml = `
            <div class="chat-reply-preview">
                <span class="reply-user">${this.escapeHtml(msg.ReplyTo.UserName)}</span>
                <span class="reply-text">${this.escapeHtml(msg.ReplyTo.Content).substring(0, 50)}...</span>
            </div>
        `;
    }

    return `
        <div class="chat-message ${isOwn ? 'own' : ''}" data-id="${msg.Id}">
            ${!isOwn ? `<img class="chat-avatar" src="${msg.UserAvatar || '/web/assets/img/avatar.png'}" />` : ''}
            <div class="chat-bubble">
                ${!isOwn ? `<div class="chat-username">${this.escapeHtml(msg.UserName)}</div>` : ''}
                ${replyHtml}
                ${contentHtml}
                <div class="chat-time">${time}</div>
            </div>
        </div>
    `;
}
```

### 3.4 Message Styling

```css
.chat-messages {
    flex: 1;
    overflow-y: auto;
    padding: 12px;
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.chat-message {
    display: flex;
    gap: 8px;
    max-width: 85%;
}

.chat-message.own {
    align-self: flex-end;
    flex-direction: row-reverse;
}

.chat-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    object-fit: cover;
}

.chat-bubble {
    background: #2d2d2d;
    border-radius: 12px;
    padding: 8px 12px;
    max-width: 100%;
    word-wrap: break-word;
}

.chat-message.own .chat-bubble {
    background: #0066cc;
}

.chat-username {
    font-size: 11px;
    color: #888;
    margin-bottom: 4px;
}

.chat-text {
    font-size: 14px;
    line-height: 1.4;
}

.chat-time {
    font-size: 10px;
    color: #666;
    margin-top: 4px;
    text-align: right;
}

.chat-gif {
    max-width: 200px;
    max-height: 150px;
    border-radius: 8px;
}
```

---

## Phase 4: Emoji System

### 4.1 Emoji Picker

```javascript
// Common emoji categories
const emojiCategories = {
    recent: [],  // Stored in localStorage
    smileys: ['😀', '😂', '😍', '🥰', '😎', '🤔', '😢', '😡', '🎉', '❤️', '👍', '👎', '🙏', '💯', '🔥'],
    gestures: ['👋', '👏', '🤝', '✌️', '🤞', '🤙', '👌', '✋', '🖐️', '🤚'],
    objects: ['🎬', '🎥', '📺', '🎮', '🎧', '🍿', '🎵', '📱', '💻', '⭐'],
    symbols: ['❤️', '💔', '💕', '✨', '💫', '⚡', '🌟', '💥', '💢', '💤']
};

renderEmojiPicker: function() {
    return `
        <div class="emoji-picker" id="emojiPicker">
            <div class="emoji-tabs">
                <button data-cat="recent" class="active">🕐</button>
                <button data-cat="smileys">😀</button>
                <button data-cat="gestures">👋</button>
                <button data-cat="objects">🎬</button>
                <button data-cat="symbols">❤️</button>
            </div>
            <div class="emoji-grid" id="emojiGrid">
                <!-- Emojis rendered here -->
            </div>
        </div>
    `;
}
```

### 4.2 Emoji Styling

```css
.emoji-picker {
    position: absolute;
    bottom: 60px;
    left: 12px;
    width: 280px;
    background: #2d2d2d;
    border-radius: 12px;
    box-shadow: 0 4px 16px rgba(0,0,0,0.3);
    overflow: hidden;
    z-index: 10001;
}

.emoji-tabs {
    display: flex;
    border-bottom: 1px solid #444;
}

.emoji-tabs button {
    flex: 1;
    padding: 8px;
    background: none;
    border: none;
    font-size: 18px;
    cursor: pointer;
    opacity: 0.6;
}

.emoji-tabs button.active {
    opacity: 1;
    background: #3d3d3d;
}

.emoji-grid {
    display: grid;
    grid-template-columns: repeat(7, 1fr);
    gap: 4px;
    padding: 8px;
    max-height: 200px;
    overflow-y: auto;
}

.emoji-grid button {
    background: none;
    border: none;
    font-size: 22px;
    padding: 4px;
    cursor: pointer;
    border-radius: 4px;
}

.emoji-grid button:hover {
    background: #444;
}
```

---

## Phase 5: GIF Integration (Tenor)

### 5.1 GIF Search

```javascript
// Tenor API integration
searchGifs: async function(query) {
    const apiKey = this.chatConfig.TenorApiKey;
    if (!apiKey) return [];

    const url = `https://tenor.googleapis.com/v2/search?q=${encodeURIComponent(query)}&key=${apiKey}&limit=20&media_filter=gif,tinygif`;

    try {
        const response = await fetch(url);
        const data = await response.json();
        return data.results.map(r => ({
            id: r.id,
            preview: r.media_formats.tinygif.url,
            full: r.media_formats.gif.url
        }));
    } catch (e) {
        console.error('GIF search failed:', e);
        return [];
    }
}
```

### 5.2 GIF Picker UI

```html
<div class="gif-picker" id="gifPicker">
    <div class="gif-search">
        <input type="text" id="gifSearchInput" placeholder="Search GIFs..." />
    </div>
    <div class="gif-trending-label">Trending</div>
    <div class="gif-grid" id="gifGrid">
        <!-- GIF thumbnails -->
    </div>
    <div class="gif-powered-by">
        Powered by Tenor
    </div>
</div>
```

---

## Phase 6: Real-Time Updates

### 6.1 Polling Mechanism (MVP)

```javascript
// Start polling for new messages
startChatPolling: function() {
    const self = this;
    this.chatPollingInterval = setInterval(async () => {
        if (!this.chatWindowOpen) return;

        try {
            // Fetch new messages since last timestamp
            const response = await fetch(`${this.baseUrl}/Ratings/Chat/Messages?since=${this.lastMessageTime}`);
            const messages = await response.json();

            if (messages.length > 0) {
                messages.forEach(msg => self.appendMessage(msg));
                self.lastMessageTime = messages[messages.length - 1].Timestamp;
            }

            // Fetch online users
            const usersResponse = await fetch(`${this.baseUrl}/Ratings/Chat/Users/Online`);
            const users = await usersResponse.json();
            self.updateOnlineUsers(users);

            // Fetch typing indicators
            const typingUsers = users.filter(u => u.IsTyping && u.UserId !== self.currentUserId);
            self.updateTypingIndicator(typingUsers);

        } catch (e) {
            console.error('Chat polling error:', e);
        }
    }, 2000); // Poll every 2 seconds
},

stopChatPolling: function() {
    if (this.chatPollingInterval) {
        clearInterval(this.chatPollingInterval);
        this.chatPollingInterval = null;
    }
}
```

### 6.2 Heartbeat for Presence

```javascript
// Send heartbeat every 30 seconds to maintain online status
startHeartbeat: function() {
    this.heartbeatInterval = setInterval(() => {
        fetch(`${this.baseUrl}/Ratings/Chat/Heartbeat`, {
            method: 'POST',
            credentials: 'include'
        });
    }, 30000);
}
```

---

## Phase 7: Translations

Add to all 16 language objects in `translations`:

```javascript
// Chat translations
liveChat: 'Live Chat',
chatOnline: 'online',
chatTyping: 'is typing...',
chatTypingMultiple: 'are typing...',
chatSend: 'Send',
chatPlaceholder: 'Type a message...',
chatNoMessages: 'No messages yet. Start the conversation!',
chatSearchGif: 'Search GIFs...',
chatTrending: 'Trending',
chatPoweredBy: 'Powered by Tenor',
chatDeleted: 'Message deleted',
chatYou: 'You',
chatJustNow: 'Just now',
chatMinutesAgo: 'min ago',
chatHoursAgo: 'h ago',
chatYesterday: 'Yesterday',
```

---

## Phase 8: Admin Features

### 8.1 Message Moderation

Admin users can:
- Delete any message (soft delete)
- View deleted messages (grayed out)
- Ban users from chat (reuse existing ban system)

### 8.2 Config Page Options

Add to configPage.html:
```html
<div class="verticalSection">
    <h2 class="sectionTitle">Chat Settings</h2>

    <div class="checkboxContainer">
        <label class="emby-checkbox-label">
            <input type="checkbox" id="chkEnableChat" is="emby-checkbox" />
            <span>Enable Live Chat</span>
        </label>
    </div>

    <div class="inputContainer">
        <label for="txtChatRetentionDays">Message Retention (days)</label>
        <input type="number" id="txtChatRetentionDays" min="1" max="365" />
    </div>

    <div class="inputContainer">
        <label for="txtChatRateLimit">Messages per minute limit</label>
        <input type="number" id="txtChatRateLimit" min="1" max="60" />
    </div>

    <div class="checkboxContainer">
        <label class="emby-checkbox-label">
            <input type="checkbox" id="chkChatAllowGifs" is="emby-checkbox" />
            <span>Allow GIFs</span>
        </label>
    </div>

    <div class="inputContainer">
        <label for="txtTenorApiKey">Tenor API Key (for GIF search)</label>
        <input type="text" id="txtTenorApiKey" />
        <div class="fieldDescription">
            Get a free API key at <a href="https://developers.google.com/tenor/guides/quickstart" target="_blank">Tenor Developer Portal</a>
        </div>
    </div>
</div>
```

---

## Implementation Order

### Sprint 1: Core Backend (Day 1)
1. Create `Models/ChatMessage.cs`, `Models/ChatUser.cs`, `Models/ChatMessageDto.cs`
2. Add chat methods to `RatingsRepository.cs`
3. Create `Api/ChatController.cs` with basic endpoints
4. Add chat config properties to `PluginConfiguration.cs`

### Sprint 2: Basic Frontend (Day 1-2)
1. Add chat button (replace cast button)
2. Create chat window HTML/CSS structure
3. Implement open/close/minimize
4. Basic message sending and receiving
5. Polling mechanism

### Sprint 3: Features (Day 2)
1. User avatars
2. Typing indicators
3. Online user list
4. Emoji picker

### Sprint 4: GIFs & Polish (Day 2-3)
1. Tenor GIF integration
2. Reply to messages
3. Unread badge
4. Sound notifications (optional)
5. Mobile responsive fixes

### Sprint 5: Security & Admin (Day 3)
1. Rate limiting
2. Input sanitization review
3. Admin moderation tools
4. Config page UI
5. Translations (all 16 languages)

---

## File Changes Summary

### New Files:
- `Models/ChatMessage.cs`
- `Models/ChatUser.cs`
- `Models/ChatMessageDto.cs`
- `Api/ChatController.cs`

### Modified Files:
- `Data/RatingsRepository.cs` - Add chat methods
- `Configuration/PluginConfiguration.cs` - Add chat settings
- `Configuration/configPage.html` - Add chat settings UI
- `Web/ratings.js` - Add all chat frontend code (~1500-2000 lines)

### New Data Files (auto-created):
- `ratings/chat-messages.json`
- `ratings/chat-users.json`

---

## Security Checklist

- [ ] HTML encode all user input before display
- [ ] Validate GIF URLs against whitelist (tenor.com, giphy.com)
- [ ] Rate limit messages (10/min default)
- [ ] Message length limit (500 chars)
- [ ] Sanitize message content (remove script patterns)
- [ ] Require authentication for all chat endpoints
- [ ] Admin-only delete endpoint
- [ ] Reuse existing ban system for chat bans
- [ ] No file uploads (GIFs via URL only)
- [ ] CSRF protection via Jellyfin's built-in token system

---

## Estimated Lines of Code

| Component | Estimated LOC |
|-----------|---------------|
| Models (3 files) | ~80 |
| ChatController.cs | ~400 |
| Repository additions | ~250 |
| Configuration additions | ~50 |
| ratings.js chat code | ~2000 |
| CSS styling | ~400 |
| configPage.html additions | ~80 |
| **Total** | **~3260** |

---

## Questions for User

1. **GIF Provider**: Tenor (free, Google-owned) or Giphy? Tenor recommended for free API.
2. **Sound Notifications**: Enable sound for new messages when chat is minimized?
3. **Message History**: How many days to retain? (Default: 7)
4. **Private Messages**: Only global chat, or also 1-on-1 DMs? (Recommend global only for MVP)
