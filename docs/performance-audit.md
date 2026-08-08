# Ratings Plugin — Performance & Quality Audit

Audit of `jellyfin-plugin-ratings` v1.0.359.0 against Jellyfin 10.11 server internals.
Every number below was measured from this working tree, not estimated.

---

## Measured baseline

| Artifact | Size |
|---|---|
| `Web/ratings.js` (source) | 1,687,932 B |
| `obj/Release/net9.0/ratings.min.js` (what actually ships) | 1,030,715 B |
| &nbsp;&nbsp;└ CSS template literal, **unminified** | 373,752 B (36%) |
| &nbsp;&nbsp;└ translations block (16 languages), **all shipped** | ~220,000 B (21%) |
| &nbsp;&nbsp;└ actual executable code | ~437,000 B (43%) |
| `Jellyfin.Plugin.Ratings.dll` in release zip | 2,145 KB (483 KB compressed) |
| Loose `.zip` build artifacts committed at repo root | 84 files, 38.2 MB |
| `.git` | 102 MB |

**58% of the JavaScript bundle every user downloads and parses is content that
is neither minified nor needed on the page being viewed.**

---

## A. Bundle & first load — biggest wins

### A1. 374 KB of CSS ships as an unminified string inside the JS bundle
`Web/ratings.js:4424-14466` — `injectStyles()` holds a 10,042-line template literal
which the plugin turns into a `<style>` tag at runtime.

esbuild **cannot** minify template-literal contents — it copies them byte for byte,
indentation and all. I confirmed this in the built artifact: the literal occupies
bytes 223,617–597,369 of `ratings.min.js`, still fully indented.

Cost per page load: 374 KB of transfer, a JS string allocation, and a synchronous
CSSOM parse on the main thread *after* the 1 MB script has already been parsed.

**Fix:** move it to `Web/ratings.css`, add a `MinifyRatingsCss` target (`esbuild
--loader=css --minify`), serve it from a `[HttpGet("ratings.css")]` action next to
the existing `ratings.js` action, and have `ScriptInjectionMiddleware` emit a
`<link rel="stylesheet">` alongside the `<script>`. That gives you:
- ~35–45% smaller from CSS minification alone,
- the browser's *preload scanner* fetches the CSS in parallel with the JS instead of after it,
- CSSOM parsing off the JS critical path,
- independent caching (a JS-only change stops re-downloading the CSS and vice versa).

### A2. All 16 languages ship to every user
`Web/ratings.js:79-1698` — ~220 KB of translations, of which one language is ever used.
`init()` (line 1933) reads the active language from `localStorage` immediately, so the
choice is known before anything is rendered.

**Fix:** keep `en` inline as the fallback (~14 KB) and serve the rest as
`/Ratings/i18n/{lang}.json`, fetched once and cached in `localStorage`. Saves ~205 KB.

**A1 + A2 together take the bundle from ~1.03 MB to roughly ~460 KB** — a >2× cut with
no behaviour change.

### A3. Compression is not guaranteed, and it's re-done on every cold load
Jellyfin's `Jellyfin.Server/Startup.cs` calls `services.AddResponseCompression()` with
**no options**. Two consequences:
- `ResponseCompressionOptions.EnableForHttps` defaults to **`false`**. Any user hitting
  Jellyfin over direct HTTPS (no TLS-terminating proxy) downloads the full **1 MB
  uncompressed**. You cannot change this from a plugin.
- Where it does apply, gzip runs over 1 MB on every cache miss, on the request thread.

**Fix:** pre-compress at build time and serve the bytes directly. Store Brotli and gzip
copies as embedded resources, pick one from `Accept-Encoding`, set
`Content-Encoding` + `Vary: Accept-Encoding` yourself. This works over HTTPS regardless
of the server's setting, and costs zero CPU per request.

### A4. The bundle is re-encoded to UTF-8 on every request
`Api/RatingsController.cs:2080` — `Content(_cachedScript, "application/javascript")`
caches the 1 MB **string** but `ContentResult` re-encodes it to bytes each time
(~1 MB of allocation per cache-miss request).

**Fix:** cache `byte[]` instead and return `File(bytes, "application/javascript")`.
Combine with A3 so the cached bytes are already compressed.

