using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Dangl.WebDocumentation.ViewModels.Home;

namespace Dangl.WebDocumentation.Controllers
{
    public class HomeController : Controller
    {

        private ApplicationDbContext Context { get; }

        public HomeController(ApplicationDbContext context)
        {
            Context = context;
        }

        public IActionResult Index()
        {
            // Get a list of all projects that the user has access to
            var accessibleProjects = Context.DocumentationProjects.Where(Project => Project.IsPublic).ToList();   // Show all public projects

            var userId = HttpContext.User.GetUserId();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var projectsWithUserAccess = Context.UserProjects.Where(Assignment => Assignment.UserId == userId).Select(Assignment => Assignment.Project).ToList();
                accessibleProjects = accessibleProjects.Union(projectsWithUserAccess).ToList();
            }                         

            var model = new IndexViewModel();
            model.Projects = accessibleProjects.ToList();
            foreach (var FoundProject in model.Projects)
            {
                FoundProject.ToString();
            }
            return View(model);
        }



        public IActionResult Error()
        {
            return View();
        }
    }
}
