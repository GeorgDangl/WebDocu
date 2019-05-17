﻿using Dangl.WebDocumentation.Models;
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

        public ProjectVersionsController(UserManager<ApplicationUser> userManager,
            IProjectVersionsService projectVersionsService,
            IProjectsService projectsService,
            IProjectFilesService projectFilesService)
        {
            _userManager = userManager;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _projectFilesService = projectFilesService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string projectName)
        {
            ViewData["Section"] = "Home";
            var userId = _userManager.GetUserId(User);
            var hasProjectAccess = await _projectsService.UserHasAccessToProject(projectName, userId);
            if (!hasProjectAccess)
            {
                return NotFound();
            }
            var entryFilePath = await _projectFilesService.GetEntryFilePathForProject(projectName);
            var model = new IndexViewModel
            {
                ProjectId = await _projectsService.GetIdForProjectByNameAsync(projectName),
                PathToIndex = entryFilePath,
                ProjectName = projectName,
                Versions = (await _projectVersionsService.GetProjectVersionsAsync(projectName))
                    .Select(v => new ProjectVersionViewModel
                    {
                        Version = v.version,
                        HasAssetFiles = v.hasAssets,
                        HasChangelog = v.hasChangelog
                    })
                    .ToList()
            };
            return View(model);
        }
    }
}
