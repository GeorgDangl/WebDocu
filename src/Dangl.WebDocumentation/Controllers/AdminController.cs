using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Repository;
using Dangl.WebDocumentation.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public AdminController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment, UserManager<ApplicationUser> userManager)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
            UserManager = userManager;
        }

        private ApplicationDbContext Context { get; }

        private IHostingEnvironment HostingEnvironment { get; }
        private UserManager<ApplicationUser> UserManager { get; }

        public IActionResult Index()
        {
            var model = new IndexViewModel();
            model.Projects = Context.DocumentationProjects.OrderBy(project => project.Name);
            return View(model);
        }

        public IActionResult CreateProject()
        {
            var model = new CreateProjectViewModel();
            model.AvailableUsers = Context.Users.Select(appUser => appUser.UserName).OrderBy(username => username);
            return View(model);
        }

        [HttpPost]
        public IActionResult CreateProject(CreateProjectViewModel model, List<string> selectedUsers)
        {
            var usersToAdd = Context.Users.Where(currentUser => selectedUsers.Contains(currentUser.UserName)).ToList();
            if (selectedUsers.Any(selected => usersToAdd.All(foundUser => foundUser.UserName != selected)))
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
        [Route("EditProject/{projectId}")]
        public IActionResult EditProject(Guid projectId)
        {
            var project = Context.DocumentationProjects.FirstOrDefault(curr => curr.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            var usersWithAccess = Context.UserProjects.Where(assignment => assignment.ProjectId == project.Id).Select(assignment => assignment.User.Email).ToList();
            var usersWithoutAccess = Context.Users.Select(currentUser => currentUser.Email).Where(currentUser => !usersWithAccess.Contains(currentUser)).ToList();
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
        [Route("EditProject/{projectId}")]
        public IActionResult EditProject(Guid projectId, EditProjectViewModel model, List<string> selectedUsers)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var databaseProject = Context.DocumentationProjects.FirstOrDefault(project => project.Id == projectId);
            if (databaseProject == null)
            {
                return NotFound();
            }
            databaseProject.ApiKey = model.ApiKey;
            databaseProject.IsPublic = model.IsPublic;
            databaseProject.Name = model.ProjectName;
            databaseProject.PathToIndex = model.PathToIndexPage;
            Context.SaveChanges();
            var selectedUsersIds = Context.Users
                .Where(user => selectedUsers.Contains(user.Email))
                .Select(user => user.Id)
                .ToList();
            // Add missing users
            var knownUsersInProject = Context.UserProjects.Where(rel => rel.ProjectId == databaseProject.Id).Select(rel => rel.UserId).ToList();
            var usersToAdd = selectedUsersIds.Where(userId => !knownUsersInProject.Contains(userId));
            foreach (var newUserId in usersToAdd)
            {
                Context.UserProjects.Add(new UserProjectAccess {UserId = newUserId, ProjectId = databaseProject.Id});
            }
            // Remove users that no longer have access
            var usersToRemove = Context.UserProjects.Where(assignment => assignment.ProjectId == databaseProject.Id).Where(assignment => !selectedUsersIds.Contains(assignment.UserId));
            Context.UserProjects.RemoveRange(usersToRemove);
            Context.SaveChanges();
            var usersWithAccess = Context.UserProjects.Where(assignment => assignment.ProjectId == databaseProject.Id).Select(assignment => assignment.User.Email).ToList();
            var usersWithoutAccess = Context.Users.Select(currentUser => currentUser.Email).Where(currentUser => !usersWithAccess.Contains(currentUser)).ToList();
            model.UsersWithAccess = usersWithAccess;
            model.AvailableUsers = usersWithoutAccess;
            ViewBag.SuccessMessage = $"Changed project {databaseProject.Name}.";
            return View(model);
        }

        [HttpGet]
        [Route("UploadProject/{projectId}")]
        public IActionResult UploadProject(Guid projectId)
        {
            var project = Context.DocumentationProjects.FirstOrDefault(p=> p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            ViewBag.ProjectName = project.Name;
            ViewBag.ApiKey = project.ApiKey;
            return View();
        }

        [HttpPost]
        [Route("UploadProject/{projectId}")]
        public IActionResult UploadProject(Guid projectId, IFormFile projectPackage)
        {
            if (projectPackage == null)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View();
            }
            var projectEntry = Context.DocumentationProjects.FirstOrDefault(project => project.Id == projectId);
            if (projectEntry == null)
            {
                return NotFound();
            }
            // Try to read as zip file
            using (var inputStream = projectPackage.OpenReadStream())
            {
                try
                {
                    using (var archive = new ZipArchive(inputStream))
                    {
                        var physicalRootDirectory = Path.Combine(HostingEnvironment.WebRootPath, "App_Data/");
                        var result = ProjectWriter.CreateProjectFilesFromZip(archive, physicalRootDirectory, projectEntry.Id, Context);
                        if (!result)
                        {
                            ModelState.AddModelError(string.Empty, "Failed to update the project files");
                            return View();
                        }
                    }
                    ViewBag.SuccessMessage = "Uploaded package.";
                    return View();
                }
                catch (InvalidDataException)
                {
                    ModelState.AddModelError("", "Cannot read the file as zip archive.");
                    return View();
                }
                catch
                {
                    ModelState.AddModelError("", "Error in request.");
                    return View();
                }
            }
        }

        [HttpGet]
        [Route("DeleteProject/{projectId}")]
        public IActionResult DeleteProject(Guid projectId)
        {
            var project = Context.DocumentationProjects.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            var model = new DeleteProjectViewModel();
            model.ProjectName = project.Name;
            model.ProjectId = project.Id;
            return View(model);
        }

        [HttpPost]
        [Route("DeleteProject/{projectId}")]
        public IActionResult DeleteProject(Guid projectId, DeleteProjectViewModel model)
        {
            if (!model.ConfirmDelete)
            {
                ModelState.AddModelError(nameof(model.ConfirmDelete), "Please confirm the deletion by checking the checkbox.");
            }
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var documentationProject = Context.DocumentationProjects.FirstOrDefault(project => project.Id == model.ProjectId);
            if (documentationProject == null)
            {
                return NotFound();
            }
            if (documentationProject.FolderGuid != Guid.Empty)
            {
                // Check if physical files present and if yes, delete them
                var physicalDirectory = Path.Combine(HostingEnvironment.WebRootPath, "App_Data/" + documentationProject.FolderGuid);
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

        public IActionResult ManageUsers()
        {
            var adminRoleId = Context.Roles.FirstOrDefault(role => role.Name == "Admin").Id;
            var model = new ManageUsersViewModel();
            model.Users = Context.Users.Select(user => new UserAdminRoleViewModel { Name = user.Email, IsAdmin = user.Roles.Any(role => role.RoleId == adminRoleId)});
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ManageUsers(IEnumerable<string> adminUsers)
        {
            var adminRole = Context.Roles.FirstOrDefault(role => role.Name == "Admin");
            if (adminRole == null)
            {
                throw new InvalidDataException("Admin role not found");
            }

            // Remove users that are no longer admin
            var oldAdminsToDelete = (from user in Context.Users
                join userRole in Context.UserRoles on user.Id equals userRole.UserId
                join role in Context.Roles on userRole.RoleId equals role.Id
                where role.Name == adminRole.Name
                      && !adminUsers.Contains(user.Email)
                select new {User = user, UserRole = userRole, Role = role}).ToList();
            foreach (var user in oldAdminsToDelete)
            {
                await UserManager.RemoveFromRoleAsync(user.User, adminRole.Name);
            }

            // Add new admin users
            var newAdminsToAdd = (from user in Context.Users
                where Context.UserRoles.Count(userRole => userRole.UserId == user.Id && userRole.RoleId == adminRole.Id) == 0 // As of 04.01.2016, the EF7 RC1 does translate an errorenous SQL when using .Any() in a sub query here, need to fall back to "Count() == 0"
                      && adminUsers.Contains(user.Email)
                select user).ToList();
            foreach (var user in newAdminsToAdd)
            {
                await UserManager.AddToRoleAsync(user, adminRole.Name);
            }

            ViewBag.SuccessMessage = "Updated users.";
            var model = new ManageUsersViewModel();
            model.Users = Context.Users.Select(websiteUser => new UserAdminRoleViewModel { Name = websiteUser.Email, IsAdmin = websiteUser.Roles.Any(role => role.RoleId == adminRole.Id)});
            return View(model);
        }
    }
}
