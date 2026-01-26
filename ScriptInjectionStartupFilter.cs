using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Startup filter that registers the script injection middleware.
    /// This ensures the middleware is added to the ASP.NET Core pipeline.
    /// </summary>
    public class ScriptInjectionStartupFilter : IStartupFilter
    {
        private readonly ILogger<ScriptInjectionStartupFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptInjectionStartupFilter"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public ScriptInjectionStartupFilter(ILogger<ScriptInjectionStartupFilter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Configures the application to use the script injection middleware.
        /// </summary>
        /// <param name="next">The next configure action.</param>
        /// <returns>The configure action with middleware added.</returns>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            _logger.LogInformation("Ratings Plugin: Registering ScriptInjectionMiddleware in the ASP.NET Core pipeline");

            return app =>
            {
                // Add our middleware early in the pipeline to intercept responses
                app.UseMiddleware<ScriptInjectionMiddleware>();

                // Continue with the rest of the pipeline
                next(app);
            };
        }
    }
}
