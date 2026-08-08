using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings.Data
{
    /// <summary>
    /// Persists whole-file JSON snapshots atomically, coalescing rapid successive saves of the
    /// same file into a single write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both repositories keep their data in memory and rewrite an entire JSON file on every
    /// mutation. That makes a burst of changes quadratically expensive: importing 50 ratings meant
    /// 50 full serializations and 50 full file writes, and a heartbeat from every online user meant
    /// one complete rewrite of online_statuses.json per user per interval.
    /// </para>
    /// <para>
    /// Since each write replaces the whole file, only the newest snapshot matters - an older
    /// pending one can simply be dropped. Saves are therefore held for a short debounce window and
    /// the most recent snapshot wins. <see cref="FlushAsync"/> must be called on shutdown so the
    /// debounce window cannot lose the final write, and before anything that reads these files
    /// directly from disk (backup export).
    /// </para>
    /// <para>
    /// Writes go to a temp file which is then renamed. File.WriteAllTextAsync truncates in place,
    /// so a crash mid-write previously left a truncated file that the loader could not parse -
    /// silently starting from empty on the next boot.
    /// </para>
    /// </remarks>
    public sealed class JsonFileWriter : IDisposable
    {
        // A fresh JsonSerializerOptions per call defeats System.Text.Json's metadata cache and
        // makes every write measurably slower, so all persistence shares one instance.
        // WriteIndented is off: these files are machine-read only and pretty-printing cost roughly
        // 30-40% extra bytes to serialize and write on every mutation.
        private static readonly JsonSerializerOptions PersistOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        private readonly string _dataPath;
        private readonly ILogger _logger;
        private readonly string _logPrefix;
        private readonly int _debounceMs;
        private readonly ConcurrentDictionary<string, PendingWrite> _pending = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonFileWriter"/> class.
        /// </summary>
        /// <param name="dataPath">Directory the files live in.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="logPrefix">Prefix for log messages (e.g. "[Social] ").</param>
        /// <param name="debounceMs">How long to hold a save waiting for a newer one.</param>
        public JsonFileWriter(string dataPath, ILogger logger, string logPrefix = "", int debounceMs = 1500)
        {
            _dataPath = dataPath;
            _logger = logger;
            _logPrefix = logPrefix;
            _debounceMs = debounceMs;
        }

        /// <summary>
        /// Queues a snapshot to be written, superseding any snapshot still pending for that file.
        /// </summary>
        /// <typeparam name="T">Snapshot type.</typeparam>
        /// <param name="fileName">File name within the data directory.</param>
        /// <param name="snapshot">Already-captured snapshot. Must not be mutated afterwards.</param>
        /// <param name="label">Human-readable label for log messages.</param>
        public void Queue<T>(string fileName, T snapshot, string label)
        {
            if (_disposed)
            {
                return;
            }

            var entry = _pending.GetOrAdd(fileName, name => new PendingWrite(name));

            lock (entry.Sync)
            {
                entry.Label = label;

                // Capture the serialization rather than the serialized bytes: if three saves land
                // inside the debounce window we only pay for one Serialize call, the last one.
                entry.Serialize = () => JsonSerializer.Serialize(snapshot, PersistOptions);
                entry.HasPending = true;

                entry.Timer ??= new Timer(OnTimer, entry, Timeout.Infinite, Timeout.Infinite);
                entry.Timer.Change(_debounceMs, Timeout.Infinite);
            }
        }

        /// <summary>
        /// Writes every pending snapshot immediately and waits for completion.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task FlushAsync()
        {
            var tasks = _pending.Values.Select(WriteNowAsync).ToList();
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var entry in _pending.Values)
            {
                lock (entry.Sync)
                {
                    entry.Timer?.Dispose();
                    entry.Timer = null;
                }
            }
        }

        private void OnTimer(object? state)
        {
            if (state is PendingWrite entry)
            {
                _ = WriteNowAsync(entry);
            }
        }

        private async Task WriteNowAsync(PendingWrite entry)
        {
            Func<string>? serialize;

            lock (entry.Sync)
            {
                if (!entry.HasPending)
                {
                    return;
                }

                entry.HasPending = false;
                entry.Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                serialize = entry.Serialize;
            }

            if (serialize == null)
            {
                return;
            }

            // One writer per file at a time, so a slow write cannot interleave with the next one
            // and leave the newer content overwritten by the older.
            await entry.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var path = Path.Combine(_dataPath, entry.FileName);
                var tempPath = path + ".tmp";

                // Serialization happens here, off any caller's data lock.
                var json = serialize();
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

                // Atomic replace: either the old file or the complete new one is visible.
                File.Move(tempPath, path, true);

                _logger.LogDebug("{Prefix}Saved {Label} to disk", _logPrefix, entry.Label);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Prefix}Error saving {Label} to disk", _logPrefix, entry.Label);
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        private sealed class PendingWrite
        {
            public PendingWrite(string fileName)
            {
                FileName = fileName;
            }

            public string FileName { get; }

            public object Sync { get; } = new object();

            public SemaphoreSlim Gate { get; } = new SemaphoreSlim(1, 1);

            public Timer? Timer { get; set; }

            public Func<string>? Serialize { get; set; }

            public bool HasPending { get; set; }

            public string Label { get; set; } = string.Empty;
        }
    }
}
