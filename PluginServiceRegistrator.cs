using Jellyfin.Plugin.Ratings.Data;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Registers services for the Ratings plugin.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // Register RatingsRepository as a singleton
            serviceCollection.AddSingleton<RatingsRepository>();

            // PRIMARY: Register HTTP middleware for script injection
            // This works on ALL platforms (Docker, Linux, Windows) without requiring file write permissions
            // The middleware intercepts HTML responses and injects the script tag dynamically
            serviceCollection.AddTransient<IStartupFilter, ScriptInjectionStartupFilter>();

            // FALLBACK: Keep file-based injection for backward compatibility
            // This is disabled by default but can be enabled in plugin configuration
            // It's only useful for systems where the middleware approach somehow fails
            serviceCollection.AddHostedService<JavaScriptInjectionService>();

            // Register notification service to monitor library for new media
            serviceCollection.AddHostedService<NotificationService>();
        }
    }
}
