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
        private const string InjectionMarker = "<!-- Ratings Plugin Injected -->";

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

            // Only intercept requests for index.html or root path
            if (!IsIndexHtmlRequest(path))
            {
                await _next(context);
                return;
            }

            // Capture the original response body
            var originalBodyStream = context.Response.Body;

            try
            {
                using var memoryStream = new MemoryStream();
                context.Response.Body = memoryStream;

                // Call the next middleware
                await _next(context);

                // Only process HTML responses
                var contentType = context.Response.ContentType ?? string.Empty;
                if (!contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(originalBodyStream);
                    return;
                }

                // Read the response body
                memoryStream.Position = 0;
                var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();

                // Check if already injected (either by file modification or previous middleware run)
                if (responseBody.Contains("Ratings Plugin", StringComparison.OrdinalIgnoreCase))
                {
                    // Already has the script, write original response
                    memoryStream.Position = 0;
                    await memoryStream.CopyToAsync(originalBodyStream);
                    return;
                }

                // Inject the script before </body>
                var modifiedBody = InjectScript(responseBody);

                // Write the modified response
                var modifiedBytes = Encoding.UTF8.GetBytes(modifiedBody);
                context.Response.ContentLength = modifiedBytes.Length;
                await originalBodyStream.WriteAsync(modifiedBytes);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private static bool IsIndexHtmlRequest(string path)
        {
            // Match root path, /index.html, or /web/index.html
            return path.Equals("/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/index.html", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/index.html", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase);
        }

        private static string InjectScript(string html)
        {
            // Find </body> tag and inject before it
            var bodyCloseIndex = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyCloseIndex == -1)
            {
                // No </body> tag found, append to end
                return html + $"\n{InjectionMarker}\n{ScriptTag}\n";
            }

            var injection = $"{InjectionMarker}\n{ScriptTag}\n";
            return html.Insert(bodyCloseIndex, injection);
        }
    }
}