### A5. `init()` starts every subsystem regardless of configuration
`Web/ratings.js:1923-2009` — 25 subsystems start unconditionally: chat, friends,
Netflix view, media management, notifications, deletion badges, Editor's Choice
compatibility. Each spawns its own observers, retry timers and polls. The
`EnableChat` / `EnableFriendsButton` / `EnableNetflixView` config flags are consulted
*later*, per subsystem, after the machinery already exists.

**Fix:** `await getConfig()` once at the top of `init()`, then start only what's enabled.
On a server with chat and social off this eliminates a 2 s poll, a 10 s poll, a 30 s
heartbeat, a WebSocket, and several full-body `MutationObserver`s outright.

---

## B. Client runtime — the "smoothness" problems

### B1. Self-retriggering full-document scan on every DOM mutation ← worst jank source
`Web/ratings.js:19596-19612`:

```js
const mutationObserver = new MutationObserver(() => {
    const cards = document.querySelectorAll(cardSelector);   // whole document
    cards.forEach(card => {
        if (!card.dataset.ratingsObserved) {
            card.dataset.ratingsObserved = 'true';           // ← this is a mutation
            intersectionObserver.observe(card);
        }
    });
});
mutationObserver.observe(document.body, { childList: true, subtree: true });
```

Three compounding problems:
1. The callback is **undebounced** and re-queries the *entire document* on every batch.
   Jellyfin's web client mutates the DOM constantly — image loads, hover, scroll, tab switches.
2. Writing `dataset.ratingsObserved` is itself a DOM mutation, so the observer feeds itself.
3. `cardSelector` includes `.card:not(.card .card)` — a descendant-negation selector the
   engine must evaluate against every ancestor chain, run over every element in the document.

**Fix:** debounce through `requestIdleCallback` (fallback `requestAnimationFrame`), only
walk `mutation.addedNodes` rather than the whole document, and track seen cards in a
`WeakSet` instead of a `data-` attribute so the observer stops feeding itself.
This alone should visibly smooth library scrolling.

### B2. Timers that never stop
Full inventory of `setInterval` in the bundle:

| Period | Location | What it does |
|---|---|---|
| 100 ms | `17589`, `21151` | element-existence polling |
| 300 ms | `23776` | URL change polling |
| 500 ms | `2317`, `14542`, `20330`, `27553`, `27836`, `30401` | URL/SPA-navigation polling |
| 1 s | `19890`, `20271`, `20482`, `21074`, `21224`, `22099`, `30214` | DOM re-scans |
| 2 s | `19997`, `23571`, `27836`, `28582`, `30072` | library-page checks, **chat poll** |
| 5 s / 10 s / 30 s / 3 min | `33559`, `27280`/`30331`, `19817`/`30401`, `21151` | badges, notifications, heartbeat |

There are **28 intervals**, and only 5 `visibilitychange` handlers in the whole file
(`4401`, `20297`, `21099`, `21250`, `22124`) — the chat poll, notification poll, heartbeat
and every DOM re-scan keep running in background tabs. On a phone this is a battery drain;
with several tabs open it multiplies server load.

**Fixes, in order of value:**
1. Replace the 100/300/500 ms URL pollers with **one** navigation listener. Jellyfin fires
   `viewshow`/`pageshow` events and supports `window.addEventListener('hashchange')`.
   One shared `onNavigate` dispatcher can replace ~8 separate intervals.
2. Gate every remaining interval on `!document.hidden`, and re-sync once on
   `visibilitychange`. One shared visibility guard, not 28 separate checks.
3. Replace "does this element exist yet" polls (`17589`, `21151`, the `*WithRetry`
   initialisers) with a single scoped `MutationObserver` that disconnects on first hit —
   the pattern already used correctly at `27805`.

### B3. Chat re-downloads and re-renders everything every 2 seconds
- `Web/ratings.js:30487` — `GET /Ratings/Chat/Messages?limit=50`. The server supports a
  `since` parameter (`Api/ChatController.cs:707`) and **the client never sends it**.
- `renderChatMessages()` (`30565`) rebuilds the whole list as an HTML string each tick.
- `loadOnlineUsers()` fires on the same 2 s tick as a second request.

So: 2 requests/sec/open-client, ~50 messages of JSON each, full list re-render each time.

**Fix (small):** send `since=<lastTimestamp>`, append only new messages, and drop the
online-user refresh to 15 s. Cuts chat traffic by well over 90% at idle.

**Fix (right):** you already run `SocialWebSocketListener` (`Api/SocialWebSocketListener.cs`,
registered at `PluginServiceRegistrator.cs:26-27`) and the client already connects to it
(`ratings.js:3223`). Push chat messages over that socket and delete the polling entirely.
This is the single largest reduction in steady-state server load available.

