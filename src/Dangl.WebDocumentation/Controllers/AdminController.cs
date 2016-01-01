using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Mvc.Rendering;
using Microsoft.Data.Entity;
using Microsoft.Extensions.Logging;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Admin;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize(Roles ="Admin")]
    public class AdminController : Controller
    {
        private ApplicationDbContext Context { get; }

        public AdminController(ApplicationDbContext context)
        {
            Context = context;
        }

        public IActionResult Index()
        {
            // Get a list of all projects that the user has access to
            var projects = Context.DocumentationProjects;
            var model = new IndexViewModel();
            model.Projects = projects;
            return View(model);
            //var accessibleProjects = Context.DocumentationProjects.Where(Project => Project.IsPublic);   // Show all public projects

            //var userId = HttpContext.User.GetUserId();

            //if (!string.IsNullOrWhiteSpace(userId))
            //{
            //    accessibleProjects = accessibleProjects.Union(Context.UserProjects.Where(Assignment => Assignment.UserId == userId).Select(Assignment => Assignment.Project));
            //}

            //var model = new IndexViewModel();
            //model.Projects = accessibleProjects;
            //return View();
        }

        public IActionResult CreateProject()
        {
            var model = new CreateProjectViewModel();
            model.AvailableUsers = Context.Users.Select(AppUser => AppUser.UserName).OrderBy(Username => Username);
            return View(model);
        }

        [HttpPost]
        public IActionResult CreateProject(CreateProjectViewModel model, List<string> SelectedUsers)
        {
            var usersToAdd = Context.Users.Where(CurrentUser => SelectedUsers.Contains(CurrentUser.UserName)).ToList();
            if (SelectedUsers.Any(Selected => usersToAdd.All(FoundUser => FoundUser.UserName != Selected)))
            {
                ModelState.AddModelError("", "Unrecognized user selected");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var projectToAdd = new DocumentationProject
            {
                IsPublic = model.IsPublic,
                Name = model.ProjectName,
                PathToIndex = model.PathToIndexPage
            };
            Context.DocumentationProjects.Add(projectToAdd);
            Context.SaveChanges();
            if (usersToAdd.Any())
            {
                foreach (var currentUser in usersToAdd)
                {
                    Context.UserProjects.Add(new UserProjectAccess
                    {
                        ProjectId = projectToAdd.Name,
                        UserId = currentUser.Id
                    });
                }
                Context.SaveChanges();
            }
            return RedirectToAction(nameof(EditProject));
        }

        public IActionResult EditProject(string ProjectName)
        {
            throw new NotImplementedException();
        }

    }
}
