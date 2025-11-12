using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Ratings.Api
{
    /// <summary>
    /// Controller for serving the ratings JavaScript file.
    /// </summary>
    [ApiController]
    [Route("Ratings")]
    [AllowAnonymous]
    public class ScriptController : ControllerBase
    {
        /// <summary>
        /// Serves the ratings.js file.
        /// </summary>
        /// <returns>The JavaScript file content.</returns>
        [HttpGet("ratings.js")]
        [Produces("application/javascript")]
        public ActionResult GetRatingsScript()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "Jellyfin.Plugin.Ratings.Web.ratings.js";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return NotFound($"Resource {resourceName} not found");
                }

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                return Content(content, "application/javascript");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading script: {ex.Message}");
            }
        }
    }
}