### B4. Unthrottled resize handler
`Web/ratings.js:1988` — `window.addEventListener('resize', () => self.applyBadgeProfile())`,
where `applyBadgeProfile` (`1810`) walks and restyles badges. Fires on every resize frame.

**Fix:** `requestAnimationFrame` coalescing, or a 150 ms trailing debounce.

### B5. 258 `innerHTML` assignments
Each is a parse + full subtree replacement. The chat message list and friends panel are
rebuilt wholesale on every refresh, which also destroys text selection and scroll anchoring.
Worth converting at least those two hot lists to incremental updates.

---

## C. Server hot paths

### C1. `GET /Ratings/Media` loads the entire library to return one page
`Api/RatingsController.cs:2847` — `_libraryManager.GetItemList(query)` with `Recursive = true`
and **no `Limit`/`StartIndex`**. Pagination happens in memory at line 2976, *after*:

- `_repository.GetRatingStats(item.Id)` for **every** item (line ~2864) — each one takes the
  global repository lock;
- when sorting by size: `GetMediaSources(false)` for **every** movie (line 2898) — file I/O per item;
- when sorting by play count: a **separate `GetItemList` episode query per series** plus
  `GetUserData` per episode (lines 2925-2939) — a textbook N+1.

On a 20,000-item library, rendering a 50-row admin page walks 20,000 items, takes the
repository lock 20,000 times, and can issue thousands of extra queries.

**Fix:** push paging into `InternalItemsQuery` (`Limit`, `StartIndex`, `OrderBy`) so Jellyfin's
SQLite layer does the work, then compute stats for the ≤200 returned rows only. For
`size`/`playcount` sorts — which genuinely need whole-library data — precompute into a
cache refreshed by a scheduled task rather than on the request path.

### C2. `PlaybackBlockingMiddleware` runs on every request in the server
Registered via `IStartupFilter` (`PlaybackBlockingStartupFilter`), which places it at the very
**front** of the pipeline — ahead of `UseForwardedHeaders`, `UseResponseCompression`,
`UseStaticFiles` and routing. It therefore sees every image request, every API call and
every HLS segment.

- `PlaybackBlockingMiddleware.cs:56` — `path.Value?.ToLowerInvariant()` allocates a new
  string **per request, server-wide**. Use `Contains(x, StringComparison.OrdinalIgnoreCase)`
  and allocate nothing.
- `PlaybackBlockingMiddleware.cs:153` — `IsPlaybackStartRequest` matches any GET whose path
  contains `/stream` or `main.m3u8`. During HLS playback the client refreshes the playlist
  every few seconds, and **each refresh calls `IncrementMediaUsageAsync`**, which
  (`RatingsRepository.cs:3760`) serialises and rewrites the whole `media_quotas.json`.
  A single stream can rewrite that file hundreds of times per hour.
- Every matched request may call `_sessionManager.GetSessionByAuthenticationToken` (line 198),
  a DB lookup, with no memoisation.

**Fixes:** ordinal comparisons; count a playback *start* once per session (not per playlist
refresh) — e.g. only on `POST /PlaybackInfo`; and add a short-lived
`ConcurrentDictionary<token, (Guid userId, DateTime expiry)>` cache in front of the session
lookup.

### C3. `GET /Ratings/Chat/Messages` does 100 user lookups per poll
`Api/ChatController.cs:736-737` — for each of 50 messages it calls `IsJellyfinAdmin(m.UserId)`
(which hits `_userManager.GetUserById`, line 153) and `_repository.IsChatModerator(m.UserId)`.
100 lookups × every client × every 2 seconds.

**Fix:** the distinct sender count per page is tiny. Build a
`Dictionary<Guid,(bool admin, bool mod)>` from `messages.Select(m => m.UserId).Distinct()`
once, then index it. ~100 lookups → ~5.

### C4. Rating stats make 10 passes where 1 would do
`Data/RatingsRepository.cs:666-669` and `711-714`:
```csharp
for (int i = 1; i <= 10; i++)
    stats.Distribution[i-1] = itemRatings.Count(r => r.Rating == i);
```
Ten full enumerations per item. In `GetBatchRatingStats` that's ×100 items per card-batch request.
Also `GetItemRatingsInternal:528` does `itemList.ToList()` — a defensive copy per item, 100 copies
per batch call.

