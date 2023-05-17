using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class ProjectsController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IProjectFilesService _projectFilesService;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(IProjectsService projectsService,
            IProjectFilesService projectFilesService,
            ILoggerFactory loggerFactory)
        {
            _projectsService = projectsService;
            _projectFilesService = projectFilesService;
            _logger = loggerFactory.CreateLogger<ProjectsController>();
        }

        /// <summary>
        ///     Provides an Api to upload projects.
        ///     Exemplary cURL usage:
        ///     curl -F "ApiKey=123" -F "ProjectPackage=@\"C:\Path\to\file.zip\"" http://localhost:10013/API/Projects/Upload
        /// </summary>
        /// <param name="apiKey">The ApiKey to authorize a project upload.</param>
        /// <param name="projectPackage">The project content as zip file.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("API/Projects/Upload")]
        public async Task<IActionResult> Upload(string apiKey, string version, string markdownChangelog, IFormFile projectPackage)
        {
            if (projectPackage == null)
            {
                _logger.LogInformation("Tried to upload a new project revision without a projectPackage file.");
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Not accepting empty API key -> Disable API upload to projects by setting the API key empty
                _logger.LogInformation("Tried to upload a new project revision without providing an API key.");
                return NotFound();
            }

            var projectName = await _projectsService.GetProjectNameForApiKey(apiKey);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                _logger.LogInformation("Failed to resolve the project name from the given api key.");
                return NotFound();
            }

            if (await _projectFilesService.PackageAlreadyExistsAsync(projectName, version))
            {
                _logger.LogInformation("Tried to reupload a project version that already exists.");
                return Conflict();
            }

            using (var projectPackageStream = projectPackage.OpenReadStream())
            {
                var packageUploadResult = await _projectFilesService.UploadProjectPackageAsync(projectName, version, markdownChangelog, projectPackageStream);
                if (packageUploadResult)
                {
                    return Ok();
                }
                return BadRequest();
            }
        }
    }
}
