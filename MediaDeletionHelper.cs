using System;
using System.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Removes the derived data Jellyfin generates for an item but does not clean up when the
    /// item's files are deleted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ILibraryManager.DeleteItem(item, new DeleteOptions { DeleteFileLocation = true })</c>
    /// removes the media file (or its folder) and the library row, but the extracted data folders
    /// live outside that folder - trickplay tiles under the server's trickplay path, plus extracted
    /// subtitles and attachments - so they were left behind forever, growing without bound
    /// (issue #70).
    /// </para>
    /// <para>
    /// Paths come from Jellyfin's own <see cref="IPathManager"/> rather than being reconstructed
    /// here, so they stay correct if the server changes its layout (it already moved trickplay
    /// files once, in the 10.10 -> 10.11 migration).
    /// </para>
    /// </remarks>
    public static class MediaDeletionHelper
    {
        /// <summary>
        /// Deletes trickplay, extracted subtitle and attachment folders belonging to an item.
        /// </summary>
        /// <remarks>
        /// Call this BEFORE deleting the item: <see cref="IPathManager.GetExtractedDataPaths"/>
        /// needs the item's media sources, which are gone once the library row is removed.
        /// </remarks>
        /// <param name="pathManager">Jellyfin path manager.</param>
        /// <param name="item">The item being deleted.</param>
        /// <param name="logger">Logger.</param>
        public static void DeleteExtractedData(IPathManager? pathManager, BaseItem item, ILogger logger)
        {
            if (pathManager == null || item == null)
            {
                return;
            }

            try
            {
                foreach (var path in pathManager.GetExtractedDataPaths(item))
                {
                    TryDeleteDirectory(path, logger);
                }
            }
            catch (Exception ex)
            {
                // Never let cleanup failure stop the actual deletion.
                logger.LogWarning(ex, "Could not enumerate extracted data paths for {ItemName}", item.Name);
            }

            // When the library is configured to save trickplay next to the media, the tiles sit in
            // a sibling "<file>.trickplay" folder. Deleting a single movie file leaves that behind.
            try
            {
                var withMedia = pathManager.GetTrickplayDirectory(item, true);
                TryDeleteDirectory(withMedia, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not resolve saved-with-media trickplay path for {ItemName}", item.Name);
            }
        }

        private static void TryDeleteDirectory(string? path, ILogger logger)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    logger.LogInformation("Removed leftover media data: {Path}", path);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not remove leftover media data at {Path}", path);
            }
        }
    }
}
