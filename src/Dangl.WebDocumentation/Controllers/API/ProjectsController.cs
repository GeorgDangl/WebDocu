﻿using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class ProjectsController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IProjectFilesService _projectFilesService;

        public ProjectsController(IProjectsService projectsService,
            IProjectFilesService projectFilesService)
        {
            _projectsService = projectsService;
            _projectFilesService = projectFilesService;
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
                return BadRequest();
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Not accepting empty API key -> Disable API upload to projects by setting the API key empty
                return NotFound();
            }
            var projectName = await _projectsService.GetProjectNameForApiKey(apiKey);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return NotFound();
            }

            if (await _projectFilesService.PackageAlreadyExistsAsync(projectName, version))
            {
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
