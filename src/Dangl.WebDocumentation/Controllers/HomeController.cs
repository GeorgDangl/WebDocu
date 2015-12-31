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
            var accessibleProjects = Context.DocumentationProjects.Where(Project => Project.IsPublic);   // Show all public projects

            var userId = HttpContext.User.GetUserId();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                accessibleProjects = accessibleProjects.Union(Context.UserProjects.Where(Assignment => Assignment.UserId == userId).Select(Assignment => Assignment.Project));
            }                         

            // Not done, but now we gotta celebrate New Year=)
            throw new NotImplementedException();

            var model = new Dangl.WebDocumentation.ViewModels.Home.IndexViewModel();
            return View();
        }



        public IActionResult Error()
        {
            return View();
        }
    }
}
