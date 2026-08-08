using Jellyfin.Plugin.Ratings.Api;
using Jellyfin.Plugin.Ratings.Data;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Net;
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

            // Register SocialRepository for social features (friends, profiles, etc.)
            serviceCollection.AddSingleton<SocialRepository>();

            // Computes per-user genre watch-time profiles for the taste chart and user matching.
            // Singleton so its cache is shared - building a profile is an indexed library query
            // per user, and matching needs one for everybody on the server.
            serviceCollection.AddSingleton<GenreAffinityService>();

            // Register WebSocket listener for real-time social updates (Jellyfin's IWebSocketListener)
            serviceCollection.AddSingleton<SocialWebSocketListener>();
            serviceCollection.AddSingleton<IWebSocketListener>(sp => sp.GetRequiredService<SocialWebSocketListener>());

            // Register middleware startup filter for script injection (works without file permissions)
            serviceCollection.AddSingleton<IStartupFilter, ScriptInjectionStartupFilter>();

            // Register middleware for blocking media playback for banned users
            serviceCollection.AddSingleton<IStartupFilter, PlaybackBlockingStartupFilter>();

            // Register hosted service for JavaScript injection as fallback (for setups where file modification works)
            serviceCollection.AddHostedService<JavaScriptInjectionService>();

            // Register notification service to monitor library for new media
            serviceCollection.AddHostedService<NotificationService>();

            // Register deletion service for scheduled media deletions
            serviceCollection.AddHostedService<DeletionService>();

            // Flushes debounced repository writes on shutdown so coalescing cannot lose the last
            // few seconds of changes across a restart.
            serviceCollection.AddHostedService<RepositoryFlushService>();
        }
    }
}
