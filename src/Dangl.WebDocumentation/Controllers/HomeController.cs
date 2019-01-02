using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProjectsService _projectsService;

        public HomeController(UserManager<ApplicationUser> userManager,
            IProjectsService projectsService)
        {
            UserManager = userManager;
            _projectsService = projectsService;
        }

        private UserManager<ApplicationUser> UserManager { get; }

        public async Task<IActionResult> Index()
        {
            ViewData["Section"] = "Home";

            var userId = UserManager.GetUserId(User);
            var accessibleProjects = await _projectsService.GetAllProjectsForUser(userId);

            var model = new IndexViewModel();
            model.Projects = accessibleProjects;
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
    }
}
