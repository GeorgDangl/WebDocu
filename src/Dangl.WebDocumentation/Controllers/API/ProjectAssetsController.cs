using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class ProjectAssetsController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IProjectVersionAssetFilesService _projectVersionAssetFilesService;

        public ProjectAssetsController(IProjectsService projectsService,
            IProjectVersionAssetFilesService projectVersionAssetFilesService)
        {
            _projectsService = projectsService;
            _projectVersionAssetFilesService = projectVersionAssetFilesService;
        }

        /// <summary>
        ///     Provides an Api to upload asset files for a speicific project and version.
        ///     Exemplary cURL usage:
        ///     curl -F "ApiKey=123" -F "Version=1.0.0" -F "AssetFile=@\"C:\Path\to\file.zip\"" http://localhost:10013/API/ProjectsAssets/Upload
        /// </summary>
        /// <param name="apiKey">The ApiKey to authorize a project upload.</param>
        /// <param name="projectPackage">The project content as zip file.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("API/ProjectsAssets/Upload")]
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
            var projectName = await _projectsService.GetProjectNameForApiKey(apiKey);
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
    }
}
