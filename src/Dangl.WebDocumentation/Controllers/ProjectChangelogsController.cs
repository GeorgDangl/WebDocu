﻿using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.ProjectChangelogs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Route("ProjectChangelogs/{projectName}/{version}")]
    public class ProjectChangelogsController : Controller
    {
        private readonly IProjectChangelogService _projectChangelogService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectsService _projectsService;
        private readonly IDocuUserInfoService _docuUserInfoService;

        public ProjectChangelogsController(IProjectChangelogService projectChangelogService,
            UserManager<ApplicationUser> userManager,
            IProjectsService projectsService,
            IDocuUserInfoService docuUserInfoService)
        {
            _projectChangelogService = projectChangelogService;
            _userManager = userManager;
            _projectsService = projectsService;
            _docuUserInfoService = docuUserInfoService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string projectName, string version)
        {
            ViewData["Section"] = "Home";
            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var hasProjectAccess = await _projectsService.UserHasAccessToProjectAsync(projectName, userId);
            if (!hasProjectAccess)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return NotFound();
            }

            var htmlChangelog = await _projectChangelogService.GetChangelogInHtmlFormatAsync(projectName, version);

            if (htmlChangelog == null)
            {
                return NotFound();
            }

            var viewModel = new IndexViewModel
            {
                HtmlChangelog = htmlChangelog,
                ProjectName = projectName,
                ProjectVersion = version
            };

            return View(viewModel);
        }
    }
}
