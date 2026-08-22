using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// A user's viewing taste, expressed as watch time per genre.
    /// </summary>
    public sealed class GenreProfile
    {
        /// <summary>
        /// Gets minutes watched per genre.
        /// </summary>
        public Dictionary<string, double> MinutesByGenre { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets total minutes watched across all genres.
        /// </summary>
        public double TotalMinutes { get; set; }

        /// <summary>
        /// Gets or sets how many played items contributed.
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct movies the user has played.
        /// </summary>
        /// <remarks>
        /// Counted for every played movie, including ones carrying no genre, so this is a true
        /// "films watched" figure rather than the subset that happens to feed the genre chart.
        /// </remarks>
        public int MovieCount { get; set; }

        /// <summary>
        /// Gets or sets the number of distinct series the user has played an episode of.
        /// </summary>
        public int SeriesCount { get; set; }

        /// <summary>
        /// Gets or sets total minutes of played runtime, whether or not the item carried a genre.
        /// </summary>
        /// <remarks>
        /// <see cref="TotalMinutes"/> only counts genre-bearing items because the chart's
        /// percentages have to add up to it. Callers that want "time watched" want this instead.
        /// </remarks>
        public double PlayedMinutes { get; set; }

        /// <summary>
        /// Gets the ids of every played item counted here, so callers can fold in another source
        /// (ratings, say) without counting the same title twice.
        /// </summary>
        public HashSet<Guid> PlayedItemIds { get; } = new();

        /// <summary>
        /// Gets a value indicating whether there is enough data to be meaningful.
        /// </summary>
        public bool HasData => TotalMinutes > 0 && MinutesByGenre.Count > 0;
    }

    /// <summary>
    /// Works out what each user actually watches, by genre, and how closely two users' tastes line up.
    /// </summary>
    /// <remarks>
    /// The plugin previously derived every profile statistic from ratings, which only reflects what
    /// people bother to rate. This uses Jellyfin's own playback data instead, so it covers
    /// everything a user has actually watched.
    /// Results are cached per user: building a profile costs one indexed query plus a pass over
    /// that user's played items, and the similar-users list needs a profile for every user on the
    /// server. Without caching, opening a profile page would do that work every time.
    /// </remarks>
    public class GenreAffinityService
    {
        // Long enough that browsing profiles never recomputes, short enough that a user's chart
        // reflects an evening's viewing by the next day.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

        private static readonly ConcurrentDictionary<Guid, (DateTime Expires, GenreProfile Profile)> _cache = new();

        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly ILogger<GenreAffinityService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenreAffinityService"/> class.
        /// </summary>
        /// <param name="libraryManager">Library manager.</param>
        /// <param name="userManager">User manager.</param>
        /// <param name="logger">Logger.</param>
        public GenreAffinityService(ILibraryManager libraryManager, IUserManager userManager, ILogger<GenreAffinityService> logger)
        {
            _libraryManager = libraryManager;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets a user's genre watch-time profile, from cache when it is still fresh.
        /// </summary>
        /// <param name="userId">User id.</param>
        /// <returns>The profile (empty when the user has watched nothing).</returns>
        public GenreProfile GetProfile(Guid userId)
        {
            if (_cache.TryGetValue(userId, out var cached) && cached.Expires > DateTime.UtcNow)
            {
                return cached.Profile;
            }

            var profile = Build(userId);
            _cache[userId] = (DateTime.UtcNow.Add(CacheTtl), profile);
            return profile;
        }

        /// <summary>
        /// Drops a user's cached profile, so their next view recomputes.
        /// </summary>
        /// <param name="userId">User id.</param>
        public static void Invalidate(Guid userId) => _cache.TryRemove(userId, out _);

        /// <summary>
        /// Scores how alike two viewing profiles are, from 0 to 1.
        /// </summary>
        /// <remarks>
        /// Cosine similarity over the genre vectors. It compares the SHAPE of someone's taste
        /// rather than the volume, so a user with 2,000 hours watched and one with 50 can still be
        /// a strong match if they favour the same genres in the same proportions - which is the
        /// question being asked here ("do we like the same things?"), not "who watches most".
        /// </remarks>
        /// <param name="a">First profile.</param>
        /// <param name="b">Second profile.</param>
        /// <returns>Similarity between 0 and 1.</returns>
        public static double Similarity(GenreProfile a, GenreProfile b)
        {
            if (a == null || b == null || !a.HasData || !b.HasData)
            {
                return 0;
            }

            // Walk the smaller vector; genres missing from the other side contribute nothing.
            var (small, large) = a.MinutesByGenre.Count <= b.MinutesByGenre.Count ? (a, b) : (b, a);

            double dot = 0;
            foreach (var kv in small.MinutesByGenre)
            {
                if (large.MinutesByGenre.TryGetValue(kv.Key, out var other))
                {
                    dot += kv.Value * other;
                }
            }

            if (dot <= 0)
            {
                return 0;
            }

            var magA = Math.Sqrt(a.MinutesByGenre.Values.Sum(v => v * v));
            var magB = Math.Sqrt(b.MinutesByGenre.Values.Sum(v => v * v));
            if (magA <= 0 || magB <= 0)
            {
                return 0;
            }

            return Math.Clamp(dot / (magA * magB), 0, 1);
        }

        private GenreProfile Build(Guid userId)
        {
            var profile = new GenreProfile();

            try
            {
                var user = _userManager.GetUserById(userId);
                if (user == null)
                {
                    return profile;
                }

                // Ask Jellyfin for played items directly rather than walking the whole library and
                // checking each one - this is an indexed query.
                var query = new InternalItemsQuery(user)
                {
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
                    IsPlayed = true,
                    Recursive = true
                };

                var items = _libraryManager.GetItemList(query);
                if (items.Count == 0)
                {
                    return profile;
                }

                // Episodes carry no genres of their own - genre lives on the series. Resolve the
                // series in ONE batched query instead of a lookup per episode, which on a heavy TV
                // watcher would otherwise be thousands of queries.
                var seriesGenres = new Dictionary<Guid, string[]>();
                var seriesIds = items
                    .OfType<Episode>()
                    .Select(e => e.SeriesId)
                    .Where(id => !id.Equals(Guid.Empty))
                    .Distinct()
                    .ToArray();

                if (seriesIds.Length > 0)
                {
                    foreach (var series in _libraryManager.GetItemList(new InternalItemsQuery { ItemIds = seriesIds }))
                    {
                        seriesGenres[series.Id] = series.Genres ?? Array.Empty<string>();
                    }
                }

                // Counted separately from the genre tally below, which skips anything without a
                // genre: a film with no genre metadata is still a film the user watched.
                var playedSeries = new HashSet<Guid>();

                foreach (var item in items)
                {
                    var minutes = item.RunTimeTicks.HasValue
                        ? item.RunTimeTicks.Value / (double)TimeSpan.TicksPerMinute
                        : 0;

                    profile.PlayedItemIds.Add(item.Id);
                    profile.PlayedMinutes += minutes;

                    if (item is Movie)
                    {
                        profile.MovieCount++;
                    }
                    else if (item is Episode ep && !ep.SeriesId.Equals(Guid.Empty))
                    {
                        playedSeries.Add(ep.SeriesId);
                    }

                    if (minutes <= 0)
                    {
                        continue;
                    }

                    var genres = item.Genres;
                    if ((genres == null || genres.Length == 0)
                        && item is Episode episode
                        && seriesGenres.TryGetValue(episode.SeriesId, out var inherited))
                    {
                        genres = inherited;
                    }

                    if (genres == null || genres.Length == 0)
                    {
                        continue;
                    }

                    // Split the runtime across an item's genres rather than counting it once per
                    // genre. Otherwise a three-genre film contributes triple its length and the
                    // percentages in the chart would not add up to the time actually watched.
                    var share = minutes / genres.Length;

                    foreach (var genre in genres)
                    {
                        if (string.IsNullOrWhiteSpace(genre))
                        {
                            continue;
                        }

                        profile.MinutesByGenre.TryGetValue(genre, out var current);
                        profile.MinutesByGenre[genre] = current + share;
                    }

                    profile.TotalMinutes += minutes;
                    profile.ItemCount++;
                }

                profile.SeriesCount = playedSeries.Count;
            }
            catch (Exception ex)
            {
                // A profile page must still render if this fails.
                _logger.LogWarning(ex, "Could not build genre profile for user {UserId}", userId);
            }

            return profile;
        }
    }
}
