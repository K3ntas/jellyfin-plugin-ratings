using Jellyfin.Plugin.Ratings.Data;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
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

            // Register hosted service for JavaScript injection (though it doesn't work in Docker)
            serviceCollection.AddHostedService<JavaScriptInjectionService>();

            // Register notification service to monitor library for new media
            serviceCollection.AddHostedService<NotificationService>();
        }
    }
}
