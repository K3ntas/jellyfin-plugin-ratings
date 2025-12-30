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
        private const string ScriptTag = "<script defer src=\"/Ratings/ratings.js\"></script>";

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

            // Only intercept exact index.html requests (strict matching)
            if (!IsIndexHtmlRequest(path))
            {
                await _next(context).ConfigureAwait(false);
                return;
            }

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
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Check for compression (shouldn't happen but safety check)
                var contentEncoding = context.Response.Headers.ContentEncoding.ToString();
                if (!string.IsNullOrEmpty(contentEncoding))
                {
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Only process text/html
                var contentType = context.Response.ContentType ?? string.Empty;
                if (!contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
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
                if (string.IsNullOrEmpty(responseBody) ||
                    responseBody.Contains("ratings.js", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

                // Inject the script before </body>
                var modifiedBody = InjectScript(responseBody);
                if (modifiedBody == responseBody)
                {
                    // Injection failed (no </body> found), write original
                    await WriteOriginalResponse(memoryStream, originalBodyStream).ConfigureAwait(false);
                    return;
                }

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
            // Strict matching - only exact paths
            return path.Equals("/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase);
        }

        private static string InjectScript(string html)
        {
            // Find </body> tag and inject before it
            var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyCloseIndex == -1)
            {
                // No </body> tag found, return unchanged
                return html;
            }

            return html.Insert(bodyCloseIndex, ScriptTag + "\n");
        }
    }
}
