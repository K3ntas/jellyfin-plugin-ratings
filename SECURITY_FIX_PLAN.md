# Security Vulnerability Fix Plan

## Overview
This document outlines the plan to fix 20 security vulnerabilities identified in the Jellyfin Ratings Plugin.

---

## CRITICAL SEVERITY (Fix First)

### Issue #1: Fire-and-Forget Async Saves - Silent Data Loss
**File:** `Data/RatingsRepository.cs`
**Lines:** 136, 148, 248, 314, 385, 405, 469, 492, 514, 566, 719, 724, 743, 811, 877, 934, 1036, 1086, 1151, 1196, 1215, 1324, 1378, 1472, 1493, 1584, 1610, 1710, 1821, 1839

**Problem:** 29 instances of `_ = SaveAsync()` fire-and-forget pattern. Exceptions are swallowed silently, and data loss can occur.

**Fix Strategy:**
1. Add error logging to all fire-and-forget saves using `.ContinueWith()`
2. Create a helper method for safe fire-and-forget:
```csharp
private void SafeFireAndForget(Task task, string operation)
{
    task.ContinueWith(t =>
    {
        if (t.IsFaulted)
            _logger.LogError(t.Exception, "Failed to save {Operation}", operation);
    }, TaskContinuationOptions.OnlyOnFaulted);
}
```
3. Replace all `_ = SaveXxxAsync()` with `SafeFireAndForget(SaveXxxAsync(), "ratings")`

**Estimated Changes:** ~30 line modifications

---

### Issue #2: Race Condition in Concurrent File Writes
**File:** `Data/RatingsRepository.cs`

**Problem:** Multiple fire-and-forget saves can execute concurrently for the same file, causing data corruption.

**Fix Strategy:**
1. Add `SemaphoreSlim` per data file to serialize writes:
```csharp
private static readonly SemaphoreSlim _ratingsWriteLock = new(1, 1);
private static readonly SemaphoreSlim _requestsWriteLock = new(1, 1);
private static readonly SemaphoreSlim _chatWriteLock = new(1, 1);
// ... etc for each file type
```
2. Wrap each `SaveXxxAsync()` method body with semaphore:
```csharp
private async Task SaveRatingsAsync()
{
    await _ratingsWriteLock.WaitAsync().ConfigureAwait(false);
    try
    {
        // existing save logic
    }
    finally
    {
        _ratingsWriteLock.Release();
    }
}
```

**Estimated Changes:** ~15 methods modified, ~60 lines added

---

## HIGH SEVERITY

### Issue #3: Stored XSS - No Sanitization on Media Requests
**File:** `Api/RatingsController.cs` (lines 918-931)

**Problem:** Title, Notes, Type, CustomFields, ImdbCode stored without sanitization.

**Fix Strategy:**
1. Create shared sanitization method (or reuse from ChatController):
```csharp
private static string SanitizeInput(string? input, int maxLength = 500)
{
    if (string.IsNullOrEmpty(input)) return string.Empty;
    // Strip HTML tags
    var sanitized = Regex.Replace(input, @"<[^>]*>", string.Empty);
    // HTML encode special characters
    sanitized = System.Web.HttpUtility.HtmlEncode(sanitized);
    // Limit length
    return sanitized.Length > maxLength ? sanitized.Substring(0, maxLength) : sanitized;
}
```
2. Apply to all fields before storage in CreateMediaRequest and UpdateMediaRequest

**Estimated Changes:** ~20 lines

---

### Issue #4: No Input Length Limits on Media Requests
**File:** `Models/MediaRequestDto.cs`

**Status:** ALREADY FIXED - MediaRequestDto has [MaxLength] attributes:
- Title: 500
- Type: 100
- Notes: 2000
- CustomFields: 5000
- ImdbCode: 50
- ImdbLink: 500

**Action:** Verify server-side enforcement with model validation.

---

### Issue #5: GIF URL Stored Without HTML Sanitization
**File:** `Api/ChatController.cs` (lines 570-573)

**Problem:** GIF URL passes domain validation but could contain encoded XSS payloads.

**Fix Strategy:**
1. HTML-encode the GIF URL before storage:
```csharp
GifUrl = !string.IsNullOrEmpty(dto.GifUrl) && IsValidGifUrl(dto.GifUrl)
    ? System.Web.HttpUtility.HtmlEncode(dto.GifUrl)
    : null,
```

**Estimated Changes:** ~5 lines

---

### Issue #6: IDOR - Any User Can View Any User's Ratings
**File:** `Api/RatingsController.cs` (lines 254-301)

**Problem:** `/Ratings/Users/{userId}/Ratings` allows any user to view any other user's ratings.

**Fix Strategy (Design Decision Needed):**
- **Option A:** Restrict to own user only (privacy-focused)
- **Option B:** Make other users' ratings admin-only
- **Option C:** Keep as-is (social feature - user previously indicated this is intentional)

