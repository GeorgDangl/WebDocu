using System;
using System.Collections.Generic;
using System.IO;
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
using Microsoft.AspNet.Hosting;
using Microsoft.Extensions.PlatformAbstractions;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize(Roles ="Admin")]
    public class AdminController : Controller
    {
        private ApplicationDbContext Context { get; }

        private IHostingEnvironment HostingEnvironment { get; }

        public AdminController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
        }

        public IActionResult Index()
        {
            var model = new IndexViewModel();
            model.Projects = Context.DocumentationProjects.OrderBy(Project => Project.Name);
            return View(model);
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
            return RedirectToAction(nameof(Index));
        }

        [Route("EditProject/{ProjectName}")]
        public IActionResult EditProject(string ProjectName)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [Route("DeleteProject/{ProjectName}")]
        public IActionResult DeleteProject(string ProjectName)
        {
            var model = new DeleteProjectViewModel();
            model.ProjectName = ProjectName;
            return View(model);
        }

        [HttpPost]
        [Route("DeleteProject/{ProjectName}")]
        public IActionResult DeleteProject(string ProjectName, DeleteProjectViewModel model)
        {
            if (!model.ConfirmDelete)
            {
                ModelState.AddModelError(nameof(model.ConfirmDelete), "Please confirm the deletion by checking the checkbox.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var documentationProject = Context.DocumentationProjects.FirstOrDefault(Project => Project.Name == model.ProjectName);
            if (documentationProject == null)
            {
                return HttpNotFound();
            }
            if (documentationProject.FolderGuid != Guid.Empty)
            {
                // Check if physical files present and if yes, delete them
                var physicalDirectory = HostingEnvironment.MapPath("App_Data/" + documentationProject.FolderGuid);
                if (Directory.Exists(physicalDirectory))
                {
                    Directory.Delete(physicalDirectory);
                }
            }
            Context.DocumentationProjects.Remove(documentationProject);
            Context.SaveChanges();
            return RedirectToAction(nameof(DeleteProjectConfirmed), new {ProjectName= model.ProjectName});
        }

        [HttpGet]
        [Route("DeleteProjectConfirmed/{ProjectName}")]
        public IActionResult DeleteProjectConfirmed(string ProjectName)
        {
            ViewBag.ProjectName = ProjectName;
            return View();
        }
    }
}
