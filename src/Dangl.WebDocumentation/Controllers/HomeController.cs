using Dangl.Identity.Client.Mvc.Services;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IDocuUserInfoService _docuUserInfoService;

        public HomeController(UserManager<ApplicationUser> userManager,
            IProjectsService projectsService,
            IDocuUserInfoService docuUserInfoService)
        {
            UserManager = userManager;
            _projectsService = projectsService;
            _docuUserInfoService = docuUserInfoService;
        }

        private UserManager<ApplicationUser> UserManager { get; }

        public async Task<IActionResult> Index(string projectsFilter = null)
        {
            ViewData["Section"] = "Home";

            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();

            var accessibleProjects = await _projectsService.GetAllProjectsForUserAsync(userId, projectsFilter);

            var model = new IndexViewModel();
            model.Projects = accessibleProjects;
            model.ProjectsFilter = projectsFilter;
            return View(model);
        }

        public IActionResult Privacy()
        {
            ViewData["Section"] = "Privacy";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }

        public IActionResult ManageAccount()
        {
            return View();
        }
    }
}
