using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.ProjectVersions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Route("Projects/{projectName}")]
    public class ProjectVersionsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectVersionsService _projectVersionsService;
        private readonly IProjectsService _projectsService;
        private readonly IProjectFilesService _projectFilesService;
        private readonly IDocuUserInfoService _docuUserInfoService;

        public ProjectVersionsController(UserManager<ApplicationUser> userManager,
            IProjectVersionsService projectVersionsService,
            IProjectsService projectsService,
            IProjectFilesService projectFilesService,
            IDocuUserInfoService docuUserInfoService)
        {
            _userManager = userManager;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _projectFilesService = projectFilesService;
            _docuUserInfoService = docuUserInfoService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string projectName)
        {
            ViewData["Section"] = "Home";
            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var hasProjectAccess = await _projectsService.UserHasAccessToProjectAsync(projectName, userId);
            if (!hasProjectAccess)
            {
                var projectExists = await _projectsService.ProjectExistsAsync(projectName);
                if (projectExists)
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
            var entryFilePath = await _projectFilesService.GetEntryFilePathForProjectAsync(projectName);
            var versions = await _projectVersionsService.GetProjectVersionsAsync(projectName);
            var versionViewModels = versions
                .Select(v => new ProjectVersionViewModel
                {
                    Version = v.version,
                    HasAssetFiles = v.hasAssets,
                    HasChangelog = v.hasChangelog,
                    HasMarkdownPackage = v.hasMarkdownPackage,
                    DateUtc = v.dateUtc,
                    PackageSizeInBytes = v.packageSizeInBytes
                })
                .ToList();

            var model = new IndexViewModel
            {
                ProjectId = await _projectsService.GetIdForProjectByNameAsync(projectName),
                PathToIndex = entryFilePath,
                ProjectName = projectName,
                Versions = versionViewModels
            };
            return View(model);
        }

        [HttpGet("Download/{version}")]
        public Task<IActionResult> DownloadPackage(string projectName, string version) =>
            DownloadPackageAsync(projectName, version, markdown: false);

        [HttpGet("DownloadMarkdown/{version}")]
        public Task<IActionResult> DownloadMarkdownPackage(string projectName, string version) =>
            DownloadPackageAsync(projectName, version, markdown: true);

        private async Task<IActionResult> DownloadPackageAsync(string projectName, string version, bool markdown)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            if (!User.IsInRole(AppConstants.ADMIN_ROLE_NAME))
            {
                var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
                if (!await _projectsService.UserHasAccessToProjectAsync(projectName, userId))
                {
                    return Forbid();
                }
            }

            var packageDownload = markdown
                ? await _projectFilesService.GetMarkdownPackageDownloadAsync(projectName, version)
                : await _projectFilesService.GetProjectPackageDownloadAsync(projectName, version);
            if (!packageDownload.IsSuccess)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(packageDownload.Value.sasDownloadUrl))
            {
                return Redirect(packageDownload.Value.sasDownloadUrl);
            }

            var fileName = markdown ? $"{projectName}_{version}_markdown.zip" : $"{projectName}_{version}.zip";
            return File(packageDownload.Value.stream, "application/zip", fileName);
        }
    }
}
