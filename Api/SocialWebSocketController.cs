using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// WebSocket endpoint for real-time social updates.
    /// </summary>
    [ApiController]
    [Route("Social")]
    public class SocialWebSocketController : ControllerBase
    {
        private readonly SocialWebSocketHandler _webSocketHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocialWebSocketController"/> class.
        /// </summary>
        /// <param name="webSocketHandler">The WebSocket handler.</param>
        public SocialWebSocketController(SocialWebSocketHandler webSocketHandler)
        {
            _webSocketHandler = webSocketHandler;
        }

        /// <summary>
        /// WebSocket endpoint for real-time status updates.
        /// Connect with: ws://server/Social/WebSocket?token=YOUR_TOKEN
        /// </summary>
        /// <returns>WebSocket connection.</returns>
        [HttpGet("WebSocket")]
        public async Task WebSocket()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                await _webSocketHandler.HandleConnectionAsync(HttpContext, webSocket).ConfigureAwait(false);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        /// <summary>
        /// Gets WebSocket connection stats (for debugging).
        /// </summary>
        /// <returns>Connection statistics.</returns>
        [HttpGet("WebSocket/Stats")]
        public ActionResult<object> GetStats()
        {
            return Ok(new
            {
                connectedUsers = _webSocketHandler.GetConnectedUserCount(),
                totalConnections = _webSocketHandler.GetTotalConnectionCount()
            });
        }
    }
}
