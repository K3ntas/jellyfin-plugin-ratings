using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Background service that injects the ratings JavaScript into Jellyfin's web client on startup.
    /// This is a FALLBACK method - the primary injection is done via HTTP middleware.
    /// File injection works on systems where Jellyfin has write access to jellyfin-web folder.
    /// </summary>
    public class JavaScriptInjectionService : IHostedService
    {
        private readonly ILogger<JavaScriptInjectionService> _logger;
        private readonly IApplicationPaths _appPaths;

        /// <summary>
        /// Initializes a new instance of the <see cref="JavaScriptInjectionService"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{JavaScriptInjectionService}"/> interface.</param>
        /// <param name="appPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        public JavaScriptInjectionService(ILogger<JavaScriptInjectionService> logger, IApplicationPaths appPaths)
        {
            _logger = logger;
            _appPaths = appPaths;
        }

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Remove stale duplicate plugin folders first (prevents the "2 versions
                    // installed / restart required" loop on every update).
                    CleanupOldPluginVersions();

                    // Add a small delay to ensure web files are loaded
                    Thread.Sleep(2000);

                    // Only CLEAN now - we deliberately no longer write our tag into index.html.
                    // See CleanupOldInjection for why (issue #66). This also removes the tag left
                    // behind by older versions of the plugin.
                    CleanupOldInjection();
                }
                catch
                {
                    // Silent failure - middleware will handle injection if file method fails
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        // Our plugin's stable identity (must match Plugin.Id).
        private const string PluginGuid = "a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d";

        /// <summary>
        /// Removes older duplicate installs of THIS plugin from the plugins directory.
        /// Jellyfin can leave the previous versioned folder behind on update (especially when the
        /// repository/manifest name differs from the plugin name), producing the recurring
        /// "two versions installed / restart required" problem. This deletes only folders that
        /// carry our exact GUID and a strictly-older version than the running one. It never touches
        /// other plugins (different GUID) nor the folder we are currently running from.
        /// </summary>
        private void CleanupOldPluginVersions()
        {
            try
            {
                var pluginsDir = _appPaths.PluginsPath;
                var myVersion = typeof(Plugin).Assembly.GetName().Version;

                // Folder the running assembly lives in - must never be deleted.
                string? currentDir = null;
                try
                {
                    var loc = typeof(Plugin).Assembly.Location;
                    if (!string.IsNullOrEmpty(loc))
                    {
                        currentDir = Path.GetFullPath(Path.GetDirectoryName(loc) !);
                    }
                }
                catch
                {
                    currentDir = null;
                }

                _logger.LogInformation(
                    "Ratings cleanup: scanning '{Dir}' (running version {Ver}, current folder '{Cur}')",
                    pluginsDir, myVersion, currentDir ?? "(unknown)");

                if (string.IsNullOrEmpty(pluginsDir) || !Directory.Exists(pluginsDir) || myVersion == null)
                {
                    _logger.LogInformation("Ratings cleanup: nothing to do (plugins dir missing or version unknown)");
                    return;
                }

                int ours = 0, removed = 0, failed = 0;

                foreach (var dir in Directory.GetDirectories(pluginsDir))
                {
                    // Extra guard: only consider folders that look like ours.
                    var folderName = Path.GetFileName(dir);
                    if (folderName == null || folderName.IndexOf("Ratings", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    Version? otherVersion = null;
                    try
                    {
                        var metaPath = Path.Combine(dir, "meta.json");
                        if (!File.Exists(metaPath))
                        {
                            continue;
                        }

                        using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                        if (!doc.RootElement.TryGetProperty("guid", out var guidEl)
                            || !string.Equals(guidEl.GetString(), PluginGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // not our plugin - leave it alone
                        }

                        ours++;

                        // Never delete the folder we are running from.
                        if (currentDir != null &&
                            string.Equals(Path.GetFullPath(dir), currentDir, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Ratings cleanup: keeping current folder '{Dir}'", dir);
                            continue;
                        }

                        // Only delete STRICTLY older versions, so we can never remove the current/newest.
                        if (!doc.RootElement.TryGetProperty("version", out var verEl)
                            || !Version.TryParse(verEl.GetString(), out otherVersion)
                            || otherVersion >= myVersion)
                        {
                            _logger.LogInformation(
                                "Ratings cleanup: keeping folder '{Dir}' (version {Other} not older than {Current})",
                                dir, otherVersion, myVersion);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ratings cleanup: could not evaluate plugin folder '{Dir}'", dir);
                        continue;
                    }

                    // Separate try so a Windows file-lock on the old DLL is reported clearly.
                    try
                    {
                        Directory.Delete(dir, true);
                        removed++;
                        _logger.LogInformation(
                            "Ratings cleanup: removed stale duplicate plugin folder '{Dir}' (version {Old}, keeping {Current})",
                            dir, otherVersion, myVersion);
                    }
                    catch (Exception ex)
                    {
                        // On Windows the old DLL is usually still mapped into the process, so the
                        // folder cannot be deleted until the next restart. Deleting the folder's
                        // meta.json is normally still allowed, and that is what Jellyfin reads to
                        // decide a plugin is installed - so removing it stops the stale version
                        // being listed and loaded, even while the DLL itself lingers (issue #67).
                        var neutralized = TryNeutralizePluginFolder(dir);

                        if (neutralized)
                        {
                            removed++;
                            _logger.LogInformation(
                                "Ratings cleanup: could not delete '{Dir}' (locked DLL), but removed its meta.json so the stale version is no longer registered; the folder itself will go on a later restart",
                                dir);
                        }
                        else
                        {
                            failed++;
                            _logger.LogWarning(ex,
                                "Ratings cleanup: FAILED to remove old folder '{Dir}' (likely a locked DLL on Windows); will retry next restart",
                                dir);
                        }
                    }
                }

                _logger.LogInformation(
                    "Ratings cleanup: done. ourFolders={Ours}, removed={Removed}, failed={Failed}",
                    ours, removed, failed);
            }
            catch (Exception ex)
            {
                // Cleanup is best-effort and must never break startup.
                _logger.LogWarning(ex, "Ratings cleanup: plugin version cleanup failed");
            }
        }

        /// <summary>
        /// Last resort when a stale plugin folder cannot be deleted: strip everything deletable
        /// inside it, above all meta.json.
        /// </summary>
        /// <remarks>
        /// Jellyfin identifies an installed plugin by the meta.json in its folder. With that file
        /// gone the stale version stops appearing in the plugin list and stops being loaded, which
        /// is the visible half of the "two versions installed / restart required" loop, even though
        /// the locked DLL itself has to wait for a restart (issue #67).
        /// </remarks>
        /// <param name="dir">Plugin folder that could not be deleted.</param>
        /// <returns>True if meta.json is gone when this returns.</returns>
        private bool TryNeutralizePluginFolder(string dir)
        {
            var metaPath = Path.Combine(dir, "meta.json");

            try
            {
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ratings cleanup: could not remove '{Meta}'", metaPath);
                return false;
            }

            // Best effort: clear whatever else will delete, so the leftover folder is as small as
            // possible until a restart lets it go entirely.
            try
            {
                foreach (var file in Directory.GetFiles(dir))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Locked (typically the DLL itself) - leave it for the next restart.
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ratings cleanup: partial cleanup of '{Dir}' incomplete", dir);
            }

            return !File.Exists(metaPath);
        }

        /// <summary>
        /// Removes any Ratings plugin block previously written into jellyfin-web/index.html.
        /// </summary>
        /// <remarks>
        /// The plugin used to write its script tag directly into index.html as a "fallback"
        /// alongside the middleware. That file is part of jellyfin-web, not the plugin, so nothing
        /// removed the tag when the plugin was uninstalled: the header buttons kept appearing on
        /// every client until each user manually cleared their cache, and there was no way for an
        /// admin to fix it server-side (issue #66).
        /// Injection is now middleware-only (ScriptInjectionMiddleware), which disappears with the
        /// plugin, so uninstalling really does remove the UI. This method still runs on every start
        /// so servers upgrading from an older version get their index.html cleaned up.
        /// </remarks>
        private void CleanupOldInjection()
        {
            var indexPath = Path.Combine(_appPaths.WebPath, "index.html");
            if (!File.Exists(indexPath))
            {
                return;
            }

            try
            {
                var content = File.ReadAllText(indexPath);
                var startComment = Regex.Escape("<!-- BEGIN Ratings Plugin -->");
                var endComment = Regex.Escape("<!-- END Ratings Plugin -->");

                var cleanupRegex = new Regex($"{startComment}[\\s\\S]*?{endComment}\\s*", RegexOptions.Multiline);

                if (cleanupRegex.IsMatch(content))
                {
                    content = cleanupRegex.Replace(content, string.Empty);
                    File.WriteAllText(indexPath, content);
                }
            }
            catch
            {
                // Silent failure - not critical, middleware handles injection
            }
        }

    }
}