**Fix:** single pass incrementing `Distribution[r.Rating-1]`, and accumulate sum/count/user-rating
in the same loop. Iterate the indexed list directly instead of copying (you already hold the lock).

### C5. `ScriptInjectionMiddleware` disables compression for `index.html`
`ScriptInjectionMiddleware.cs:50` — `context.Request.Headers.Remove("Accept-Encoding")` runs for
*every* `index.html` request, so Jellyfin's shell HTML is always served uncompressed and buffered
through a `MemoryStream`.

Note also that `JavaScriptInjectionService` (line 227) already writes the tag into `index.html` on
disk when permissions allow — in that case the middleware buffers the whole response only to
discover the tag is present (line 105) and pass it through unchanged.

**Fix:** re-compress the modified HTML yourself before writing, or gate the middleware off
(a `static volatile bool`) once `JavaScriptInjectionService` confirms the on-disk injection worked.

---

## D. Data layer — the structural issue

Both repositories are **in-memory collections persisted as whole-file JSON rewrites**. There is
no database: `grep -c "CREATE TABLE" Data/` returns 0. Every mutation reserialises and rewrites
an entire file.

### D1. Writes happen synchronously inside the global lock
The pattern repeats 51 times in `RatingsRepository`:
```csharp
lock (_lock) {
    _chatMessages.Add(message);
    _ = SaveChatMessagesAsync();     // fire-and-forget, started inside the lock
}
```
`SaveChatMessagesAsync` (`2429`) takes an uncontended `SemaphoreSlim` — which completes
**synchronously** — then re-enters `lock (_lock)` (reentrant on the same thread) and runs
`JsonSerializer.Serialize(...)` **inline, while the global lock is held**. Only the final
`File.WriteAllTextAsync` actually yields.

So sending one chat message serialises all 1,000 retained messages (`WriteIndented = true`,
so several hundred KB) on the request thread, blocking every other reader of the repository.

**Fix:** move serialisation off the lock — snapshot under the lock, serialise and write outside it.

### D2. `WriteIndented = true` on every hot file
`ratings.json`, `chat_messages.json`, `private_messages.json`, `media_quotas.json` and others are
all written pretty-printed. That's roughly 30–40% more bytes to serialise and write, for files no
human reads.

**Fix:** `WriteIndented = false` everywhere except explicit backup export. Reuse one **static**
`JsonSerializerOptions` instance too — a fresh `JsonSerializerOptions` per call defeats
System.Text.Json's metadata cache and is a documented significant slowdown.

### D3. No write coalescing
A burst of 50 ratings triggers 50 full rewrites of `ratings.json`.

**Fix:** mark dirty and flush on a debounce (e.g. 2 s trailing, plus a flush on
`IHostApplicationLifetime.ApplicationStopping`).

### D4. Writes are not atomic
`File.WriteAllTextAsync` truncates in place. A crash or power loss mid-write leaves a truncated
JSON file, and the loader (`LoadRatings`, line 180) will fail to parse it and start from empty —
silent total data loss.

**Fix:** write to `<file>.tmp`, `File.Move(tmp, file, overwrite: true)`. Cheap, and it removes a
real data-loss risk.

### D5. Blocking file I/O in a singleton constructor
`RatingsRepository.cs:120-138` runs `Parallel.Invoke` over 19 synchronous loaders in the
constructor. DI resolves this lazily on first use, so the first request that touches the
repository blocks on reading all 19 files.

**Fix:** move loading into an `IHostedService.StartAsync`, or expose an
`await EnsureLoadedAsync()` guard.

### D6. Linear scans still present despite the secondary indexes
The indexes at `RatingsRepository.cs:47-50` are good, but the write path ignores them —
`SetRatingAsync` (`385-400`) does up to four `_ratings.Values.FirstOrDefault(...)` full scans,
and `DeleteRatingAsync:740` does another. `GetActiveChatBan:2973` scans all bans on every
playback request (see C2).

**Fix:** add a `Dictionary<(Guid user, Guid item), UserRating>` index and use the existing
provider-ID indexes on the write path too.

### D7. Worth considering: move to SQLite
Jellyfin already ships `Microsoft.Data.Sqlite`. For chat, private messages, ratings and
notifications, a single SQLite file with proper indexes removes D1–D4 and D6 as a category, and
makes `GetRecentChatMessages(since)` an indexed range scan instead of a sort over 1,000 in-memory
objects (`RatingsRepository.cs:2484`). This is the largest change here — but it's the one that
stops these problems recurring.