**Recommendation:** Add config flag `PublicRatings` to control this behavior:
```csharp
if (targetUserId != authUserId)
{
    var config = Plugin.Instance?.Configuration;
    if (config?.PublicRatings != true && !IsJellyfinAdmin(authUserId))
        return Forbid("Viewing other users' ratings is disabled");
}
```

**Estimated Changes:** ~10 lines

---

### Issue #7: Sensitive Endpoints Use AllowAnonymous
**Files:** Both controllers

**Problem:** Endpoints use [AllowAnonymous] with manual auth checks - risky if check is forgotten.

**Fix Strategy:**
1. Replace [AllowAnonymous] with [Authorize] on these endpoints:
   - `/Chat/Messages` (GET/POST)
   - `/Chat/Ban`
   - `/Chat/Moderators`
   - `/Chat/Users/All`
   - `/Chat/Messages/Clear`
   - All DM endpoints
2. Keep [AllowAnonymous] only for truly public endpoints:
   - `/Ratings/Config` (but limit fields - see #13)
   - `/Ratings/test` (but remove version - see #12)

**Note:** Manual token fallback can remain as secondary mechanism.

**Estimated Changes:** ~20 attribute changes

---

### Issue #8: Unbounded Collection Growth
**File:** `Data/RatingsRepository.cs`

**Problem:** No limits on ratings, requests, bans, chat users - memory/disk grows indefinitely.

**Fix Strategy:**
1. Add periodic cleanup method called on plugin startup and daily timer:
```csharp
public async Task CleanupExpiredDataAsync()
{
    // Remove expired bans
    var now = DateTime.UtcNow;
    _userBans.RemoveWhere(b => b.ExpiresAt.HasValue && b.ExpiresAt < now);
    _chatBans.RemoveWhere(b => b.ExpiresAt.HasValue && b.ExpiresAt < now);

    // Remove inactive chat users (not seen in 30 days)
    var cutoff = now.AddDays(-30);
    _chatUsers.RemoveWhere(u => u.LastSeen < cutoff);

    // Archive old fulfilled deletion requests (older than 90 days)
    // ... etc
}
```
2. Add caps with LRU eviction for very large collections
3. Call cleanup on startup and via scheduled task

**Estimated Changes:** ~50 lines

---

## MEDIUM SEVERITY

### Issue #9: HttpClient Without Timeout
**File:** `Api/ChatController.cs` (line 35)

**Fix:**
```csharp
private static readonly HttpClient _httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(10)
};
```

**Estimated Changes:** 1 line

---

### Issue #10: API Key in URL Path (Klipy)
**File:** `Api/ChatController.cs` (line 307)

**Problem:** Klipy API key is in URL path, may be logged.

**Fix Strategy:**
1. Check if Klipy supports header auth (likely not based on their API design)
2. Add URL redaction for Klipy URLs in logging:
```csharp
var redactedUrl = Regex.Replace(klipyUrl, @"/api/v1/[^/]+/", "/api/v1/[REDACTED]/");
_logger.LogDebug("Fetching GIFs from: {Url}", redactedUrl);
```

**Estimated Changes:** ~5 lines

---

### Issue #11: Race Condition in Rate Limiter Cleanup
**File:** `Api/ChatController.cs` (lines 183-210)

**Fix:**
```csharp
private static DateTime _lastRateLimitCleanup = DateTime.UtcNow;
private static readonly object _cleanupLock = new object();

// In cleanup logic:
lock (_cleanupLock)
{
    if ((now - _lastRateLimitCleanup).TotalMinutes >= 5)
    {
        _lastRateLimitCleanup = now;
        // cleanup logic
    }
}
```

**Estimated Changes:** ~10 lines

---

### Issue #12: Version Disclosure on Test Endpoint
**File:** `Api/RatingsController.cs` (lines 451-456)

**Fix:**
```csharp
return Ok(new { message = "Ratings plugin is loaded!" });
// Remove: version = "1.0.8.0"
```

**Estimated Changes:** 1 line

---

### Issue #13: Config Exposed Without Authentication
**File:** `Api/RatingsController.cs` (lines 462-552)

**Fix Strategy:**
1. Create two config response types - public (minimal) and authenticated (full):
```csharp
// For unauthenticated users - only essential UI config
var publicConfig = new {
    EnableChat = config?.EnableChat ?? false,
    HasGifSupport = hasGifSupport,
    ChatAllowEmojis = config?.ChatAllowEmojis ?? true
};

// For authenticated users - full config
```

**Estimated Changes:** ~30 lines

---

### Issue #14: Admin Status Leaked in Chat Messages
**File:** `Api/ChatController.cs` (lines 495-496)

**Fix:**
```csharp
// Only include admin/mod status if requester is admin/mod
var requesterId = await GetCurrentUserIdAsync();
var requesterIsPrivileged = IsJellyfinAdmin(requesterId) || _repository.IsChatModerator(requesterId);

// In message mapping:
{ "isAdmin", requesterIsPrivileged ? IsJellyfinAdmin(m.UserId) : (object?)null },
{ "isModerator", requesterIsPrivileged ? _repository.IsChatModerator(m.UserId) : (object?)null }
```

**Estimated Changes:** ~10 lines

---

### Issue #15: No Upper Bound on Ban Duration
**File:** `Api/ChatController.cs` (line 826)

**Fix:**
```csharp
// Max ban duration: 1 year (525600 minutes)
var sanitizedDuration = Math.Min(durationMinutes.Value, 525600);
```

**Estimated Changes:** 1 line

---

## LOW SEVERITY

### Issue #16: Regex-based XSS Sanitization is Fragile
**File:** `Api/ChatController.cs` (lines 122-148)

**Fix Strategy:**
1. Add HTML encoding as defense-in-depth after regex stripping:
```csharp
// After regex sanitization, also HTML encode
sanitized = System.Web.HttpUtility.HtmlEncode(sanitized);
```
2. Consider adding HtmlSanitizer NuGet package for robust sanitization (optional)

**Estimated Changes:** ~5 lines

---

### Issue #17: DetailedRatings Exposes Usernames Without Auth
**File:** `Api/RatingsController.cs` (lines 413-445)

**Status:** This is related to Issue #6 (IDOR). Fix with same config flag approach.

---

### Issue #18: DoS via GetMediaItems Loading All Items
**File:** `Api/RatingsController.cs` (lines 1529-1825)

**Fix Strategy:**
1. Use Jellyfin's built-in pagination in GetItemList:
```csharp
var query = new InternalItemsQuery(user)
{
    StartIndex = (page - 1) * pageSize,
    Limit = pageSize,
    // ... other filters
};
```

**Estimated Changes:** ~15 lines

---

### Issue #19: Hardcoded Claim Name with No Fallback Warning
**File:** `Api/RatingsController.cs` (line 2621)

**Fix:**
```csharp
public static Guid GetUserId(this ClaimsPrincipal principal)
{
    var userId = principal.FindFirst("Jellyfin.UserId")?.Value;
    if (string.IsNullOrEmpty(userId))
    {
        // Could log warning here if needed for debugging
        return Guid.Empty;
    }
    return Guid.TryParse(userId, out var id) ? id : Guid.Empty;
}
```

**Status:** Already fixed with TryParse in v2.0.11.0

---

### Issue #20: Index.html Modification Without File Locking
**Files:** `JavaScriptInjectionService.cs`, `StartupService.cs`

**Fix Strategy:**
1. Use FileStream with exclusive access:
```csharp
using var fileStream = new FileStream(indexPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
using var reader = new StreamReader(fileStream);
var content = await reader.ReadToEndAsync();
// modify content
fileStream.SetLength(0);
using var writer = new StreamWriter(fileStream);
await writer.WriteAsync(modifiedContent);
```

**Estimated Changes:** ~20 lines per file

---

## Implementation Order

### Phase 1: Critical (Do First)
1. Issue #1 - Fire-and-forget saves (add error logging)
2. Issue #2 - Race condition in file writes (add semaphores)

### Phase 2: High Priority Security
3. Issue #3 - Sanitize media request fields
4. Issue #5 - HTML-encode GIF URLs
5. Issue #7 - Replace AllowAnonymous with Authorize

### Phase 3: Data Protection
6. Issue #8 - Add cleanup for unbounded collections
7. Issue #6 - Add config for public ratings (if not intentional)

### Phase 4: Medium Priority
8. Issue #9 - HttpClient timeout
9. Issue #10 - Redact Klipy API key in logs
10. Issue #11 - Rate limiter race condition
11. Issue #12 - Remove version from test endpoint
12. Issue #13 - Split config endpoint (public/authenticated)
13. Issue #14 - Hide admin status from non-admins
14. Issue #15 - Cap ban duration

### Phase 5: Low Priority
15. Issue #16 - Add HTML encoding to sanitization
16. Issue #18 - Server-side pagination for GetMediaItems
17. Issue #20 - File locking for index.html

---

## Estimated Total Effort
- **Critical fixes:** ~100 lines changed
- **High priority:** ~60 lines changed
- **Medium priority:** ~70 lines changed
- **Low priority:** ~50 lines changed
- **Total:** ~280 lines changed across 4-5 files

## Testing Required
- [ ] Verify saves don't lose data under concurrent load
- [ ] Test XSS payloads are sanitized
- [ ] Verify auth works after removing AllowAnonymous
- [ ] Test cleanup doesn't delete active data
- [ ] Load test GetMediaItems with large library
- [ ] Verify GIF search still works with timeout
