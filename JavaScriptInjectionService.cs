using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                    var metaPath = Path.Combine(dir, "meta.json");
                    try
                    {
                        if (File.Exists(metaPath))
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                            if (!doc.RootElement.TryGetProperty("guid", out var guidEl)
                                || !string.Equals(guidEl.GetString(), PluginGuid, StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // not our plugin - leave it alone
                            }

                            if (doc.RootElement.TryGetProperty("version", out var verEl))
                            {
                                Version.TryParse(verEl.GetString(), out otherVersion);
                            }
                        }
                        else if (File.Exists(Path.Combine(dir, PluginAssemblyFile)))
                        {
                            // One of ours with no meta.json. Older builds of this plugin deleted
                            // that file to "deregister" a folder whose DLL was locked, which does
                            // not work: Jellyfin does not treat a plugin as gone when meta.json is
                            // missing, it synthesises an Active manifest from the folder name and
                            // loads the assembly anyway. The result is two live copies of this
                            // plugin - every controller, hosted service and middleware registered
                            // twice, and the stale DLL able to answer requests (issue #67). Such a
                            // folder has to be recognised and finished off, not skipped.
                            otherVersion = VersionFromFolderName(folderName);
                            _logger.LogInformation(
                                "Ratings cleanup: folder '{Dir}' has no meta.json; treating it as version {Ver} left behind by an earlier cleanup",
                                dir,
                                otherVersion);
                        }
                        else
                        {
                            continue; // no manifest and no assembly of ours - not our business
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
                        if (otherVersion == null || otherVersion >= myVersion)
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
                        // folder cannot be deleted until the next restart. Mark it Deleted in its
                        // own manifest instead: Jellyfin skips any plugin whose status is below
                        // Active, and retries removing a Deleted one at every restart - which is
                        // what its own uninstall does for a folder it cannot delete (issue #67).
                        var markedDeleted = TryMarkFolderDeleted(dir, metaPath, otherVersion, folderName);

                        if (markedDeleted)
                        {
                            removed++;
                            _logger.LogInformation(
                                "Ratings cleanup: could not delete '{Dir}' (locked DLL), so marked it Deleted in meta.json - Jellyfin will not load it, and removes the folder on a later restart",
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
        /// Name of this plugin's assembly, used to recognise one of our folders when its meta.json
        /// has gone missing.
        /// </summary>
        private const string PluginAssemblyFile = "Jellyfin.Plugin.Ratings.dll";

        /// <summary>
        /// Reads the version out of a plugin folder name the same way Jellyfin does for a folder
        /// with no manifest: whatever follows the last underscore.
        /// </summary>
        /// <param name="folderName">Folder name, for example "Ratings_1.0.359.0".</param>
        /// <returns>The version, or null when the name does not carry one.</returns>
        private static Version? VersionFromFolderName(string folderName)
        {
            var underscore = folderName.LastIndexOf('_');
            if (underscore < 0 || underscore == folderName.Length - 1)
            {
                return null;
            }

            return Version.TryParse(folderName.AsSpan(underscore + 1), out var parsed) ? parsed : null;
        }

        /// <summary>
        /// Last resort when a stale plugin folder cannot be deleted: mark it Deleted in its own
        /// manifest so Jellyfin stops loading it.
        /// </summary>
        /// <remarks>
        /// An earlier attempt at this deleted meta.json outright, on the assumption that Jellyfin
        /// decides a plugin is installed by the presence of that file. It does not: a folder with
        /// no manifest gets an <c>Active</c> one synthesised from its name, so the stale assembly
        /// was still loaded - a second live copy of the plugin, with every controller and hosted
        /// service registered twice - and no meta.json was left for this cleanup to recognise the
        /// folder by on the next restart.
        /// <para>
        /// Writing <c>"status": "Deleted"</c> is what Jellyfin's own uninstall does for a folder it
        /// cannot remove. Any status below Active is skipped at load, and a Deleted one is retried
        /// for removal at every restart, so the folder goes on its own once the DLL is unmapped.
        /// </para>
        /// </remarks>
        /// <param name="dir">Plugin folder that could not be deleted.</param>
        /// <param name="metaPath">Path to that folder's meta.json, which may not exist.</param>
        /// <param name="version">Version of the stale folder, for a manifest that has to be rebuilt.</param>
        /// <param name="folderName">Folder name, used as the plugin name when rebuilding.</param>
        /// <returns>True if the folder is now marked as not to be loaded.</returns>
        private bool TryMarkFolderDeleted(string dir, string metaPath, Version? version, string folderName)
        {
            try
            {
                JsonObject manifest;

                if (File.Exists(metaPath))
                {
                    // Keep every field the installer wrote - above all the guid, which is how this
                    // cleanup recognises the folder next time - and change only the status.
                    manifest = JsonNode.Parse(File.ReadAllText(metaPath)) as JsonObject ?? new JsonObject();
                }
                else
                {
                    // Left without a manifest by an older build of this plugin. Rebuild enough of
                    // one that Jellyfin reads it instead of synthesising an Active manifest.
                    var underscore = folderName.LastIndexOf('_');
                    manifest = new JsonObject
                    {
                        ["guid"] = PluginGuid,
                        ["name"] = underscore > 0 ? folderName.Substring(0, underscore) : folderName,
                        ["version"] = (version ?? new Version(0, 0, 0, 0)).ToString(),
                        ["owner"] = "K3ntas",
                        ["description"] = "Superseded copy, pending removal"
                    };
                }

                manifest["status"] = "Deleted";

                File.WriteAllText(metaPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return true;
            }
            catch (Exception ex)
            {
                // Leave the folder exactly as it is. A stale folder that still holds its original
                // manifest is the lesser problem: Jellyfin sees two versions of one plugin and
                // supersedes the older itself. Breaking the manifest would cost us that.
                _logger.LogWarning(ex, "Ratings cleanup: could not mark '{Dir}' as deleted", dir);
                return false;
            }
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
