using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Ratings.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Flushes any debounced repository writes when the server shuts down.
    /// </summary>
    /// <remarks>
    /// Saves are coalesced (see <see cref="JsonFileWriter"/>), so at any instant there can be up to
    /// a debounce window's worth of changes held in memory. Without this, a clean server restart
    /// would silently discard them - the exact data loss the coalescing is otherwise safe from.
    /// </remarks>
    public class RepositoryFlushService : IHostedService
    {
        private readonly RatingsRepository _ratingsRepository;
        private readonly SocialRepository _socialRepository;
        private readonly ILogger<RepositoryFlushService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryFlushService"/> class.
        /// </summary>
        /// <param name="ratingsRepository">Ratings repository.</param>
        /// <param name="socialRepository">Social repository.</param>
        /// <param name="logger">Logger.</param>
        public RepositoryFlushService(
            RatingsRepository ratingsRepository,
            SocialRepository socialRepository,
            ILogger<RepositoryFlushService> logger)
        {
            _ratingsRepository = ratingsRepository;
            _socialRepository = socialRepository;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _ratingsRepository.FlushPendingWritesAsync().ConfigureAwait(false);
                await _socialRepository.FlushPendingWritesAsync().ConfigureAwait(false);
                _logger.LogInformation("Ratings: flushed pending data writes on shutdown");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Ratings: failed to flush pending data writes on shutdown");
            }
        }
    }
}
