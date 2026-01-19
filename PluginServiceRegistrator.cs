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

            // Register middleware startup filter for script injection (works without file permissions)
            serviceCollection.AddSingleton<IStartupFilter, ScriptInjectionStartupFilter>();

            // Register hosted service for JavaScript injection as fallback (for setups where file modification works)
            serviceCollection.AddHostedService<JavaScriptInjectionService>();

            // Register notification service to monitor library for new media
            serviceCollection.AddHostedService<NotificationService>();

            // Register deletion service for scheduled media deletions
            serviceCollection.AddHostedService<DeletionService>();
        }
    }
}
