using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
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
    [AllowAnonymous]
    public class ChatController : ControllerBase
    {
        private readonly RatingsRepository _repository;
        private readonly IUserManager _userManager;
        private readonly ISessionManager _sessionManager;
        private readonly ILogger<ChatController> _logger;
        private static readonly Dictionary<Guid, DateTime> _rateLimitTracker = new();
        private static readonly Dictionary<Guid, int> _messageCountTracker = new();
        private static readonly object _rateLock = new();

        /// <summary>
        /// Allowed GIF domains for security.
        /// </summary>
        private static readonly string[] AllowedGifDomains = new[]
        {
            "tenor.com", "media.tenor.com", "c.tenor.com",
            "giphy.com", "media.giphy.com", "media0.giphy.com",
            "media1.giphy.com", "media2.giphy.com", "media3.giphy.com", "media4.giphy.com"
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
        private Guid GetCurrentUserId()
        {
            var userId = User.GetUserId();
            if (userId != Guid.Empty) return userId;

            var authHeader = Request.Headers["X-Emby-Authorization"].FirstOrDefault()
                          ?? Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader)) return Guid.Empty;

            var tokenMatch = Regex.Match(authHeader, @"Token=""([^""]+)""");
            if (!tokenMatch.Success) return Guid.Empty;

            var token = tokenMatch.Groups[1].Value;
            var sessionTask = _sessionManager.GetSessionByAuthenticationToken(token, null, null);
            var session = sessionTask.Result;
            return session?.UserId ?? Guid.Empty;
        }

        /// <summary>
        /// Checks if the current user is an admin.
        /// Admin status is determined by the client passing it in heartbeat and stored in ChatUser.
        /// This follows the same pattern as RatingsController which relies on client-side admin checks.
        /// </summary>
        private bool IsCurrentUserAdmin()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return false;
            // Check stored admin status from last heartbeat
            return _repository.IsChatUserAdmin(userId);
        }

        /// <summary>
        /// Checks if user can moderate (admin or moderator).
        /// </summary>
        private bool CanModerate(Guid userId)
        {
            // Check stored admin status or moderator list
            if (_repository.IsChatUserAdmin(userId)) return true;
            return _repository.IsChatModerator(userId);
        }

        /// <summary>
        /// Sanitizes message content to prevent XSS.
        /// </summary>
        private string SanitizeMessage(string input, int maxLength = 500)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            // Trim and limit length
            input = input.Trim();
            if (input.Length > maxLength) input = input.Substring(0, maxLength);

            // HTML encode to prevent XSS
            input = HttpUtility.HtmlEncode(input);

            // Remove potential script patterns
            input = Regex.Replace(input, @"javascript:", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"on\w+\s*=", "", RegexOptions.IgnoreCase);
            input = Regex.Replace(input, @"<script", "", RegexOptions.IgnoreCase);

            return input;
        }

        /// <summary>
        /// Validates GIF URL is from allowed domains.
        /// </summary>
        private bool IsValidGifUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return true; // null is OK (no GIF)
            try
            {
                var uri = new Uri(url);
                return AllowedGifDomains.Any(d => uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks rate limit for a user.
        /// </summary>
        private bool IsRateLimited(Guid userId)
        {
            var config = Plugin.Instance?.Configuration;
            var maxPerMinute = config?.ChatRateLimitPerMinute ?? 10;

            lock (_rateLock)
            {
                var now = DateTime.UtcNow;

                // Reset counter if minute has passed
                if (_rateLimitTracker.TryGetValue(userId, out var lastReset))
                {
                    if ((now - lastReset).TotalMinutes >= 1)
                    {
                        _rateLimitTracker[userId] = now;
                        _messageCountTracker[userId] = 0;
                    }
                }
                else
                {
                    _rateLimitTracker[userId] = now;
                    _messageCountTracker[userId] = 0;
                }

                var count = _messageCountTracker.GetValueOrDefault(userId, 0);
                if (count >= maxPerMinute) return true;

                _messageCountTracker[userId] = count + 1;
                return false;
            }
        }

        /// <summary>
        /// Gets chat configuration.
        /// </summary>
        [HttpGet("Config")]
        [AllowAnonymous]
        public ActionResult GetChatConfig()
        {
            var config = Plugin.Instance?.Configuration;
            return Ok(new
            {
                EnableChat = config?.EnableChat ?? false,
                ChatAllowGifs = config?.ChatAllowGifs ?? true,
                ChatAllowEmojis = config?.ChatAllowEmojis ?? true,
                ChatMaxMessageLength = config?.ChatMaxMessageLength ?? 500,
                TenorApiKey = config?.TenorApiKey ?? ""
            });
        }

        /// <summary>
        /// Gets recent chat messages.
        /// </summary>
        [HttpGet("Messages")]
        [AllowAnonymous]
        public ActionResult<List<ChatMessage>> GetMessages([FromQuery] DateTime? since, [FromQuery] int limit = 100)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var userId = GetCurrentUserId();
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

            // Enrich messages with admin/moderator status
            var enrichedMessages = messages.Select(m => new
            {
                id = m.Id,
                userId = m.UserId,
                userName = m.UserName,
                userAvatar = m.UserAvatar,
                content = m.Content,
                gifUrl = m.GifUrl,
                timestamp = m.Timestamp,
                isDeleted = m.IsDeleted,
                replyToId = m.ReplyToId,
                isAdmin = _repository.IsChatUserAdmin(m.UserId),
                isModerator = _repository.IsChatModerator(m.UserId)
            }).ToList();

            return Ok(new { messages = enrichedMessages, typingUsers });
        }

        /// <summary>
        /// Sends a chat message.
        /// </summary>
        [HttpPost("Messages")]
        [AllowAnonymous]
        public ActionResult<ChatMessage> SendMessage([FromBody] ChatMessageDto dto)
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var userId = GetCurrentUserId();
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
            if (IsRateLimited(userId))
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
                    return BadRequest("Invalid GIF URL. Only Tenor and Giphy are allowed.");
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

            var result = _repository.AddChatMessageAsync(message).Result;
            return Ok(result);
        }

        /// <summary>
        /// Deletes a chat message (admin/moderator only).
        /// </summary>
        [HttpDelete("Messages/{messageId}")]
        [AllowAnonymous]
        public ActionResult DeleteMessage([FromRoute] Guid messageId)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            if (!CanModerate(userId))
            {
                return Forbid("Only admins and moderators can delete messages");
            }

            var deleted = _repository.DeleteChatMessageAsync(messageId, userId).Result;
            if (!deleted) return NotFound("Message not found");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets online users.
        /// </summary>
        [HttpGet("Users/Online")]
        [AllowAnonymous]
        public ActionResult<List<ChatUser>> GetOnlineUsers()
        {
            var config = Plugin.Instance?.Configuration;
            if (config?.EnableChat != true)
            {
                return BadRequest("Chat is disabled");
            }

            var users = _repository.GetOnlineChatUsers(5);
            return Ok(users);
        }

        /// <summary>
        /// Sends heartbeat to maintain online status.
        /// Admin status is passed from client (client can determine from user policy).
        /// </summary>
        [HttpPost("Heartbeat")]
        [AllowAnonymous]
        public async Task<ActionResult> Heartbeat([FromBody] HeartbeatRequest? request = null)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var user = _userManager.GetUserById(userId);
            if (user == null) return Unauthorized();

            // Admin status comes from client (they check ApiClient.getCurrentUser().Policy.IsAdministrator)
            var isAdmin = request?.IsAdmin ?? false;
            var avatar = $"/Users/{userId}/Images/Primary";

            await _repository.UpdateChatUserPresenceAsync(userId, user.Username, avatar, isAdmin);

            var isModerator = _repository.IsChatModerator(userId);

            return Ok(new Dictionary<string, object>
            {
                { "isAdmin", isAdmin },
                { "isModerator", isModerator }
            });
        }

        /// <summary>
        /// Heartbeat request model.
        /// </summary>
        public class HeartbeatRequest
        {
            /// <summary>
            /// Gets or sets whether the user is an admin (determined client-side).
            /// </summary>
            public bool IsAdmin { get; set; }
        }

        /// <summary>
        /// Sets typing status.
        /// </summary>
        [HttpPost("Typing")]
        [AllowAnonymous]
        public ActionResult SetTyping([FromQuery] bool isTyping)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            _repository.SetChatUserTyping(userId, isTyping);
            return Ok();
        }

        /// <summary>
        /// Marks messages as read.
        /// </summary>
        [HttpPost("MarkRead")]
        [AllowAnonymous]
        public ActionResult MarkRead([FromQuery] Guid messageId)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            _repository.UpdateLastSeenMessageAsync(userId, messageId).Wait();
            return Ok();
        }

        /// <summary>
        /// Gets unread message count.
        /// </summary>
        [HttpGet("UnreadCount")]
        [AllowAnonymous]
        public ActionResult GetUnreadCount()
        {
            var userId = GetCurrentUserId();
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
        public ActionResult<List<ChatModerator>> GetModerators()
        {
            if (!IsCurrentUserAdmin()) return Forbid();

            var moderators = _repository.GetAllChatModerators();
            return Ok(moderators);
        }

        /// <summary>
        /// Adds a moderator (admin only).
        /// </summary>
        [HttpPost("Moderators")]
        [AllowAnonymous]
        public ActionResult<ChatModerator> AddModerator([FromQuery] Guid targetUserId)
        {
            var userId = GetCurrentUserId();
            if (!IsCurrentUserAdmin()) return Forbid();

            var targetUser = _userManager.GetUserById(targetUserId);
            if (targetUser == null) return NotFound("User not found");

            // Check if already a moderator
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

            var result = _repository.AddChatModeratorAsync(moderator).Result;
            return Ok(result);
        }

        /// <summary>
        /// Removes a moderator (admin only).
        /// </summary>
        [HttpDelete("Moderators/{moderatorId}")]
        [AllowAnonymous]
        public ActionResult RemoveModerator([FromRoute] Guid moderatorId)
        {
            if (!IsCurrentUserAdmin()) return Forbid();

            var removed = _repository.RemoveChatModeratorAsync(moderatorId).Result;
            if (!removed) return NotFound("Moderator not found");

            return Ok(new { success = true });
        }

        // Ban Management

        /// <summary>
        /// Bans a user from chat.
        /// </summary>
        [HttpPost("Ban")]
        [AllowAnonymous]
        public ActionResult<ChatBan> BanUser(
            [FromQuery] [Required] Guid targetUserId,
            [FromQuery] [Required] string banType,
            [FromQuery] string? reason,
            [FromQuery] int? durationMinutes)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            // Validate ban type
            var validBanTypes = new[] { "chat", "snooze", "media" };
            if (!validBanTypes.Contains(banType))
            {
                return BadRequest("Invalid ban type. Must be: chat, snooze, or media");
            }

            // Media bans are admin only
            if (banType == "media" && !IsCurrentUserAdmin())
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

            // Check if target is admin (can't ban admins)
            if (_repository.IsChatUserAdmin(targetUserId))
            {
                return BadRequest("Cannot ban administrators");
            }

            var banningUser = _userManager.GetUserById(userId);

            var ban = new ChatBan
            {
                Id = Guid.NewGuid(),
                UserId = targetUserId,
                UserName = targetUser.Username,
                BanType = banType,
                Reason = reason,
                BannedBy = userId,
                BannedByName = banningUser?.Username ?? "Unknown",
                BannedAt = DateTime.UtcNow,
                ExpiresAt = durationMinutes.HasValue ? DateTime.UtcNow.AddMinutes(durationMinutes.Value) : null,
                IsPermanent = !durationMinutes.HasValue
            };

            var result = _repository.AddChatBanAsync(ban).Result;
            return Ok(result);
        }

        /// <summary>
        /// Unbans a user.
        /// </summary>
        [HttpDelete("Ban/{banId}")]
        [AllowAnonymous]
        public ActionResult UnbanUser([FromRoute] Guid banId)
        {
            var userId = GetCurrentUserId();
            if (!CanModerate(userId)) return Forbid();

            var removed = _repository.RemoveChatBanAsync(banId).Result;
            if (!removed) return NotFound("Ban not found");

            return Ok(new { success = true });
        }

        /// <summary>
        /// Gets all active bans.
        /// </summary>
        [HttpGet("Bans")]
        [AllowAnonymous]
        public ActionResult<List<ChatBan>> GetBans()
        {
            var userId = GetCurrentUserId();
            if (!CanModerate(userId)) return Forbid();

            var bans = _repository.GetAllActiveChatBans();
            return Ok(bans);
        }

        /// <summary>
        /// Checks if current user is banned.
        /// </summary>
        [HttpGet("BanStatus")]
        [AllowAnonymous]
        public ActionResult GetBanStatus()
        {
            var userId = GetCurrentUserId();
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
        public ActionResult GetAllUsers()
        {
            if (!IsCurrentUserAdmin()) return Forbid();

            var users = _userManager.Users
                .Select(u => new
                {
                    Id = u.Id,
                    Name = u.Username,
                    IsAdmin = _repository.IsChatUserAdmin(u.Id),
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
            if (!IsCurrentUserAdmin())
            {
                return Forbid();
            }

            var adminId = GetCurrentUserId();
            var adminUser = _userManager.GetUserById(adminId);
            _logger.LogInformation("Admin {AdminName} clearing all chat messages", adminUser?.Username ?? "Unknown");

            await _repository.ClearAllChatMessagesAsync();

            return Ok(new { message = "All chat messages cleared" });
        }
    }
}
