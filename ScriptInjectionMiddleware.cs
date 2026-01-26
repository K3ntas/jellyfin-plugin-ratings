using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Middleware that injects the ratings JavaScript into index.html responses.
    /// This approach doesn't require file system write permissions.
    /// </summary>
    public class ScriptInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ScriptInjectionMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptInjectionMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">The logger instance.</param>
        public ScriptInjectionMiddleware(RequestDelegate next, ILogger<ScriptInjectionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Processes the HTTP request and injects script into index.html responses.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var pathBase = context.Request.PathBase.Value ?? string.Empty;

            // Only intercept exact index.html requests (strict matching)
            if (!IsIndexHtmlRequest(path))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

            _logger.LogDebug("ScriptInjection: Intercepting request - Path={Path}, PathBase={PathBase}", path, pathBase);

            // Remove Accept-Encoding to prevent compressed response
            context.Request.Headers.Remove("Accept-Encoding");

            // Store original body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                // Call the next middleware
                await _next(context).ConfigureAwait(false);

                // Only process successful HTML responses
                if (context.Response.StatusCode != 200)
                {
                    _logger.LogDebug("ScriptInjection: Skipping - StatusCode={StatusCode} (not 200)", context.Response.StatusCode);
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Check for compression (shouldn't happen but safety check)
                var contentEncoding = context.Response.Headers.ContentEncoding.ToString();
                if (!string.IsNullOrEmpty(contentEncoding))
                {
                    _logger.LogDebug("ScriptInjection: Skipping - Response is compressed: {Encoding}", contentEncoding);
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Only process text/html
                var contentType = context.Response.ContentType ?? string.Empty;
                if (!contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("ScriptInjection: Skipping - ContentType={ContentType} (not text/html)", contentType);
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Read the response body
                memoryStream.Position = 0;
                string responseBody;
                using (var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
                {
                    responseBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                // Check if already injected or empty
                if (string.IsNullOrEmpty(responseBody))
                {
                    _logger.LogDebug("ScriptInjection: Skipping - Response body is empty");
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                if (responseBody.Contains("ratings.js", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("ScriptInjection: Skipping - ratings.js already present in response");
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Inject the script before </body>
                // Try to get base path from PathBase first, then fall back to extracting from Path
                var basePath = context.Request.PathBase.Value?.TrimEnd('/') ?? string.Empty;

                // If PathBase is empty but path contains /web/, extract the base path
                // e.g., /jellyfin/web/index.html -> basePath = /jellyfin
                // We look for "/web/" specifically to avoid matching paths like /webapps/
                if (string.IsNullOrEmpty(basePath))
                {
                    // Try to find /web/ pattern (with trailing slash)
                    var webIndex = path.IndexOf("/web/", StringComparison.OrdinalIgnoreCase);
                    if (webIndex < 0)
                    {
                        // Also check for /web at the end (without trailing slash)
                        if (path.EndsWith("/web", StringComparison.OrdinalIgnoreCase))
                        {
                            webIndex = path.Length - 4; // "/web" is 4 characters
                        }
                    }

                    if (webIndex > 0)
                    {
                        basePath = path.Substring(0, webIndex);
                    }
                }

                _logger.LogDebug("ScriptInjection: Injecting script with basePath={BasePath}", basePath);

                var modifiedBody = InjectScript(responseBody, basePath);
                if (modifiedBody == responseBody)
                {
                    // Injection failed (no </body> found), write original
                    _logger.LogWarning("ScriptInjection: Failed to inject - no </body> tag found in response");
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                _logger.LogInformation("ScriptInjection: Successfully injected ratings.js script tag");

                // Write modified response
                var modifiedBytes = Encoding.UTF8.GetBytes(modifiedBody);

                // Clear and set new content length
                context.Response.Headers.Remove("Content-Length");
                context.Response.ContentLength = modifiedBytes.Length;

                await originalBodyStream.WriteAsync(modifiedBytes, 0, modifiedBytes.Length).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // On any error, try to write original response
                _logger.LogDebug(ex, "Script injection failed, passing through original response");
                try
                {
                    await WriteOriginalResponse(context.Response.Body as MemoryStream ?? new MemoryStream(), originalBodyStream).ConfigureAwait(false);
                }
                catch
                {
                    // Last resort - nothing we can do
                }
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private static async Task WriteOriginalResponse(MemoryStream memoryStream, Stream originalBodyStream)
        {
            if (memoryStream.Length > 0)
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream).ConfigureAwait(false);
            }
        }

        private static bool IsIndexHtmlRequest(string path)
        {
            // Strict matching - only exact paths or paths ending with these patterns
            // This handles both cases where PathBase is set and where it's not
            return path.Equals("/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase)
                // Handle base URL configurations where PathBase isn't properly stripped
                || path.EndsWith("/web", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
        }

        private static string InjectScript(string html, string basePath)
        {
            // Find </body> tag and inject before it
            var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyCloseIndex == -1)
            {
                // No </body> tag found, return unchanged
                return html;
            }

            // Build script tag with dynamic base path for reverse proxy support
            var scriptTag = $"<script defer src=\"{basePath}/Ratings/ratings.js\"></script>";
            return html.Insert(bodyCloseIndex, scriptTag + "\n");
        }
    }
}