---

## E. Build & repo

### E1. Minification fails silently
`Jellyfin.Plugin.Ratings.csproj:36-42` shells out to `npx esbuild`, with `ContinueOnError="true"`
and a fallback that copies the unminified file.

**Node is not installed on this machine.** A release built here right now would silently embed
the full 1.69 MB source — a 64% bundle regression behind a single MSBuild warning.

**Fix:** make the fallback loud. Add a `PublishRelease` property that turns the missing-esbuild
warning into an error, so a release build cannot ship unminified.

### E2. No CI
There is no `.github/workflows/` directory. Every release is built from a developer machine, which
is exactly how E1 goes unnoticed.

**Fix:** a GitHub Actions workflow with `setup-node` + `setup-dotnet` that builds, asserts
`ratings.min.js` is materially smaller than the source, and attaches the zip. It also removes the
"which machine has node" question permanently.

### E3. Repo bloat
84 build `.zip` files (38.2 MB) are committed at the repo root, plus more in `release/`. The
working tree is 210 MB and `.git` is 102 MB. Every clone pays for this.

**Fix:** `git rm --cached *.zip release/*.zip`, add `*.zip` to `.gitignore`, and publish builds as
GitHub Release assets. (Rewriting history to reclaim the 102 MB is optional and disruptive — the
`.gitignore` change is the part worth doing.)

### E4. `GenerateDocumentationFile` for a plugin
`csproj:7` produces a 205 KB XML doc file. It isn't in the shipped zip (I verified: the zip
contains only the DLL and `meta.json`), so this is build-time cost only — but with
`AnalysisMode=AllEnabledByDefault` and `TreatWarningsAsErrors=false` you're paying for analysis
whose output nothing enforces. Either enforce it or turn it down.

---

## Recommended order

**Do first — high impact, low risk, no behaviour change**

| # | Change | Effect |
|---|---|---|
| A1 | Extract CSS to a separate minified stylesheet | −200 KB, off JS critical path |
| A2 | Split translations per language | −205 KB |
| B1 | Debounce card observer, `WeakSet` instead of `data-` attribute | removes the main scroll jank |
| C4 | Single-pass distribution, drop the per-item `ToList()` | ~10× faster batch stats |
| D2 | `WriteIndented = false`, static `JsonSerializerOptions` | 30–40% less write work everywhere |
| D4 | Atomic temp-file writes | closes a data-loss hole |
| E1 | Fail the release build if minification didn't run | prevents a silent 64% regression |

**Do next — meaningful, needs testing**

| # | Change | Effect |
|---|---|---|
| B3 | `since=` on chat polling, or move chat to the existing WebSocket | >90% less chat traffic |
| B2 | One navigation dispatcher; gate intervals on `document.hidden` | replaces ~8 pollers, saves battery |
| C1 | Real pagination in `GET /Ratings/Media` | admin page goes from seconds to instant |
| C2 | Ordinal comparisons; count playback starts once per session | removes per-request allocation + quota-file thrash |
| C3 | Deduplicate the admin/moderator lookups | 100 lookups → ~5 per poll |
| D1 | Serialise outside the global lock | unblocks readers during writes |
| A5 | Config-gate `init()` | disabled features cost nothing |

**Larger projects**

| # | Change |
|---|---|
| A3/A4 | Pre-compressed Brotli/gzip byte arrays (works over HTTPS regardless of server config) |
| D3 | Debounced write coalescing |
| D7 | SQLite for chat / messages / ratings |
| E2 | CI pipeline |

---

## Notes on Jellyfin server internals used here

- `Jellyfin.Server/Startup.cs` calls `services.AddResponseCompression()` with no options, and
  `UseResponseCompression()` sits after `UseWebSockets()` and before `UseStaticFiles()`/`UseRouting()`.
  Default MIME types include `application/javascript`, so the bundle *is* compressed — but
  `EnableForHttps` defaults to `false`, so direct-HTTPS clients get none. A plugin cannot change this.
- `IStartupFilter`-registered middleware wraps the *outside* of the pipeline, ahead of Jellyfin's own
  middleware. Both of this plugin's middlewares therefore run before compression, forwarded headers
  and routing — which is why C2's per-request cost matters server-wide.
- `IWebSocketListener` is already wired up (`PluginServiceRegistrator.cs:26-27`), so B3's push-based
  chat needs no new infrastructure.
