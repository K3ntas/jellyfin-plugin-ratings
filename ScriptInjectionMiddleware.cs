using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Middleware that injects the ratings plugin script into HTML responses.
    /// This approach does not require file system write permissions and works on all platforms.
    /// </summary>
    public class ScriptInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ScriptInjectionMiddleware> _logger;
        private readonly string _scriptTag;
        private bool _loggedSuccess;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptInjectionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">Logger instance.</param>
        public ScriptInjectionMiddleware(RequestDelegate next, ILogger<ScriptInjectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _scriptTag = "\n<!-- BEGIN Ratings Plugin (Middleware Injection) -->\n" +
                         "<script defer src=\"/Ratings/ratings.js\"></script>\n" +
                         "<!-- END Ratings Plugin -->\n";
            _loggedSuccess = false;
        }

        /// <summary>
        /// Invokes the middleware.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Only intercept requests for the main page or index.html
            // These paths serve the Jellyfin web client
            bool isIndexHtml = path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/web/", StringComparison.OrdinalIgnoreCase) ||
                               path.Equals("/web", StringComparison.OrdinalIgnoreCase);

            if (!isIndexHtml)
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Check Accept header to ensure we're serving HTML
            var acceptHeader = context.Request.Headers["Accept"].ToString();
            if (!string.IsNullOrEmpty(acceptHeader) &&
                !acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
                !acceptHeader.Contains("*/*", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            // Capture the original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Use a memory stream to capture the response
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                // Call the next middleware
                await _next(context).ConfigureAwait(false);

                // Only process HTML responses
                var contentType = context.Response.ContentType ?? string.Empty;
                if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    // Not HTML, just copy the response as-is
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Read the response body
                memoryStream.Seek(0, SeekOrigin.Begin);
                var responseBody = await new StreamReader(memoryStream, Encoding.UTF8).ReadToEndAsync().ConfigureAwait(false);

                // Check if script is already injected (e.g., by file-based injection)
                if (responseBody.Contains("/Ratings/ratings.js", StringComparison.OrdinalIgnoreCase))
                {
                    // Already injected, don't duplicate
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    context.Response.Body = originalBodyStream;
                    await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Inject the script before </body>
                string modifiedBody;
                if (responseBody.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                {
                    // Find </body> case-insensitively and inject before it
                    var bodyTagIndex = responseBody.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
                    modifiedBody = responseBody.Insert(bodyTagIndex, _scriptTag);

                    if (!_loggedSuccess)
                    {
                        _logger.LogInformation("Ratings plugin: Successfully injecting script via HTTP middleware (no file modification required)");
                        _loggedSuccess = true;
                    }
                }
                else if (responseBody.Contains("</html>", StringComparison.OrdinalIgnoreCase))
                {
                    // Fallback: inject before </html>
                    var htmlTagIndex = responseBody.LastIndexOf("</html>", StringComparison.OrdinalIgnoreCase);
                    modifiedBody = responseBody.Insert(htmlTagIndex, _scriptTag);

                    if (!_loggedSuccess)
                    {
                        _logger.LogInformation("Ratings plugin: Injecting script via middleware (before </html>)");
                        _loggedSuccess = true;
                    }
                }
                else
                {
                    // Cannot find injection point, return original
                    modifiedBody = responseBody;
                    _logger.LogWarning("Ratings plugin: Could not find </body> or </html> tag to inject script");
                }

                // Write the modified response
                var modifiedBytes = Encoding.UTF8.GetBytes(modifiedBody);
                context.Response.Body = originalBodyStream;
                context.Response.ContentLength = modifiedBytes.Length;
                await originalBodyStream.WriteAsync(modifiedBytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ratings plugin: Error in script injection middleware");

                // On error, try to pass through the original response
                context.Response.Body = originalBodyStream;
                throw;
            }
        }
    }
}
