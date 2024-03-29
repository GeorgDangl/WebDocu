﻿using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Route("ProjectAssets/{projectName}/{version}")]
    public class ProjectAssetsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectVersionsService _projectVersionsService;
        private readonly IProjectsService _projectsService;
        private readonly IProjectFilesService _projectFilesService;
        private readonly IProjectVersionAssetFilesService _projectVersionAssetFilesService;
        private readonly IDocuUserInfoService _docuUserInfoService;

        public ProjectAssetsController(UserManager<ApplicationUser> userManager,
            IProjectVersionsService projectVersionsService,
            IProjectsService projectsService,
            IProjectFilesService projectFilesService,
            IProjectVersionAssetFilesService projectVersionAssetFilesService,
            IDocuUserInfoService docuUserInfoService)
        {
            _userManager = userManager;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _projectFilesService = projectFilesService;
            _projectVersionAssetFilesService = projectVersionAssetFilesService;
            _docuUserInfoService = docuUserInfoService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string projectName, string version, string successMessage)
        {
            ViewData["Section"] = "Home";
            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                ViewBag.SuccessMessage = successMessage;
            }

            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var hasProjectAccess = await _projectsService.UserHasAccessToProjectAsync(projectName, userId);
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

            var requestsLatestVersion = string.Equals(version, "latest", StringComparison.CurrentCultureIgnoreCase);
            if (requestsLatestVersion)
            {
                var availableVersions = await _projectVersionsService.GetProjectVersionsAsync(projectName);
                version = availableVersions.Select(av => av.version).FirstOrDefault();
            }

            var assets = await _projectVersionAssetFilesService.GetAssetsForProjectVersionAsync(projectName, version);
            var projectId = await _projectsService.GetIdForProjectByNameAsync(projectName);

            var model = new Dangl.WebDocumentation.ViewModels.ProjectAssets.IndexViewModel
            {
                ProjectName = projectName,
                ProjectId = projectId,
                ProjectVersion = version,
                Files = assets.Select(af => new ViewModels.ProjectAssets.ProjectAssetFileViewModel
                {
                    FileName = af.fileName,
                    PrettyfiedFileSize = af.prettyfiedFileSize,
                    FileId = af.fileId
                }).ToList()
            };

            return View(model);
        }

        [HttpGet("{assetFileName}")]
        public async Task<IActionResult> GetAssetFile(string projectName, string version, string assetFileName, Guid fileId)
        {
            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var hasProjectAccess = await _projectsService.UserHasAccessToProjectAsync(projectName, userId);
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

            var assetExistsOnProject = (await _projectVersionAssetFilesService.GetAssetsForProjectVersionAsync(projectName, version))
                .Any(av => av.fileId == fileId);
            if (!assetExistsOnProject)
            {
                return NotFound();
            }

            var fileRepoResult = await _projectVersionAssetFilesService
                .GetAssetDownloadAsync(assetFileName, fileId);

            if (!fileRepoResult.IsSuccess)
            {
                return NotFound();
            }

            if (fileRepoResult.Value.stream != null)
            {
                return File(fileRepoResult.Value.stream, "application/octet-stream", assetFileName);
            }
            else if (!string.IsNullOrWhiteSpace(fileRepoResult.Value.sasDownloadUrl))
            {
                return Redirect(fileRepoResult.Value.sasDownloadUrl);
            }

            return NotFound();
        }

        [Authorize(Roles = AppConstants.ADMIN_ROLE_NAME)]
        [HttpGet("Upload")]
        public IActionResult UploadAsset(string projectName, string version)
        {
            var model = new ViewModels.ProjectAssets.UploadAssetViewModel
            {
                ProjectName = projectName,
                ProjectVersion = version
            };
            return View(model);
        }

        [Authorize(Roles = AppConstants.ADMIN_ROLE_NAME)]
        [HttpPost("Upload")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadAsset(string projectName, string version, IFormFile assetFile)
        {
            var uploadViewModel = new ViewModels.ProjectAssets.UploadAssetViewModel
            {
                ProjectName = projectName,
                ProjectVersion = version
            };

            ViewData["Section"] = "Home";
            if (assetFile == null)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View(uploadViewModel);
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                ModelState.AddModelError("", "Please specify a version.");
                return View(uploadViewModel);
            }

            var uploadResult = await _projectVersionAssetFilesService.UploadAssetFileForProjectVersionAsync(projectName,
                version,
                assetFile);

            if (uploadResult)
            {
                ViewBag.SuccessMessage = "Asset file uploaded.";
            }
            else
            {
                ModelState.AddModelError("", "The asset file could not be uploaded.");
            }

            return View(uploadViewModel);
        }
    }
}
