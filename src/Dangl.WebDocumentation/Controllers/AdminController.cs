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
using Microsoft.AspNet.Http;
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
                        ProjectId = projectToAdd.Id,
                        UserId = currentUser.Id
                    });
                }
                Context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("EditProject/{ProjectId}")]
        public IActionResult EditProject(Guid ProjectId)
        {
            var project = Context.DocumentationProjects.FirstOrDefault(Curr => Curr.Id == ProjectId);
            if (project == null)
            {
                return HttpNotFound();
            }
            var usersWithAccess = Context.UserProjects.Where(Assignment => Assignment.ProjectId == project.Id).Select(Assignment => Assignment.User.Email).ToList();
            var usersWithoutAccess = Context.Users.Select(CurrentUser => CurrentUser.Email).Where(CurrentUser => !usersWithAccess.Contains(CurrentUser)).ToList();

            var model = new EditProjectViewModel();
            model.ProjectName = project.Name;
            model.IsPublic = project.IsPublic;
            model.PathToIndexPage = project.PathToIndex;
            model.UsersWithAccess = usersWithAccess;
            model.AvailableUsers = usersWithoutAccess;
            model.ApiKey = project.ApiKey;
            return View(model);
        }

        [HttpPost]
        [Route("EditProject/{ProjectId}")]
        public IActionResult EditProject(Guid ProjectId, EditProjectViewModel model, List<string> SelectedUsers)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var databaseProject = Context.DocumentationProjects.FirstOrDefault(Project => Project.Id == ProjectId);
            if (databaseProject == null)
            {
                return HttpNotFound();
            }

            databaseProject.ApiKey = model.ApiKey;
            databaseProject.IsPublic = model.IsPublic;
            databaseProject.Name = model.ProjectName;
            databaseProject.PathToIndex = model.PathToIndexPage;
            Context.SaveChanges();

            var selectedUsersIds = Context.Users.Where(User => SelectedUsers.Contains(User.Email)).Select(User => User.Id).ToList();

            // Add missing users

            var usersToAdd = selectedUsersIds.Where(CurrentId => Context.UserProjects.All(Assignment => Assignment.UserId != CurrentId));
            foreach (var newUserId in usersToAdd)
            {
                Context.UserProjects.Add(new UserProjectAccess {UserId = newUserId, ProjectId = databaseProject.Id});
            }

            // Remove users that no longer have access
            var usersToRemove = Context.UserProjects.Where(Assignment => Assignment.ProjectId == databaseProject.Id).Where(Assignment => !selectedUsersIds.Contains(Assignment.UserId));
            Context.UserProjects.RemoveRange(usersToRemove);

            Context.SaveChanges();

            var usersWithAccess = Context.UserProjects.Where(Assignment => Assignment.ProjectId == databaseProject.Id).Select(Assignment => Assignment.User.Email).ToList();
            var usersWithoutAccess = Context.Users.Select(CurrentUser => CurrentUser.Email).Where(CurrentUser => !usersWithAccess.Contains(CurrentUser)).ToList();
            model.UsersWithAccess = usersWithAccess;
            model.AvailableUsers = usersWithoutAccess;

            ViewBag.SuccessMessage = $"Changed project {databaseProject.Name}.";

            return View(model);
        }

        [HttpGet]
        [Route("UploadProject/{ProjectId}")]
        public IActionResult UploadProject(Guid ProjectId)
        {
            return View();
        }

        [HttpPost]
        [Route("UploadProject/{ProjectId}")]
        public IActionResult UploadProject(Guid ProjectId, IFormFile projectPackage)
        {
            if (projectPackage == null)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View();
            }
            var projectEntry = Context.DocumentationProjects.FirstOrDefault(Project => Project.Id == ProjectId);
            if (projectEntry == null)
            {
                return HttpNotFound();
            }
            // Try to read as zip file
            using (var inputStream = projectPackage.OpenReadStream())
            {
                try
                {
                    using (var archive = new System.IO.Compression.ZipArchive(inputStream))
                    {
                        // Generate a Guid under which to store the upload
                        var rootFolder = Guid.NewGuid();
                        var physicalDirectory = HostingEnvironment.MapPath("App_Data/" + rootFolder);
                        Directory.CreateDirectory(physicalDirectory);
                        foreach (var fileEntry in archive.Entries)
                        {
                            var neededPath = new FileInfo(Path.Combine(physicalDirectory, fileEntry.FullName)).Directory.FullName;
                            if (!Directory.Exists(neededPath))
                            {
                                Directory.CreateDirectory(neededPath);
                            }
                            // Copy only when it's a file and not a folder
                            if (fileEntry.Length > 0)
                            {
                                using (var currentEntryStream = fileEntry.Open())
                                {
                                    using (var fileStream = System.IO.File.Create(Path.Combine(neededPath, fileEntry.Name)))
                                    {
                                        currentEntryStream.CopyTo(fileStream);
                                    }
                                }
                            }
                        }
                        // Delete previous folder and set guid to new folder
                        var oldGuid = projectEntry.FolderGuid;
                        projectEntry.FolderGuid = rootFolder;
                        Context.SaveChanges();
                        var oldFolder = HostingEnvironment.MapPath("App_Data/" + oldGuid);
                        if (Directory.Exists(oldFolder))
                        {
                            Directory.Delete(oldFolder, true);
                        }
                    }
                    ViewBag.SuccessMessage = "Uploaded package.";
                    return View();
                }
                catch (InvalidDataException caughtException)
                {
                    ModelState.AddModelError("", "Cannot read the file as zip archive.");
                    return View();
                }
            }
        }

        [HttpGet]
        [Route("DeleteProject/{ProjectId}")]
        public IActionResult DeleteProject(Guid ProjectId)
        {
            var project = Context.DocumentationProjects.FirstOrDefault(Project => Project.Id == ProjectId);
            if (project == null)
            {
                return HttpNotFound();
            }
            var model = new DeleteProjectViewModel();
            model.ProjectName = project.Name;
            model.ProjectId = project.Id;
            return View(model);
        }

        [HttpPost]
        [Route("DeleteProject/{ProjectId}")]
        public IActionResult DeleteProject(Guid ProjectId, DeleteProjectViewModel model)
        {
            if (!model.ConfirmDelete)
            {
                ModelState.AddModelError(nameof(model.ConfirmDelete), "Please confirm the deletion by checking the checkbox.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var documentationProject = Context.DocumentationProjects.FirstOrDefault(Project => Project.Id == model.ProjectId);
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
                    Directory.Delete(physicalDirectory, true);
                }
            }
            Context.DocumentationProjects.Remove(documentationProject);
            Context.SaveChanges();
            ViewBag.SuccessMessage = $"Deleted project {documentationProject.Name}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
