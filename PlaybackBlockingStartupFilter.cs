using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Startup filter that registers the playback blocking middleware.
    /// This ensures the middleware is added to the ASP.NET Core pipeline.
    /// </summary>
    public class PlaybackBlockingStartupFilter : IStartupFilter
    {
        private readonly ILogger<PlaybackBlockingStartupFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackBlockingStartupFilter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public PlaybackBlockingStartupFilter(ILogger<PlaybackBlockingStartupFilter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Configures the application to use the playback blocking middleware.
        /// </summary>
        /// <param name="next">The next configure action.</param>
        /// <returns>The configure action with middleware added.</returns>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            _logger.LogInformation("Ratings Plugin: Registering PlaybackBlockingMiddleware in the ASP.NET Core pipeline");

            return app =>
            {
                // Add our middleware early in the pipeline to intercept playback requests
                app.UseMiddleware<PlaybackBlockingMiddleware>();

                // Continue with the rest of the pipeline
                next(app);
            };
        }
    }
}
