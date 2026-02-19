using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
        private static readonly HttpClient _httpClient = new HttpClient();

        // Rate limiting with ConcurrentDictionary for thread safety
        private static readonly ConcurrentDictionary<Guid, (DateTime ResetTime, int Count)> _rateLimits = new();
        private static DateTime _lastRateLimitCleanup = DateTime.UtcNow;

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

            // Periodic cleanup of stale entries (every 5 minutes)
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

            var apiKey = config?.KlipyApiKey ?? config?.TenorApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest("GIF API not configured");
            }

            try
            {
                // Klipy API search
                var encodedQuery = Uri.EscapeDataString(query);
                var url = $"https://klipy.com/api/gifs/search?query={encodedQuery}&limit={limit}&key={apiKey}";

                var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GIF search failed with status {Status}", response.StatusCode);
                    return StatusCode(502, "GIF search failed");
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Parse and return only necessary data (no API key leakage)
                using var doc = JsonDocument.Parse(content);
                var results = new List<object>();

                if (doc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataElement.EnumerateArray())
                    {
                        var gifUrl = "";
                        var previewUrl = "";
                        var title = "";

                        // Try to extract GIF URL from various formats
                        if (item.TryGetProperty("url", out var urlProp))
                        {
                            gifUrl = urlProp.GetString() ?? "";
                        }
                        else if (item.TryGetProperty("media_formats", out var mediaFormats))
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

                        if (item.TryGetProperty("preview", out var previewProp))
                        {
                            previewUrl = previewProp.GetString() ?? previewUrl;
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

                return Ok(new { results });
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

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = user.Username,
                UserAvatar = $"/Users/{userId}/Images/Primary",
                Content = sanitizedContent,
                GifUrl = dto.GifUrl,
                Timestamp = DateTime.UtcNow,
                ReplyToId = dto.ReplyToId
            };

            var result = await _repository.AddChatMessageAsync(message).ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// Deletes a chat message (admin/moderator only).
        /// </summary>
        [HttpDelete("Messages/{messageId}")]
        [AllowAnonymous]
        public async Task<ActionResult> DeleteMessage([FromRoute] Guid messageId)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true) return BadRequest("Chat is disabled");

            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // Check if user owns the message or is a moderator
            var message = _repository.GetChatMessageById(messageId);
            if (message == null) return NotFound("Message not found");

            if (message.UserId != userId && !CanModerate(userId))
            {
                return Forbid("You can only delete your own messages");
            }

            var deleted = await _repository.DeleteChatMessageAsync(messageId, userId).ConfigureAwait(false);
            if (!deleted) return NotFound("Message not found");

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
        /// Adds a moderator (admin only).
        /// </summary>
        [HttpPost("Moderators")]
        [AllowAnonymous]
        public async Task<ActionResult> AddModerator([FromQuery] Guid targetUserId)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!IsJellyfinAdmin(userId)) return Forbid();

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
                CanTempBan = true
            };

            var result = await _repository.AddChatModeratorAsync(moderator).ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// Removes a moderator (admin only).
        /// </summary>
        [HttpDelete("Moderators/{moderatorId}")]
        [AllowAnonymous]
        public async Task<ActionResult> RemoveModerator([FromRoute] Guid moderatorId)
        {
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (!IsJellyfinAdmin(userId)) return Forbid();

            var removed = await _repository.RemoveChatModeratorAsync(moderatorId).ConfigureAwait(false);
            if (!removed) return NotFound("Moderator not found");

            return Ok(new { success = true });
        }

        // Ban Management

        /// <summary>
        /// Bans a user from chat.
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

            // Media bans are admin only
            if (banType == "media" && !IsJellyfinAdmin(userId))
            {
                return Forbid("Only admins can ban users from watching media");
            }

            // Other bans require moderator or admin
            if (!CanModerate(userId))
            {
                return Forbid("Only admins and moderators can ban users");
            }

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            // Can't ban Jellyfin admins
            if (IsJellyfinAdmin(targetUserId))
            {
                return BadRequest("Cannot ban administrators");
            }

            // Only admins can ban moderators
            if (_repository.IsChatModerator(targetUserId) && !IsJellyfinAdmin(userId))
            {
                return BadRequest("Only administrators can ban moderators");
            }

            if (durationMinutes.HasValue && durationMinutes.Value <= 0)
            {
                return BadRequest("Duration must be positive");
            }

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
                ExpiresAt = durationMinutes.HasValue ? DateTime.UtcNow.AddMinutes(durationMinutes.Value) : null,
                IsPermanent = !durationMinutes.HasValue
            };

            var result = await _repository.AddChatBanAsync(ban).ConfigureAwait(false);
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

            var message = new PrivateMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                SenderName = sender.Username,
                SenderAvatar = $"/Users/{userId}/Images/Primary",
                RecipientId = otherUserId,
                RecipientName = recipient.Username,
                Content = sanitizedContent,
                GifUrl = dto.GifUrl,
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
            var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
            if (userId == Guid.Empty) return Unauthorized();

            // SECURITY: DeletePrivateMessageAsync verifies userId is the sender
            var deleted = await _repository.DeletePrivateMessageAsync(messageId, userId).ConfigureAwait(false);
            if (!deleted) return NotFound("Message not found or not yours");

            return Ok(new { success = true });
        }
    }
}
