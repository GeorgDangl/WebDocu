using System.Linq;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Home;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers
{
    public class HomeController : Controller
    {
        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            Context = context;
            UserManager = userManager;
        }

        private ApplicationDbContext Context { get; }

        private UserManager<ApplicationUser> UserManager { get; }

        public IActionResult Index()
        {
            ViewData["Section"] = "Home";
            // Get a list of all projects that the user has access to
            var accessibleProjects = Context.DocumentationProjects.Where(project => project.IsPublic).ToList(); // Show all public projects
            var userId = UserManager.GetUserId(User);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var projectsWithUserAccess = Context.UserProjects.Where(assignment => assignment.UserId == userId).Select(assignment => assignment.Project).ToList();
                accessibleProjects = accessibleProjects.Union(projectsWithUserAccess).ToList();
            }
            var model = new IndexViewModel();
            model.Projects = accessibleProjects.OrderBy(project => project.Name);
            return View(model);
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
