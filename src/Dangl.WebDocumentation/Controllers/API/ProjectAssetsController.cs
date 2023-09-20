using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.ProjectAssets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class ProjectAssetsController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IProjectVersionAssetFilesService _projectVersionAssetFilesService;
        private readonly ILogger<ProjectAssetsController> _logger;

        public ProjectAssetsController(IProjectsService projectsService,
            IProjectVersionAssetFilesService projectVersionAssetFilesService,
            ILoggerFactory loggerFactory)
        {
            _projectsService = projectsService;
            _projectVersionAssetFilesService = projectVersionAssetFilesService;
            _logger = loggerFactory.CreateLogger<ProjectAssetsController>();
        }

        /// <summary>
        ///     Provides an Api to upload asset files for a speicific project and version.
        ///     Exemplary cURL usage:
        ///     curl -F "ApiKey=123" -F "Version=1.0.0" -F "AssetFile=@\"C:\Path\to\file.zip\"" http://localhost:10013/API/ProjectAssets/Upload
        /// </summary>
        /// <param name="apiKey">The ApiKey to authorize a project upload.</param>
        /// <param name="projectPackage">The project content as zip file.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("API/ProjectAssets/Upload")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> Upload(string apiKey, string version, IFormFile assetFile)
        {
            if (assetFile == null)
            {
                return BadRequest();
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Not accepting empty API key -> Disable API upload to projects by setting the API key empty
                return NotFound();
            }
            var projectName = await _projectsService.GetProjectNameForApiKeyAsync(apiKey);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return NotFound();
            }
            using (var projectPackageStream = assetFile.OpenReadStream())
            {
                var packageUploadResult = await _projectVersionAssetFilesService.UploadAssetFileForProjectVersionAsync(projectName, version, assetFile);
                if (packageUploadResult)
                {
                    return Ok();
                }
                return BadRequest();
            }
        }

        [HttpPost]
        [Route("API/ProjectAssets/SASUpload")]
        public async Task<IActionResult> GetSasUploadLinkAsync([FromQuery]string apiKey,
            [FromQuery] string version, 
            [FromBody]SasUploadModel sasUploadModel)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Not accepting empty API key -> Disable API upload to projects by setting the API key empty
                return NotFound();
            }

            var projectName = await _projectsService.GetProjectNameForApiKeyAsync(apiKey);
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return NotFound();
            }

            var sasLinkResult = await _projectVersionAssetFilesService.GetSasBlobUploadLinkAsync(projectName, version, sasUploadModel);
            if (!sasLinkResult.IsSuccess)
            {
                _logger.LogInformation($"Failed to generate a SAS upload link:{Environment.NewLine}{sasLinkResult.ErrorMessage}");
                return BadRequest();
            }

            return Ok(sasLinkResult.Value);
        }
    }
}
