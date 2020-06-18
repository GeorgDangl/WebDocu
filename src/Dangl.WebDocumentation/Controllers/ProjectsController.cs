using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Dangl.WebDocumentation.Controllers
{
    public class ProjectsController : Controller
    {
        private readonly IProjectFilesService _projectFilesService;
        private readonly IProjectVersionsService _projectVersionsService;
        private readonly IProjectsService _projectsService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDocuUserInfoService _docuUserInfoService;

        public ProjectsController(UserManager<ApplicationUser> userManager,
            IProjectFilesService projectFilesService,
            IProjectVersionsService projectVersionsService,
            IProjectsService projectsService,
            IDocuUserInfoService docuUserInfoService)
        {
            _projectFilesService = projectFilesService;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _userManager = userManager;
            _docuUserInfoService = docuUserInfoService;
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
            ViewData["Section"] = "Home";
            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var hasProjectAccess = await _projectsService.UserHasAccessToProject(projectName, userId);
            if (!hasProjectAccess)
            {
                var projectVersionExists = await _projectVersionsService.ProjectVersionExistsAsync(projectName, version);
                if (projectVersionExists)
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        return Forbid();
                    }
                    else
                    {
                        return Unauthorized();
                    }
                }

                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(pathToFile))
            {
                // If not pathToFile is given, the response should redirect to the entry point for the project
                var pathToEntryPoint = await _projectFilesService.GetEntryFilePathForProject(projectName);
                return RedirectToAction(nameof(GetFile), new {projectName, version, pathToFile = pathToEntryPoint});
            }

            var availableVersions = await _projectVersionsService.GetProjectVersionsAsync(projectName);
            var requestsLatestVersion = string.Equals(version, "latest", StringComparison.CurrentCultureIgnoreCase);
            if (requestsLatestVersion)
            {
                version = availableVersions.Select(av => av.version).FirstOrDefault();
            }
            else
            {
                if (!availableVersions.Any(av => av.version == version))
                {
                    // This makes sure that deleted earlier versions, e.g. prerelease versions,
                    // do not return 404 after they're deleted but get redirect to the next available version
                    var versionsOrderer = new SemanticVersionsOrderer(availableVersions.Select(av => av.version).ToList());
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

            var responseCacheDuration = requestsLatestVersion
                ? TimeSpan.FromMinutes(60) // If "latest" version is requested, only cache for 60 minutes
                : TimeSpan.FromDays(7);
            var responseCacheHeader = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = responseCacheDuration
            };

            Response.GetTypedHeaders().CacheControl = responseCacheHeader;

            return File(projectFile.FileStream, projectFile.MimeType);
        }
    }
}
