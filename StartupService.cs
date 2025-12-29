using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Startup service that injects the ratings JavaScript into Jellyfin's web client.
    /// </summary>
    public class StartupService : IScheduledTask
    {
        private readonly ILogger<StartupService> _logger;
        private readonly IApplicationPaths _appPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{StartupService}"/> interface.</param>
        /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        public StartupService(ILogger<StartupService> logger, IApplicationPaths appPaths)
        {
            _logger = logger;
            _appPaths = appPaths;
        }

        /// <inheritdoc />
        public string Name => "Ratings Plugin Startup";

        /// <inheritdoc />
        public string Key => "RatingsPluginStartup";

        /// <inheritdoc />
        public string Description => "Injects the ratings UI script into the Jellyfin web client.";

        /// <inheritdoc />
        public string Category => "Startup Services";

        /// <inheritdoc />
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    CleanupOldInjection();
                    InjectRatingsScript();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to inject ratings plugin JavaScript");
                }
            }, cancellationToken);
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Array.Empty<TaskTriggerInfo>();
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
                if (content.Contains("<!-- BEGIN Ratings Plugin -->"))
                {
                    return;
                }

                var startComment = "<!-- BEGIN Ratings Plugin -->";
                var endComment = "<!-- END Ratings Plugin -->";
                var scriptTag = "<script defer src=\"/Ratings/ratings.js\"></script>";

                var injectionBlock = $"{startComment}\n{scriptTag}\n{endComment}\n";

                if (content.Contains("</body>"))
                {
                    content = content.Replace("</body>", $"{injectionBlock}</body>");
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
