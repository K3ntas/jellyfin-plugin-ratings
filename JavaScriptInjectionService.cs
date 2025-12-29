using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Background service that injects the ratings JavaScript into Jellyfin's web client on startup.
    /// </summary>
    public class JavaScriptInjectionService : IHostedService
    {
        private readonly ILogger<JavaScriptInjectionService> _logger;
        private readonly IApplicationPaths _appPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="JavaScriptInjectionService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{JavaScriptInjectionService}"/> interface.</param>
        /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        public JavaScriptInjectionService(ILogger<JavaScriptInjectionService> logger, IApplicationPaths appPaths)
        {
            _logger = logger;
            _appPaths = appPaths;
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Add a small delay to ensure web files are loaded
                    Thread.Sleep(2000);

                    CleanupOldInjection();
                    InjectRatingsScript();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ratings plugin JavaScript injection failed");
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private void CleanupOldInjection()
        {
            var indexPath = Path.Combine(_appPaths.WebPath, "index.html");
            if (!File.Exists(indexPath))
            {
                return;
            }

            try
            {
                var content = File.ReadAllText(indexPath);
                var startComment = Regex.Escape("<!-- BEGIN Ratings Plugin -->");
                var endComment = Regex.Escape("<!-- END Ratings Plugin -->");

                var cleanupRegex = new Regex($"{startComment}[\\s\\S]*?{endComment}\\s*", RegexOptions.Multiline);

                if (cleanupRegex.IsMatch(content))
                {
                    content = cleanupRegex.Replace(content, string.Empty);
                    File.WriteAllText(indexPath, content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old injection from index.html");
            }
        }

        private void InjectRatingsScript()
        {
            var indexPath = Path.Combine(_appPaths.WebPath, "index.html");
            if (!File.Exists(indexPath))
            {
                _logger.LogError("index.html not found at: {Path}", indexPath);
                return;
            }

            try
            {
                var content = File.ReadAllText(indexPath);

                // Check if already injected (in case cleanup failed)
                if (content.Contains("<!-- BEGIN Ratings Plugin -->", StringComparison.Ordinal))
                {
                    return;
                }

                var startComment = "<!-- BEGIN Ratings Plugin -->";
                var endComment = "<!-- END Ratings Plugin -->";
                var scriptTag = "<script defer src=\"/Ratings/ratings.js\"></script>";

                var injectionBlock = $"{startComment}\n{scriptTag}\n{endComment}\n";

                if (content.Contains("</body>", StringComparison.Ordinal))
                {
                    content = content.Replace("</body>", $"{injectionBlock}</body>", StringComparison.Ordinal);
                    File.WriteAllText(indexPath, content);
                }
                else
                {
                    _logger.LogError("Could not find </body> tag in index.html");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Permission denied when trying to modify index.html");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inject script into index.html");
            }
        }
    }
}
