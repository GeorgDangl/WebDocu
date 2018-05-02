using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly IProjectFilesService _projectFilesService;
        private readonly IProjectVersionsService _projectVersionsService;
        private readonly IProjectsService _projectsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(UserManager<ApplicationUser> userManager,
            IProjectFilesService projectFilesService,
            IProjectVersionsService projectVersionsService,
            IProjectsService projectsService)
        {
            _projectFilesService = projectFilesService;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _userManager = userManager;
        }

        /// <summary>
        /// Returns a requested file for the project if the user has access or the project is public.
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="version"></param>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Projects/{projectName}/{version}/{*pathToFile}")]
        public async Task<IActionResult> GetFile(string projectName, string version, string pathToFile)
        {
            var userId = User == null ? null : _userManager.GetUserId(User);
            var hasProjectAccess = await _projectsService.UserHasAccessToProject(projectName, userId);
            if (!hasProjectAccess)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(pathToFile))
            {
                // If not pathToFile is given, the response should redirect to the entry point for the project
                var pathToEntryPoint = await _projectFilesService.GetEntryFilePathForProject(projectName);
                return RedirectToAction(nameof(GetFile), new {projectName, version, pathToFile = pathToEntryPoint});
            }

            var availableVersions = await _projectVersionsService.GetProjectVersionsAsync(projectName);
            if (String.Equals(version, "latest", StringComparison.CurrentCultureIgnoreCase))
            {
                version = availableVersions.FirstOrDefault();
            }
            else
            {
                if (!availableVersions.Contains(version))
                {
                    // This makes sure that deleted earlier versions, e.g. prerelease versions,
                    // do not return 404 after they're deleted but get redirect to the next available version
                    var versionsOrderer = new SemanticVersionsOrderer(availableVersions);
                    var nextHigherVersion = versionsOrderer.GetNextHigherVersionOrNull(version);
                    if (nextHigherVersion == null)
                    {
                        return NotFound();
                    }

                    return RedirectToAction(nameof(GetFile), new { projectName, version = nextHigherVersion, pathToFile });
                }
            }

            var projectFile = await _projectFilesService.GetFileForProject(projectName, version, pathToFile);
            if (projectFile == null)
            {
                var pathToEntryPoint = await _projectFilesService.GetEntryFilePathForProject(projectName);
                if (pathToEntryPoint == pathToFile)
                {
                    return NotFound();
                }
                return RedirectToAction(nameof(GetFile), new {projectName, version, pathToFile = pathToEntryPoint});
            }

            Response.Headers.Add("Cache-Control", new Microsoft.Extensions.Primitives.StringValues("max-age=604800"));
            return File(projectFile.FileStream, projectFile.MimeType);
        }
    }
}
