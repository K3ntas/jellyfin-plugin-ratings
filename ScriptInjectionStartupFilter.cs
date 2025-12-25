using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Startup filter that registers the script injection middleware.
    /// This allows the plugin to inject JavaScript without requiring file system permissions.
    /// </summary>
    public class ScriptInjectionStartupFilter : IStartupFilter
    {
        private readonly ILogger<ScriptInjectionStartupFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptInjectionStartupFilter"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        public ScriptInjectionStartupFilter(ILogger<ScriptInjectionStartupFilter> logger)
        {
            _logger = logger;
            _logger.LogInformation("Ratings plugin: ScriptInjectionStartupFilter initialized - middleware injection enabled");
        }

        /// <summary>
        /// Configures the application to use the script injection middleware.
        /// </summary>
        /// <param name="next">The next configure action.</param>
        /// <returns>The configured action.</returns>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                _logger.LogInformation("Ratings plugin: Registering script injection middleware (works on all platforms including Docker)");

                // Register our middleware early in the pipeline
                // This ensures we can intercept the response before it's sent
                app.UseMiddleware<ScriptInjectionMiddleware>();

                // Continue with the rest of the middleware pipeline
                next(app);
            };
        }
    }
}
