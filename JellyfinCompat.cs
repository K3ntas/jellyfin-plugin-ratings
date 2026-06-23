using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Ratings
{
    /// <summary>
    /// Version-agnostic access to Jellyfin APIs that changed binary signature within the 10.11.x
    /// line. Jellyfin replaced the <c>IUserManager.Users</c> property with a <c>GetUsers()</c>
    /// method in a 10.11 patch release, so a plugin compiled against either form throws
    /// <see cref="MissingMethodException"/> on the other. We resolve whichever member the running
    /// server actually exposes via reflection, so the plugin works on every 10.11.x build.
    /// </summary>
    internal static class JellyfinCompat
    {
        private static Func<IUserManager, IEnumerable<User>>? _usersAccessor;

        /// <summary>
        /// Enumerates all Jellyfin users, working on every 10.11.x server build.
        /// </summary>
        /// <param name="userManager">The user manager.</param>
        /// <returns>All users, or an empty sequence if neither accessor is present.</returns>
        public static IEnumerable<User> GetAllUsers(IUserManager userManager)
        {
            if (userManager == null)
            {
                return Enumerable.Empty<User>();
            }

            var accessor = _usersAccessor ??= ResolveUsersAccessor(userManager.GetType());
            try
            {
                return accessor(userManager) ?? Enumerable.Empty<User>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<User>();
            }
        }

        private static Func<IUserManager, IEnumerable<User>> ResolveUsersAccessor(Type concreteType)
        {
            // Newer Jellyfin (10.11.10+): IEnumerable<User> GetUsers().
            var method = concreteType.GetMethod("GetUsers", Type.EmptyTypes);
            if (method != null && typeof(IEnumerable<User>).IsAssignableFrom(method.ReturnType))
            {
                return um => (IEnumerable<User>)method.Invoke(um, null)!;
            }

            // Older Jellyfin (<= 10.11.9): IEnumerable<User> Users { get; }.
            var prop = concreteType.GetProperty("Users");
            if (prop != null && typeof(IEnumerable<User>).IsAssignableFrom(prop.PropertyType))
            {
                return um => (IEnumerable<User>)prop.GetValue(um)!;
            }

            return _ => Enumerable.Empty<User>();
        }
    }
}
