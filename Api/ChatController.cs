using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.Ratings.Data;
using Jellyfin.Plugin.Ratings.Models;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// Chat API controller.
    /// </summary>
    [ApiController]
    [Route("Ratings/Chat")]
    [Produces(MediaTypeNames.Application.Json)]
    public class ChatController : ControllerBase
    {
        private readonly RatingsRepository _repository;
        private readonly IUserManager _userManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<ChatController> _logger;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Rate limiting with ConcurrentDictionary for thread safety
        private static readonly ConcurrentDictionary<Guid, (DateTime ResetTime, int Count)> _rateLimits = new();
        private static DateTime _lastRateLimitCleanup = DateTime.UtcNow;
        private static readonly object _cleanupLock = new object();

        /// <summary>
        /// Allowed GIF domains for security (exact match or subdomain).
        /// </summary>
        private static readonly string[] AllowedGifDomains = new[]
        {
            "tenor.com",
            "giphy.com",
            "klipy.com"
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatController"/> class.
        /// </summary>
        public ChatController(
            RatingsRepository repository,
            IUserManager userManager,
            ISessionManager sessionManager,
            ILogger<ChatController> logger)
        {
            _repository = repository;
            _userManager = userManager;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current user ID from the request.
        /// </summary>
        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userId = User.GetUserId();
            if (userId != Guid.Empty) return userId;

            var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                          ?? Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader)) return Guid.Empty;

            var tokenMatch = Regex.Match(authHeader, @"Token=""([^""]+)""");
            if (!tokenMatch.Success) return Guid.Empty;

            var token = tokenMatch.Groups[1].Value;
            try
            {
                var session = await _sessionManager.GetSessionByAuthenticationToken(token, null, null).ConfigureAwait(false);
                return session?.UserId ?? Guid.Empty;
            }
            catch
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Checks if a user is a Jellyfin administrator (server-side check).
        /// </summary>
        private bool IsJellyfinAdmin(Guid userId)
        {
            if (userId == Guid.Empty) return false;
            try
            {
                var user = _userManager.GetUserById(userId);
                return user != null && user.HasPermission(PermissionKind.IsAdministrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if user can moderate (Jellyfin admin or chat moderator).
        /// </summary>
        private bool CanModerate(Guid userId)
        {
            if (IsJellyfinAdmin(userId)) return true;
            return _repository.IsChatModerator(userId);
        }

        /// <summary>
        /// Gets the moderator level for a user.
        /// Admin = int.MaxValue, otherwise the moderator level or 0.
        /// </summary>
        private int GetModeratorLevel(Guid userId)
        {
            if (IsJellyfinAdmin(userId)) return int.MaxValue;
            var mod = _repository.GetChatModeratorByUserId(userId);
            return mod?.Level ?? 0;
        }

        /// <summary>
        /// Checks if a moderator can perform an action on a target user.
        /// Returns false if trying to target an equal/higher level user.
        /// </summary>
        private bool CanTargetUser(Guid moderatorId, Guid targetId)
        {
            // Admins cannot be targeted by anyone except other admins
            if (IsJellyfinAdmin(targetId) && !IsJellyfinAdmin(moderatorId))
            {
                return false;
            }

            var modLevel = GetModeratorLevel(moderatorId);
            var targetLevel = GetModeratorLevel(targetId);

            // Cannot target equal or higher level users
            return modLevel > targetLevel;
        }

        /// <summary>
        /// Checks if moderator has reached their daily delete limit.
        /// Returns false if limit not reached (can still delete).
        /// </summary>
        private bool IsDeleteLimitReached(Guid moderatorId)
        {
            // Admins have no limit
            if (IsJellyfinAdmin(moderatorId)) return false;

            var mod = _repository.GetChatModeratorByUserId(moderatorId);
            if (mod == null) return true;

            // Reset count if needed
            _repository.ResetModeratorDailyDeleteCount(mod.Id);

            var config = Plugin.Instance?.Configuration;
            var limit = mod.Level == 1 ? (config?.ModLevel1DeleteLimit ?? 20) : (config?.ModLevel2DeleteLimit ?? 50);

            return mod.DailyDeleteCount >= limit;
        }

        /// <summary>
        /// Validates a hex color string.
        /// </summary>
        private static bool IsValidHexColor(string? color)
        {
            if (string.IsNullOrEmpty(color)) return true; // null/empty is valid (no color)
            return Regex.IsMatch(color, @"^#[0-9A-Fa-f]{6}$");
        }

        /// <summary>
        /// Validates a text style string.
        /// </summary>
        private static bool IsValidTextStyle(string? style)
        {
            if (string.IsNullOrEmpty(style)) return true;
            return style == "bold" || style == "italic" || style == "bold-italic";
        }

        /// <summary>
        /// Logs a moderator action.
        /// </summary>
        private async Task LogModeratorActionAsync(Guid moderatorId, string actionType, Guid targetId, string? details = null)
        {
            var mod = _repository.GetChatModeratorByUserId(moderatorId);
            var modUser = _userManager.GetUserById(moderatorId);
            var targetUser = _userManager.GetUserById(targetId);

            var action = new Models.ModeratorAction
            {
                Id = Guid.NewGuid(),
                ModeratorId = moderatorId,
                ModeratorName = modUser?.Username ?? "Admin",
                ModeratorLevel = mod?.Level ?? (IsJellyfinAdmin(moderatorId) ? 99 : 0),
                ActionType = actionType,
                TargetUserId = targetId,
                TargetUserName = targetUser?.Username ?? "Unknown",
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            await _repository.AddModeratorActionAsync(action).ConfigureAwait(false);
        }

        // Moderator action rate limiting
        private static readonly ConcurrentDictionary<Guid, (DateTime ResetTime, int Count)> _modActionRateLimits = new();

        /// <summary>
        /// Checks moderator action rate limit.
        /// </summary>
        private static bool IsModeratorActionRateLimited(Guid moderatorId, int maxPerMinute)
        {
            var now = DateTime.UtcNow;

            var entry = _modActionRateLimits.AddOrUpdate(
                moderatorId,
                _ => (now, 1),
                (_, existing) =>
                {
                    if ((now - existing.ResetTime).TotalMinutes >= 1)
                        return (now, 1);
                    return (existing.ResetTime, existing.Count + 1);
                });

            return entry.Count > maxPerMinute;
        }

        /// <summary>
        /// Sanitizes message content to prevent XSS.
        /// </summary>
        private static string SanitizeMessage(string? input, int maxLength = 500)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Trim and limit length
            input = input.Trim();
            if (input.Length > maxLength) input = input.Substring(0, maxLength);

            // Strip all HTML tags (handles malformed tags too)
            input = Regex.Replace(input, @"<[^>]*?>", "", RegexOptions.None);
            // Also strip incomplete tags at end
            input = Regex.Replace(input, @"<[^>]*$", "", RegexOptions.None);

            // Recursively remove javascript: protocol until stable
            string previous;
            do
            {
                previous = input;
                input = Regex.Replace(input, @"j\s*a\s*v\s*a\s*s\s*c\s*r\s*i\s*p\s*t\s*:", "", RegexOptions.IgnoreCase);
            } while (input != previous);

            // Remove event handler attributes
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            // Note: NOT HTML encoding here because client renders with textContent (safe)
            // Adding encoding would cause "&" to display as "&amp;"

            return input;
        }

        /// <summary>
        /// Validates GIF URL is from allowed domains (exact or subdomain match).
        /// Also rejects URLs containing characters that could break HTML attributes.
        /// </summary>
        private static bool IsValidGifUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return true;
            try
            {
                var uri = new Uri(url);

                // Must be HTTPS
                if (uri.Scheme != "https") return false;

                // Reject URLs with characters that could break HTML attributes
                if (url.Contains('"') || url.Contains('\'') || url.Contains('<') || url.Contains('>'))
                    return false;

                // Exact domain or subdomain match (not EndsWith which allows evil-tenor.com)
                return AllowedGifDomains.Any(d =>
                    uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks rate limit for a user. Cleans up stale entries periodically.
        /// </summary>
        private static bool IsRateLimited(Guid userId, int maxPerMinute)
        {
            var now = DateTime.UtcNow;

            // Periodic cleanup of stale entries (every 5 minutes) - thread-safe
            if ((now - _lastRateLimitCleanup).TotalMinutes >= 5)
            {
                lock (_cleanupLock)
                {
                    // Double-check after acquiring lock
                    if ((now - _lastRateLimitCleanup).TotalMinutes >= 5)
                    {
                        _lastRateLimitCleanup = now;
                        foreach (var key in _rateLimits.Keys.ToList())
                        {
                            if (_rateLimits.TryGetValue(key, out var val) && (now - val.ResetTime).TotalMinutes >= 2)
                            {
                                _rateLimits.TryRemove(key, out _);
                            }
                        }
                    }
                }
            }

            var entry = _rateLimits.AddOrUpdate(
                userId,
                _ => (now, 1),
                (_, existing) =>
                {
                    if ((now - existing.ResetTime).TotalMinutes >= 1)
                        return (now, 1);
                    return (existing.ResetTime, existing.Count + 1);
                });

            return entry.Count > maxPerMinute;
        }

        /// <summary>
        /// Gets chat configuration. API keys are NOT exposed to clients.
        /// </summary>
        [HttpGet("Config")]
        [AllowAnonymous]
        public async Task<ActionResult> GetChatConfig()
        {
            var config = Plugin.Instance?.Configuration;
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);

            // Check if GIF search is available (API key configured server-side)
            var hasGifSupport = !string.IsNullOrEmpty(config?.KlipyApiKey) || !string.IsNullOrEmpty(config?.TenorApiKey);

            return Ok(new Dictionary<string, object>
            {
                { "EnableChat", config?.EnableChat ?? false },
                { "ChatAllowGifs", config?.ChatAllowGifs ?? true },
                { "ChatAllowEmojis", config?.ChatAllowEmojis ?? true },
                { "ChatMaxMessageLength", config?.ChatMaxMessageLength ?? 500 },
                { "HasGifSupport", hasGifSupport }
            });
        }

        /// <summary>
        /// Server-side GIF search proxy. API key is never exposed to clients.
        /// Supports both Tenor and Klipy APIs.
        /// </summary>
        [HttpGet("GifSearch")]
        [AllowAnonymous]
        public async Task<ActionResult> SearchGifs([FromQuery] string query, [FromQuery] int limit = 20)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true || config?.ChatAllowGifs != true)
            {
                return BadRequest("GIFs are disabled");
            }

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Rate limit GIF searches
            if (IsRateLimited(userId, 30)) // 30 searches per minute
            {
                return StatusCode(429, "Rate limit exceeded");
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query is required");
            }

            // Sanitize query
            query = query.Trim();
            if (query.Length > 100) query = query.Substring(0, 100);
            limit = Math.Clamp(limit, 1, 50);

            // Determine which API to use
            var klipyKey = config?.KlipyApiKey;
            var tenorKey = config?.TenorApiKey;

            if (string.IsNullOrEmpty(klipyKey) && string.IsNullOrEmpty(tenorKey))
            {
                return BadRequest("GIF API not configured");
            }

            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
                var results = new List<object>();

                // Try Tenor API first if key is available (more reliable)
                if (!string.IsNullOrEmpty(tenorKey))
                {
                    var tenorUrl = $"https://tenor.googleapis.com/v2/search?q={encodedQuery}&key={tenorKey}&limit={limit}&media_filter=gif,tinygif";
                    _logger.LogDebug("Calling Tenor API: {Url}", tenorUrl.Replace(tenorKey, "***"));

                    var response = await _httpClient.GetAsync(tenorUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        results = ParseTenorResponse(content);
                        if (results.Count > 0)
                        {
                            return Ok(new { results });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Tenor API failed with status {Status}", response.StatusCode);
                    }
                }

                // Try Klipy API as fallback (correct URL format: api.klipy.com/api/v1/{key}/gifs/search)
                if (!string.IsNullOrEmpty(klipyKey))
                {
                    var klipyUrl = $"https://api.klipy.com/api/v1/{klipyKey}/gifs/search?q={encodedQuery}&per_page={limit}&customer_id=jellyfin";
                    _logger.LogDebug("Calling Klipy API");

                    var response = await _httpClient.GetAsync(klipyUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        results = ParseKlipyResponse(content);
                        if (results.Count > 0)
                        {
                            return Ok(new { results });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Klipy API failed with status {Status}", response.StatusCode);
                    }
                }

                // Both APIs failed or returned no results
                return Ok(new { results = new List<object>() });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "GIF search HTTP error");
                return StatusCode(502, "GIF search failed");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "GIF search JSON parse error");
                return StatusCode(502, "GIF search failed");
            }
        }

        /// <summary>
        /// Parse Tenor API v2 response.
        /// </summary>
        private List<object> ParseTenorResponse(string content)
        {
            var results = new List<object>();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("results", out var resultsElement) && resultsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultsElement.EnumerateArray())
                {
                    var gifUrl = "";
                    var previewUrl = "";
                    var title = "";

                    // Tenor v2 format: media_formats.gif.url and media_formats.tinygif.url
                    if (item.TryGetProperty("media_formats", out var mediaFormats))
                    {
                        if (mediaFormats.TryGetProperty("gif", out var gif) && gif.TryGetProperty("url", out var gifUrlProp))
                        {
                            gifUrl = gifUrlProp.GetString() ?? "";
                        }
                        if (mediaFormats.TryGetProperty("tinygif", out var tinyGif) && tinyGif.TryGetProperty("url", out var tinyUrlProp))
                        {
                            previewUrl = tinyUrlProp.GetString() ?? "";
                        }
                    }

                    if (item.TryGetProperty("title", out var titleProp))
                    {
                        title = titleProp.GetString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(gifUrl))
                    {
                        results.Add(new
                        {
                            url = gifUrl,
                            preview = string.IsNullOrEmpty(previewUrl) ? gifUrl : previewUrl,
                            title = title
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Parse Klipy API response.
        /// Response format: { data: { data: [ { file: { xs/sm/md/hd: { gif: { url: "..." } } } } ] } }
        /// </summary>
        private List<object> ParseKlipyResponse(string content)
        {
            var results = new List<object>();
            using var doc = JsonDocument.Parse(content);

            // Klipy response: data.data[] contains the GIF items
            if (doc.RootElement.TryGetProperty("data", out var outerData) &&
                outerData.TryGetProperty("data", out var dataElement) &&
                dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataElement.EnumerateArray())
                {
                    var gifUrl = "";
                    var previewUrl = "";

                    // Extract URLs from file.{size}.gif.url structure
                    if (item.TryGetProperty("file", out var file))
                    {
                        // Full size: prefer md (medium) or hd (high definition)
                        if (file.TryGetProperty("md", out var md) &&
                            md.TryGetProperty("gif", out var mdGif) &&
                            mdGif.TryGetProperty("url", out var mdUrl))
                        {
                            gifUrl = mdUrl.GetString() ?? "";
                        }
                        else if (file.TryGetProperty("hd", out var hd) &&
                                 hd.TryGetProperty("gif", out var hdGif) &&
                                 hdGif.TryGetProperty("url", out var hdUrl))
                        {
                            gifUrl = hdUrl.GetString() ?? "";
                        }

                        // Preview: prefer xs (extra small) or sm (small)
                        if (file.TryGetProperty("xs", out var xs) &&
                            xs.TryGetProperty("gif", out var xsGif) &&
                            xsGif.TryGetProperty("url", out var xsUrl))
                        {
                            previewUrl = xsUrl.GetString() ?? "";
                        }
                        else if (file.TryGetProperty("sm", out var sm) &&
                                 sm.TryGetProperty("gif", out var smGif) &&
                                 smGif.TryGetProperty("url", out var smUrl))
                        {
                            previewUrl = smUrl.GetString() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(gifUrl))
                    {
                        results.Add(new
                        {
                            url = gifUrl,
                            preview = string.IsNullOrEmpty(previewUrl) ? gifUrl : previewUrl,
                            title = ""
                        });
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Gets recent chat messages.
        /// </summary>
        [HttpGet("Messages")]
        [AllowAnonymous]
        public async Task<ActionResult> GetMessages([FromQuery] DateTime? since, [FromQuery] int limit = 100)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Check if user is banned from chat
            var chatBan = _repository.GetActiveChatBan(userId, "chat");
            if (chatBan != null)
            {
                return StatusCode(403, new { message = "You are banned from chat", expiresAt = chatBan.ExpiresAt, reason = chatBan.Reason });
            }

            if (limit > 500) limit = 500;
            var messages = _repository.GetRecentChatMessages(limit, since);
            var typingUsers = _repository.GetTypingUsers();

            // Enrich messages - null out content for deleted messages (#13)
            var enrichedMessages = messages.Select(m => new Dictionary<string, object?>
            {
                { "id", m.Id },
                { "userId", m.UserId },
                { "userName", m.UserName },
                { "userAvatar", m.UserAvatar },
                { "content", m.IsDeleted ? null : m.Content },
                { "gifUrl", m.IsDeleted ? null : m.GifUrl },
                { "timestamp", m.Timestamp },
                { "isDeleted", m.IsDeleted },
                { "replyToId", m.ReplyToId },
                { "isAdmin", IsJellyfinAdmin(m.UserId) },
                { "isModerator", _repository.IsChatModerator(m.UserId) }
            }).ToList();

            return Ok(new { messages = enrichedMessages, typingUsers });
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        [HttpPost("Messages")]
        [AllowAnonymous]
        public async Task<ActionResult> SendMessage([FromBody] ChatMessageDto dto)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Check if user is banned
            var chatBan = _repository.GetActiveChatBan(userId, "chat");
            if (chatBan != null)
            {
                return StatusCode(403, new { message = "You are banned from chat", expiresAt = chatBan.ExpiresAt, reason = chatBan.Reason });
            }

            // Check snooze ban (temporary mute)
            var snoozeBan = _repository.GetActiveChatBan(userId, "snooze");
            if (snoozeBan != null)
            {
                return StatusCode(403, new { message = "You are temporarily muted", expiresAt = snoozeBan.ExpiresAt, reason = snoozeBan.Reason });
            }

            // Rate limiting
            var maxPerMinute = config?.ChatRateLimitPerMinute ?? 10;
            if (IsRateLimited(userId, maxPerMinute))
            {
                return StatusCode(429, "Rate limit exceeded. Please wait before sending more messages.");
            }

            // Validate content
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.GifUrl))
            {
                return BadRequest("Message content or GIF is required");
            }

            // Validate GIF URL
            if (!string.IsNullOrEmpty(dto.GifUrl))
            {
                if (config?.ChatAllowGifs != true)
                {
                    return BadRequest("GIFs are disabled");
                }
                if (!IsValidGifUrl(dto.GifUrl))
                {
                    return BadRequest("Invalid GIF URL");
                }
            }

            var user = _userManager.GetUserById(userId);
            if (user == null) return Unauthorized();

            var maxLength = config?.ChatMaxMessageLength ?? 500;
            var sanitizedContent = SanitizeMessage(dto.Content, maxLength);

            // HTML-encode GIF URL to prevent XSS (URL is already validated by IsValidGifUrl)
            var sanitizedGifUrl = !string.IsNullOrEmpty(dto.GifUrl) && IsValidGifUrl(dto.GifUrl)
                ? WebUtility.HtmlEncode(dto.GifUrl)
                : null;

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = user.Username,
                UserAvatar = $"/Users/{userId}/Images/Primary",
                Content = sanitizedContent,
                GifUrl = sanitizedGifUrl,
                Timestamp = DateTime.UtcNow,
                ReplyToId = dto.ReplyToId
            };

            var result = await _repository.AddChatMessageAsync(message).ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// Deletes a chat message (admin/moderator only for others' messages).
        /// </summary>
        [HttpDelete("Messages/{messageId}")]
        [AllowAnonymous]
        public async Task<ActionResult> DeleteMessage([FromRoute] Guid messageId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var message = _repository.GetChatMessageById(messageId);
            if (message == null) return NotFound("Message not found");

            // Users can always delete their own messages
            if (message.UserId == userId)
            {
                var deleted = await _repository.DeleteChatMessageAsync(messageId, userId).ConfigureAwait(false);
                return Ok(new { success = deleted });
            }

            // Moderator deleting another user's message
            if (!CanModerate(userId))
            {
                return Forbid("You can only delete your own messages");
            }

            // Check rate limit for moderator actions
            var actionRateLimit = config?.ModeratorActionRateLimitPerMinute ?? 10;
            if (IsModeratorActionRateLimited(userId, actionRateLimit))
            {
                return StatusCode(429, "Moderator action rate limit exceeded");
            }

            // Check delete limit (Level 1 and 2 have limits)
            if (IsDeleteLimitReached(userId))
            {
                return BadRequest("Daily message delete limit reached");
            }

            // Cannot delete messages from equal/higher level mods
            if (!CanTargetUser(userId, message.UserId))
            {
                return BadRequest("Cannot delete messages from equal or higher level moderators");
            }

            var wasDeleted = await _repository.DeleteChatMessageAsync(messageId, userId).ConfigureAwait(false);
            if (!wasDeleted) return NotFound("Message not found");

            // Increment delete counter and log action
            var mod = _repository.GetChatModeratorByUserId(userId);
            if (mod != null)
            {
                await _repository.IncrementModeratorDeleteCountAsync(mod.Id).ConfigureAwait(false);
            }

            await LogModeratorActionAsync(userId, "delete_message", message.UserId,
                JsonSerializer.Serialize(new { messageId = messageId.ToString() })).ConfigureAwait(false);

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets online users. Requires authentication.
        /// </summary>
        [HttpGet("Users/Online")]
        [AllowAnonymous]
        public async Task<ActionResult> GetOnlineUsers()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var users = _repository.GetOnlineChatUsers(5);
            // Return only display info, not raw user IDs
            var safeUsers = users.Select(u => new Dictionary<string, object?>
            {
                { "userName", u.UserName },
                { "userAvatar", u.Avatar },
                { "isTyping", u.IsTyping }
            }).ToList();
            return Ok(safeUsers);
        }

        /// <summary>
        /// Sends heartbeat to maintain online status.
        /// Admin status is determined SERVER-SIDE from Jellyfin permissions.
        /// </summary>
        [HttpPost("Heartbeat")]
        [AllowAnonymous]
        public async Task<ActionResult> Heartbeat()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var user = _userManager.GetUserById(userId);
            if (user == null) return Unauthorized();

            // Server-side admin check - never trust client
            var isAdmin = IsJellyfinAdmin(userId);
            var avatar = $"/Users/{userId}/Images/Primary";

            await _repository.UpdateChatUserPresenceAsync(userId, user.Username, avatar, isAdmin).ConfigureAwait(false);

            var isModerator = _repository.IsChatModerator(userId);

            return Ok(new Dictionary<string, object>
            {
                { "isAdmin", isAdmin },
                { "isModerator", isModerator }
            });
        }

        /// <summary>
        /// Sets typing status.
        /// </summary>
        [HttpPost("Typing")]
        [AllowAnonymous]
        public async Task<ActionResult> SetTyping([FromQuery] bool isTyping)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            _repository.SetChatUserTyping(userId, isTyping);
            return Ok();
        }

        /// <summary>
        /// Marks messages as read.
        /// </summary>
        [HttpPost("MarkRead")]
        [AllowAnonymous]
        public async Task<ActionResult> MarkRead([FromQuery] Guid messageId)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            await _repository.UpdateLastSeenMessageAsync(userId, messageId).ConfigureAwait(false);
            return Ok();
        }

        /// <summary>
        /// Gets unread message count.
        /// </summary>
        [HttpGet("UnreadCount")]
        [AllowAnonymous]
        public async Task<ActionResult> GetUnreadCount()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var chatUser = _repository.GetChatUser(userId);
            var count = _repository.GetUnreadChatMessageCount(userId, chatUser?.LastSeenMessageId);
            return Ok(new { count });
        }

        // Moderator Management

        /// <summary>
        /// Gets all moderators (admin only).
        /// </summary>
        [HttpGet("Moderators")]
        [AllowAnonymous]
        public async Task<ActionResult> GetModerators()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!IsJellyfinAdmin(userId)) return Forbid();

            var moderators = _repository.GetAllChatModerators();
            return Ok(moderators);
        }

        /// <summary>
        /// Adds a moderator. Admin can add any level, Level 3 can add Level 1-2.
        /// </summary>
        [HttpPost("Moderators")]
        [AllowAnonymous]
        public async Task<ActionResult> AddModerator([FromQuery] Guid targetUserId, [FromQuery] int level = 1)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var myLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            // Level 3+ or admin can add moderators
            if (myLevel < 3 && !isAdmin)
            {
                return Forbid("Only level 3 moderators or admins can add moderators");
            }

            // Validate level
            if (level < 1 || level > 3)
            {
                return BadRequest("Level must be 1, 2, or 3");
            }

            // Level 3 mods can only add Level 1-2
            if (!isAdmin && level >= 3)
            {
                return BadRequest("Only admins can add level 3 moderators");
            }

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            if (_repository.IsChatModerator(targetUserId))
            {
                return BadRequest("User is already a moderator");
            }

            var moderator = new ChatModerator
            {
                Id = Guid.NewGuid(),
                UserId = targetUserId,
                UserName = targetUser.Username,
                AssignedBy = userId,
                AssignedAt = DateTime.UtcNow,
                CanDeleteMessages = true,
                CanSnoozeUsers = true,
                CanTempBan = level >= 2,
                Level = level
            };

            var result = await _repository.AddChatModeratorAsync(moderator).ConfigureAwait(false);

            // Log action
            await LogModeratorActionAsync(userId, "add_mod", targetUserId,
                JsonSerializer.Serialize(new { level })).ConfigureAwait(false);

            return Ok(result);
        }

        /// <summary>
        /// Removes a moderator. Admin can remove any, Level 3 can remove Level 1-2.
        /// </summary>
        [HttpDelete("Moderators/{moderatorId}")]
        [AllowAnonymous]
        public async Task<ActionResult> RemoveModerator([FromRoute] Guid moderatorId)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var myLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            // Level 3+ or admin can remove moderators
            if (myLevel < 3 && !isAdmin)
            {
                return Forbid("Only level 3 moderators or admins can remove moderators");
            }

            var targetMod = _repository.GetChatModeratorById(moderatorId);
            if (targetMod == null) return NotFound("Moderator not found");

            // Level 3 mods can only remove Level 1-2
            if (!isAdmin && targetMod.Level >= 3)
            {
                return BadRequest("Only admins can remove level 3 moderators");
            }

            var removed = await _repository.RemoveChatModeratorAsync(moderatorId).ConfigureAwait(false);
            if (!removed) return NotFound("Moderator not found");

            // Log action
            await LogModeratorActionAsync(userId, "remove_mod", targetMod.UserId,
                JsonSerializer.Serialize(new { removedLevel = targetMod.Level })).ConfigureAwait(false);

            return Ok(new { success = true });
        }

        // Ban Management

        /// <summary>
        /// Bans a user from chat. Level requirements:
        /// - snooze: Level 1+
        /// - chat (temp): Level 2+
        /// - chat (perm): Level 3+ / Admin
        /// - media: Level 3+ / Admin
        /// </summary>
        [HttpPost("Ban")]
        [AllowAnonymous]
        public async Task<ActionResult> BanUser(
            [FromQuery] [Required] Guid targetUserId,
            [FromQuery] [Required] string banType,
            [FromQuery] string? reason,
            [FromQuery] int? durationMinutes)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Validate ban type
            var validBanTypes = new[] { "chat", "snooze", "media" };
            if (!validBanTypes.Contains(banType))
            {
                return BadRequest("Invalid ban type. Must be: chat, snooze, or media");
            }

            // Check rate limit for moderator actions
            var actionRateLimit = config?.ModeratorActionRateLimitPerMinute ?? 10;
            if (IsModeratorActionRateLimited(userId, actionRateLimit))
            {
                return StatusCode(429, "Moderator action rate limit exceeded");
            }

            var modLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            // Level-based permission checks
            if (banType == "snooze")
            {
                // Level 1+ can snooze
                if (modLevel < 1)
                {
                    return Forbid("Snooze requires moderator level 1 or higher");
                }
            }
            else if (banType == "chat")
            {
                // Permanent bans require Level 3+ or Admin
                if (!durationMinutes.HasValue)
                {
                    if (modLevel < 3 && !isAdmin)
                    {
                        return Forbid("Permanent chat bans require moderator level 3 or admin");
                    }
                }
                else
                {
                    // Temp bans require Level 2+
                    if (modLevel < 2)
                    {
                        return Forbid("Temporary chat bans require moderator level 2 or higher");
                    }

                    // Level 2 has max temp ban duration
                    if (modLevel == 2 && !isAdmin)
                    {
                        var maxDays = config?.ModLevel2TempBanMaxDays ?? 7;
                        var maxMinutes = maxDays * 24 * 60;
                        if (durationMinutes.Value > maxMinutes)
                        {
                            return BadRequest($"Level 2 moderators can only temp ban for up to {maxDays} days");
                        }
                    }
                }
            }
            else if (banType == "media")
            {
                // Media bans require Level 3+ or Admin
                if (modLevel < 3 && !isAdmin)
                {
                    return Forbid("Media bans require moderator level 3 or admin");
                }

                // Check monthly media ban limit per user (Level 3 only, not admins)
                if (!isAdmin && durationMinutes.HasValue)
                {
                    var maxDaysPerMonth = config?.ModLevel3MediaBanMaxDays ?? 7;
                    var daysUsed = _repository.GetMediaBanDaysUsedThisMonth(userId, targetUserId);
                    var requestedDays = (int)Math.Ceiling(durationMinutes.Value / (24.0 * 60));
                    if (daysUsed + requestedDays > maxDaysPerMonth)
                    {
                        return BadRequest($"Media ban limit exceeded. You can only ban this user for {maxDaysPerMonth - daysUsed} more days this month");
                    }
                }
            }

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            // Can't target Jellyfin admins
            if (IsJellyfinAdmin(targetUserId))
            {
                return BadRequest("Cannot ban administrators");
            }

            // Can't target equal or higher level moderators
            if (!CanTargetUser(userId, targetUserId))
            {
                return BadRequest("Cannot ban equal or higher level moderators");
            }

            if (durationMinutes.HasValue && durationMinutes.Value <= 0)
            {
                return BadRequest("Duration must be positive");
            }

            // Cap ban duration at 1 year (525600 minutes) to prevent abuse
            var cappedDuration = durationMinutes.HasValue ? Math.Min(durationMinutes.Value, 525600) : (int?)null;

            var banningUser = _userManager.GetUserById(userId);

            var ban = new ChatBan
            {
                Id = Guid.NewGuid(),
                UserId = targetUserId,
                UserName = targetUser.Username,
                BanType = banType,
                Reason = SanitizeMessage(reason, 500),
                BannedBy = userId,
                BannedByName = banningUser?.Username ?? "Unknown",
                BannedAt = DateTime.UtcNow,
                ExpiresAt = cappedDuration.HasValue ? DateTime.UtcNow.AddMinutes(cappedDuration.Value) : null,
                IsPermanent = !cappedDuration.HasValue
            };

            var result = await _repository.AddChatBanAsync(ban).ConfigureAwait(false);

            // Log the action
            var actionType = banType == "snooze" ? "snooze" :
                            (banType == "media" ? "media_ban" :
                            (ban.IsPermanent ? "perm_ban" : "temp_ban"));
            var durationDays = cappedDuration.HasValue ? (int)Math.Ceiling(cappedDuration.Value / (24.0 * 60)) : 0;
            await LogModeratorActionAsync(userId, actionType, targetUserId,
                JsonSerializer.Serialize(new { reason = ban.Reason, durationMinutes = cappedDuration, durationDays, permanent = ban.IsPermanent })).ConfigureAwait(false);

            return Ok(result);
        }

        /// <summary>
        /// Unbans a user.
        /// </summary>
        [HttpDelete("Ban/{banId}")]
        [AllowAnonymous]
        public async Task<ActionResult> UnbanUser([FromRoute] Guid banId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!CanModerate(userId)) return Forbid();

            // Check if this ban was placed by an admin - only admins can lift admin-placed bans
            var ban = _repository.GetChatBanById(banId);
            if (ban == null) return NotFound("Ban not found");

            if (IsJellyfinAdmin(ban.BannedBy) && !IsJellyfinAdmin(userId))
            {
                return BadRequest("Only administrators can lift bans placed by administrators");
            }

            var removed = await _repository.RemoveChatBanAsync(banId).ConfigureAwait(false);
            if (!removed) return NotFound("Ban not found");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets all active bans.
        /// </summary>
        [HttpGet("Bans")]
        [AllowAnonymous]
        public async Task<ActionResult> GetBans()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!CanModerate(userId)) return Forbid();

            var bans = _repository.GetAllActiveChatBans();
            return Ok(bans);
        }

        /// <summary>
        /// Checks if current user is banned.
        /// </summary>
        [HttpGet("BanStatus")]
        [AllowAnonymous]
        public async Task<ActionResult> GetBanStatus()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var chatBan = _repository.GetActiveChatBan(userId, "chat");
            var snoozeBan = _repository.GetActiveChatBan(userId, "snooze");
            var mediaBan = _repository.GetActiveChatBan(userId, "media");

            return Ok(new
            {
                chatBan = chatBan != null ? new { chatBan.ExpiresAt, chatBan.Reason, chatBan.IsPermanent } : null,
                snoozeBan = snoozeBan != null ? new { snoozeBan.ExpiresAt, snoozeBan.Reason } : null,
                mediaBan = mediaBan != null ? new { mediaBan.ExpiresAt, mediaBan.Reason, mediaBan.IsPermanent } : null
            });
        }

        /// <summary>
        /// Gets all users for moderator selection (admin only).
        /// </summary>
        [HttpGet("Users/All")]
        [AllowAnonymous]
        public async Task<ActionResult> GetAllUsers()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!IsJellyfinAdmin(userId)) return Forbid();

            var users = _userManager.Users
                .Select(u => new
                {
                    Id = u.Id,
                    Name = u.Username,
                    IsAdmin = IsJellyfinAdmin(u.Id),
                    IsModerator = _repository.IsChatModerator(u.Id)
                })
                .OrderBy(u => u.Name)
                .ToList();

            return Ok(users);
        }

        /// <summary>
        /// Clears all chat messages (admin only).
        /// </summary>
        [HttpDelete("Messages/Clear")]
        [AllowAnonymous]
        public async Task<ActionResult> ClearAllMessages()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!IsJellyfinAdmin(userId))
            {
                return Forbid();
            }

            var adminUser = _userManager.GetUserById(userId);
            _logger.LogInformation("Admin {AdminName} clearing all chat messages", adminUser?.Username ?? "Unknown");

            await _repository.ClearAllChatMessagesAsync().ConfigureAwait(false);

            return Ok(new { message = "All chat messages cleared" });
        }

        // ============ PRIVATE MESSAGE (DM) ENDPOINTS ============

        /// <summary>
        /// Gets users for DM autocomplete.
        /// </summary>
        [HttpGet("DM/Users")]
        [AllowAnonymous]
        public async Task<ActionResult> GetDMUsers([FromQuery] string? query)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var users = _userManager.Users
                .Where(u => u.Id != userId) // Exclude self
                .Select(u => new
                {
                    Id = u.Id,
                    Name = u.Username,
                    Avatar = $"/Users/{u.Id}/Images/Primary"
                });

            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowerQuery = query.ToLowerInvariant();
                users = users.Where(u => u.Name.ToLowerInvariant().Contains(lowerQuery));
            }

            return Ok(users.OrderBy(u => u.Name).Take(20).ToList());
        }

        /// <summary>
        /// Gets DM conversation list for current user.
        /// </summary>
        [HttpGet("DM/Conversations")]
        [AllowAnonymous]
        public async Task<ActionResult> GetConversations()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var conversations = _repository.GetConversations(userId);

            return Ok(conversations.Select(c => new
            {
                OtherUserId = c.OtherUserId,
                OtherUserName = c.OtherUserName,
                OtherUserAvatar = c.OtherUserAvatar ?? $"/Users/{c.OtherUserId}/Images/Primary",
                LastMessage = new
                {
                    Content = c.LastMessage.IsDeleted ? null : (c.LastMessage.Content?.Length > 50 ? c.LastMessage.Content.Substring(0, 50) + "..." : c.LastMessage.Content),
                    GifUrl = c.LastMessage.IsDeleted ? null : c.LastMessage.GifUrl,
                    Timestamp = c.LastMessage.Timestamp,
                    IsFromMe = c.LastMessage.SenderId == userId
                },
                UnreadCount = c.UnreadCount
            }).ToList());
        }

        /// <summary>
        /// Gets messages in a DM thread. SECURITY: Verifies user is participant.
        /// </summary>
        [HttpGet("DM/{otherUserId}/Messages")]
        [AllowAnonymous]
        public async Task<ActionResult> GetDMMessages(
            [FromRoute] Guid otherUserId,
            [FromQuery] DateTime? since,
            [FromQuery] int limit = 50)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // SECURITY: GetPrivateMessages only returns messages where userId is sender or recipient
            var messages = _repository.GetPrivateMessages(userId, otherUserId, Math.Min(limit, 100), since);

            // Mark messages as read
            await _repository.MarkConversationReadAsync(userId, otherUserId).ConfigureAwait(false);

            return Ok(messages.Select(m => new
            {
                Id = m.Id,
                SenderId = m.SenderId,
                SenderName = m.SenderName,
                SenderAvatar = m.SenderAvatar,
                Content = m.IsDeleted ? null : m.Content,
                GifUrl = m.IsDeleted ? null : m.GifUrl,
                Timestamp = m.Timestamp,
                IsRead = m.IsRead,
                IsDeleted = m.IsDeleted,
                IsFromMe = m.SenderId == userId
            }).ToList());
        }

        /// <summary>
        /// Sends a DM to another user. SECURITY: Rate limited, sanitized.
        /// </summary>
        [HttpPost("DM/{otherUserId}/Messages")]
        [AllowAnonymous]
        public async Task<ActionResult> SendDM(
            [FromRoute] Guid otherUserId,
            [FromBody] PrivateMessageDto dto)
        {
            var config = Plugin.Instance?.Configuration;

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Cannot DM yourself
            if (userId == otherUserId)
            {
                return BadRequest("Cannot send DM to yourself");
            }

            // Verify recipient exists
            var recipient = _userManager.GetUserById(otherUserId);
            if (recipient == null) return NotFound("User not found");

            // Check if sender is banned from chat
            var chatBan = _repository.GetActiveChatBan(userId, "chat");
            if (chatBan != null)
            {
                return StatusCode(403, new { message = "You are banned from chat" });
            }

            // Rate limiting (share with public chat)
            var maxPerMinute = config?.ChatRateLimitPerMinute ?? 10;
            if (IsRateLimited(userId, maxPerMinute))
            {
                return StatusCode(429, "Rate limit exceeded");
            }

            // Validate content
            if (string.IsNullOrWhiteSpace(dto.Content) && string.IsNullOrWhiteSpace(dto.GifUrl))
            {
                return BadRequest("Message content or GIF is required");
            }

            // Validate GIF URL
            if (!string.IsNullOrEmpty(dto.GifUrl))
            {
                if (config?.ChatAllowGifs != true)
                {
                    return BadRequest("GIFs are disabled");
                }
                if (!IsValidGifUrl(dto.GifUrl))
                {
                    return BadRequest("Invalid GIF URL");
                }
            }

            var sender = _userManager.GetUserById(userId);
            if (sender == null) return Unauthorized();

            var maxLength = config?.ChatMaxMessageLength ?? 500;
            var sanitizedContent = SanitizeMessage(dto.Content, maxLength);

            // HTML-encode GIF URL to prevent XSS (URL is already validated by IsValidGifUrl)
            var sanitizedGifUrl = !string.IsNullOrEmpty(dto.GifUrl) && IsValidGifUrl(dto.GifUrl)
                ? WebUtility.HtmlEncode(dto.GifUrl)
                : null;

            var message = new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                SenderName = sender.Username,
                SenderAvatar = $"/Users/{userId}/Images/Primary",
                RecipientId = otherUserId,
                RecipientName = recipient.Username,
                Content = sanitizedContent,
                GifUrl = sanitizedGifUrl,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            var result = await _repository.AddPrivateMessageAsync(message).ConfigureAwait(false);
            return Ok(new
            {
                Id = result.Id,
                Timestamp = result.Timestamp
            });
        }

        /// <summary>
        /// Gets total unread DM count.
        /// </summary>
        [HttpGet("DM/Unread")]
        [AllowAnonymous]
        public async Task<ActionResult> GetUnreadDMCount()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var count = _repository.GetUnreadDMCount(userId);
            return Ok(new { count });
        }

        /// <summary>
        /// Deletes own DM message.
        /// </summary>
        [HttpDelete("DM/Messages/{messageId}")]
        [AllowAnonymous]
        public async Task<ActionResult> DeleteDM([FromRoute] Guid messageId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // SECURITY: DeletePrivateMessageAsync verifies userId is the sender
            var deleted = await _repository.DeletePrivateMessageAsync(messageId, userId).ConfigureAwait(false);
            if (!deleted) return NotFound("Message not found or not yours");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets public chat unread count.
        /// </summary>
        [HttpGet("Public/Unread")]
        [AllowAnonymous]
        public async Task<ActionResult> GetPublicUnreadCount()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var count = _repository.GetPublicChatUnreadCount(userId);
            return Ok(new { count });
        }

        /// <summary>
        /// Marks public chat as read for current user.
        /// </summary>
        [HttpPost("Public/MarkRead")]
        [AllowAnonymous]
        public async Task<ActionResult> MarkPublicChatRead()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            await _repository.MarkPublicChatReadAsync(userId).ConfigureAwait(false);
            return Ok(new { success = true });
        }

        // ============ MODERATOR MANAGEMENT ENDPOINTS ============

        /// <summary>
        /// Gets moderator stats including action counts (admin/mod only).
        /// </summary>
        [HttpGet("Moderators/Stats")]
        [AllowAnonymous]
        public async Task<ActionResult> GetModeratorStats()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!CanModerate(userId)) return Forbid();

            var moderators = _repository.GetAllChatModerators();
            var config = Plugin.Instance?.Configuration;

            var stats = moderators.Select(m =>
            {
                _repository.ResetModeratorDailyDeleteCount(m.Id);
                var limit = m.Level == 1 ? (config?.ModLevel1DeleteLimit ?? 20) : (config?.ModLevel2DeleteLimit ?? 50);

                return new
                {
                    m.Id,
                    m.UserId,
                    m.UserName,
                    m.Level,
                    m.AssignedAt,
                    AssignedByName = _userManager.GetUserById(m.AssignedBy)?.Username ?? "Unknown",
                    ActionCount = _repository.GetModeratorActionCount(m.UserId),
                    DailyDeleteCount = m.DailyDeleteCount,
                    DailyDeleteLimit = limit
                };
            }).ToList();

            return Ok(stats);
        }

        /// <summary>
        /// Gets moderator action log (admin/mod only).
        /// </summary>
        [HttpGet("Moderators/Actions")]
        [AllowAnonymous]
        public async Task<ActionResult> GetModeratorActions(
            [FromQuery] Guid? moderatorId,
            [FromQuery] int limit = 100)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!CanModerate(userId)) return Forbid();

            var actions = _repository.GetModeratorActions(moderatorId, Math.Min(limit, 500));
            return Ok(actions);
        }

        /// <summary>
        /// Updates a moderator's level (admin only for level 3, level 3+ for 1-2).
        /// </summary>
        [HttpPut("Moderators/{moderatorId}/Level")]
        [AllowAnonymous]
        public async Task<ActionResult> UpdateModeratorLevel(
            [FromRoute] Guid moderatorId,
            [FromQuery] int level)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var myLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            if (myLevel < 3 && !isAdmin)
            {
                return Forbid("Only level 3 moderators or admins can change levels");
            }

            if (level < 1 || level > 3)
            {
                return BadRequest("Level must be 1, 2, or 3");
            }

            var targetMod = _repository.GetChatModeratorById(moderatorId);
            if (targetMod == null) return NotFound("Moderator not found");

            // Level 3 mods can only set levels 1-2
            if (!isAdmin && (level >= 3 || targetMod.Level >= 3))
            {
                return BadRequest("Only admins can modify level 3 moderators");
            }

            var oldLevel = targetMod.Level;
            var updated = await _repository.UpdateChatModeratorAsync(moderatorId, level).ConfigureAwait(false);

            await LogModeratorActionAsync(userId, "change_level", targetMod.UserId,
                JsonSerializer.Serialize(new { oldLevel, newLevel = level })).ConfigureAwait(false);

            return Ok(updated);
        }

        // ============ USER STYLE OVERRIDE ENDPOINTS ============

        /// <summary>
        /// Sets user style override (Level 1+ mods).
        /// </summary>
        [HttpPost("Users/{targetUserId}/Style")]
        [AllowAnonymous]
        public async Task<ActionResult> SetUserStyle(
            [FromRoute] Guid targetUserId,
            [FromQuery] string? nicknameColor,
            [FromQuery] string? messageColor,
            [FromQuery] string? textStyle)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            var modLevel = GetModeratorLevel(userId);

            if (modLevel < 1)
            {
                return Forbid("Only moderators can set user styles");
            }

            // Validate colors
            if (!IsValidHexColor(nicknameColor))
            {
                return BadRequest("Invalid nickname color format. Use #RRGGBB");
            }
            if (!IsValidHexColor(messageColor))
            {
                return BadRequest("Invalid message color format. Use #RRGGBB");
            }
            if (!IsValidTextStyle(textStyle))
            {
                return BadRequest("Invalid text style. Use: bold, italic, or bold-italic");
            }

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            var settingUser = _userManager.GetUserById(userId);

            var style = new Models.UserStyleOverride
            {
                UserId = targetUserId,
                UserName = targetUser.Username,
                NicknameColor = nicknameColor,
                MessageColor = messageColor,
                TextStyle = textStyle ?? string.Empty,
                SetBy = userId,
                SetByName = settingUser?.Username ?? "Unknown",
                SetAt = DateTime.UtcNow
            };

            var result = await _repository.SetUserStyleOverrideAsync(style).ConfigureAwait(false);

            await LogModeratorActionAsync(userId, "change_style", targetUserId,
                JsonSerializer.Serialize(new { nicknameColor, messageColor, textStyle })).ConfigureAwait(false);

            return Ok(result);
        }

        /// <summary>
        /// Removes user style override (Level 1+ mods).
        /// </summary>
        [HttpDelete("Users/{targetUserId}/Style")]
        [AllowAnonymous]
        public async Task<ActionResult> RemoveUserStyle([FromRoute] Guid targetUserId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            var modLevel = GetModeratorLevel(userId);

            if (modLevel < 1)
            {
                return Forbid("Only moderators can remove user styles");
            }

            var removed = await _repository.RemoveUserStyleOverrideAsync(targetUserId).ConfigureAwait(false);
            if (!removed) return NotFound("Style override not found");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets all user style overrides (mods only).
        /// </summary>
        [HttpGet("Users/Styles")]
        [AllowAnonymous]
        public async Task<ActionResult> GetAllUserStyles()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Anyone can see styles (needed for rendering)
            var styles = _repository.GetAllUserStyleOverrides();
            return Ok(styles.ToDictionary(s => s.UserId.ToString(), s => new
            {
                s.NicknameColor,
                s.MessageColor,
                s.TextStyle
            }));
        }

        // ============ MEDIA QUOTA ENDPOINTS ============

        /// <summary>
        /// Sets media quota for a user (Level 3+ / Admin).
        /// </summary>
        [HttpPost("Users/{targetUserId}/Quota")]
        [AllowAnonymous]
        public async Task<ActionResult> SetMediaQuota(
            [FromRoute] Guid targetUserId,
            [FromQuery] int dailyLimit = 0,
            [FromQuery] int weeklyLimit = 0,
            [FromQuery] int monthlyLimit = 0)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            var modLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            if (modLevel < 3 && !isAdmin)
            {
                return Forbid("Only level 3 moderators or admins can set media quotas");
            }

            // Cannot set quota on admins
            if (IsJellyfinAdmin(targetUserId))
            {
                return BadRequest("Cannot set quota on administrators");
            }

            // Cannot set quota on equal/higher level mods
            if (!CanTargetUser(userId, targetUserId))
            {
                return BadRequest("Cannot set quota on equal or higher level moderators");
            }

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            var settingUser = _userManager.GetUserById(userId);

            var quota = new Models.MediaQuota
            {
                UserId = targetUserId,
                UserName = targetUser.Username,
                DailyLimit = Math.Max(0, dailyLimit),
                WeeklyLimit = Math.Max(0, weeklyLimit),
                MonthlyLimit = Math.Max(0, monthlyLimit),
                SetBy = userId,
                SetByName = settingUser?.Username ?? "Unknown",
                SetAt = DateTime.UtcNow
            };

            var result = await _repository.SetMediaQuotaAsync(quota).ConfigureAwait(false);

            await LogModeratorActionAsync(userId, "set_quota", targetUserId,
                JsonSerializer.Serialize(new { dailyLimit, weeklyLimit, monthlyLimit })).ConfigureAwait(false);

            return Ok(result);
        }

        /// <summary>
        /// Removes media quota for a user (Level 3+ / Admin).
        /// </summary>
        [HttpDelete("Users/{targetUserId}/Quota")]
        [AllowAnonymous]
        public async Task<ActionResult> RemoveMediaQuota([FromRoute] Guid targetUserId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            var modLevel = GetModeratorLevel(userId);
            var isAdmin = IsJellyfinAdmin(userId);

            if (modLevel < 3 && !isAdmin)
            {
                return Forbid("Only level 3 moderators or admins can remove media quotas");
            }

            var removed = await _repository.RemoveMediaQuotaAsync(targetUserId).ConfigureAwait(false);
            if (!removed) return NotFound("Quota not found");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets current user's moderator info (level, limits, etc.).
        /// </summary>
        [HttpGet("Moderators/Me")]
        [AllowAnonymous]
        public async Task<ActionResult> GetMyModeratorInfo()
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            var isAdmin = IsJellyfinAdmin(userId);
            var mod = _repository.GetChatModeratorByUserId(userId);
            var config = Plugin.Instance?.Configuration;

            if (!isAdmin && mod == null)
            {
                return Ok(new { isModerator = false, isAdmin = false });
            }

            if (isAdmin)
            {
                return Ok(new
                {
                    isModerator = true,
                    isAdmin = true,
                    level = 99,
                    canAddMods = true,
                    canMediaBan = true,
                    canPermBan = true,
                    dailyDeleteLimit = -1, // unlimited
                    dailyDeleteCount = 0
                });
            }

            _repository.ResetModeratorDailyDeleteCount(mod!.Id);
            var deleteLimit = mod.Level == 1 ? (config?.ModLevel1DeleteLimit ?? 20) : (config?.ModLevel2DeleteLimit ?? 50);

            return Ok(new
            {
                isModerator = true,
                isAdmin = false,
                level = mod.Level,
                canAddMods = mod.Level >= 3,
                canMediaBan = mod.Level >= 3,
                canPermBan = mod.Level >= 3,
                canTempBan = mod.Level >= 2,
                dailyDeleteLimit = deleteLimit,
                dailyDeleteCount = mod.DailyDeleteCount,
                tempBanMaxDays = config?.ModLevel2TempBanMaxDays ?? 7,
                mediaBanMaxDays = config?.ModLevel3MediaBanMaxDays ?? 7
            });
        }
    }
}
