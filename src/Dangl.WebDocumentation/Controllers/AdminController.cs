using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Repository;
using Dangl.WebDocumentation.Services;
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
        private readonly IProjectFilesService _projectFilesService;
        private readonly ApplicationDbContext _context;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(ApplicationDbContext context,
            IHostingEnvironment hostingEnvironment,
            UserManager<ApplicationUser> userManager,
            IProjectFilesService projectFilesService)
        {
            _projectFilesService = projectFilesService;
            _context = context;
            _hostingEnvironment = hostingEnvironment;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            var model = new IndexViewModel();
            model.Projects = _context.DocumentationProjects.OrderBy(project => project.Name);
            return View(model);
        }

        public IActionResult CreateProject()
        {
            var model = new CreateProjectViewModel();
            model.AvailableUsers = _context.Users.Select(appUser => appUser.UserName).OrderBy(username => username);
            return View(model);
        }

        [HttpPost]
        public IActionResult CreateProject(CreateProjectViewModel model, List<string> selectedUsers)
        {
            var usersToAdd = _context.Users.Where(currentUser => selectedUsers.Contains(currentUser.UserName)).ToList();
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
            _context.DocumentationProjects.Add(projectToAdd);
            _context.SaveChanges();
            if (usersToAdd.Any())
            {
                foreach (var currentUser in usersToAdd)
                {
                    _context.UserProjects.Add(new UserProjectAccess
                    {
                        ProjectId = projectToAdd.Id,
                        UserId = currentUser.Id
                    });
                }
                _context.SaveChanges();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("EditProject/{projectId}")]
        public IActionResult EditProject(Guid projectId)
        {
            var project = _context.DocumentationProjects.FirstOrDefault(curr => curr.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            var usersWithAccess = _context.UserProjects.Where(assignment => assignment.ProjectId == project.Id).Select(assignment => assignment.User.Email).ToList();
            var usersWithoutAccess = _context.Users.Select(currentUser => currentUser.Email).Where(currentUser => !usersWithAccess.Contains(currentUser)).ToList();
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
            var databaseProject = _context.DocumentationProjects.FirstOrDefault(project => project.Id == projectId);
            if (databaseProject == null)
            {
                return NotFound();
            }
            databaseProject.ApiKey = model.ApiKey;
            databaseProject.IsPublic = model.IsPublic;
            databaseProject.Name = model.ProjectName;
            databaseProject.PathToIndex = model.PathToIndexPage;
            _context.SaveChanges();
            var selectedUsersIds = _context.Users
                .Where(user => selectedUsers.Contains(user.Email))
                .Select(user => user.Id)
                .ToList();
            // Add missing users
            var knownUsersInProject = _context.UserProjects.Where(rel => rel.ProjectId == databaseProject.Id).Select(rel => rel.UserId).ToList();
            var usersToAdd = selectedUsersIds.Where(userId => !knownUsersInProject.Contains(userId));
            foreach (var newUserId in usersToAdd)
            {
                _context.UserProjects.Add(new UserProjectAccess {UserId = newUserId, ProjectId = databaseProject.Id});
            }
            // Remove users that no longer have access
            var usersToRemove = _context.UserProjects.Where(assignment => assignment.ProjectId == databaseProject.Id).Where(assignment => !selectedUsersIds.Contains(assignment.UserId));
            _context.UserProjects.RemoveRange(usersToRemove);
            _context.SaveChanges();
            var usersWithAccess = _context.UserProjects.Where(assignment => assignment.ProjectId == databaseProject.Id).Select(assignment => assignment.User.Email).ToList();
            var usersWithoutAccess = _context.Users.Select(currentUser => currentUser.Email).Where(currentUser => !usersWithAccess.Contains(currentUser)).ToList();
            model.UsersWithAccess = usersWithAccess;
            model.AvailableUsers = usersWithoutAccess;
            ViewBag.SuccessMessage = $"Changed project {databaseProject.Name}.";
            return View(model);
        }

        [HttpGet]
        [Route("UploadProject/{projectId}")]
        public IActionResult UploadProject(Guid projectId)
        {
            var project = _context.DocumentationProjects.FirstOrDefault(p=> p.Id == projectId);
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
        public async Task<IActionResult> UploadProject(Guid projectId, string version, IFormFile projectPackage)
        {
            if (projectPackage == null)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View();
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                ModelState.AddModelError("", "Please specify a version.");
                return View();
            }
            var projectEntry = _context.DocumentationProjects.FirstOrDefault(project => project.Id == projectId);
            if (projectEntry == null)
            {
                return NotFound();
            }
            // Try to read as zip file
            using (var inputStream = projectPackage.OpenReadStream())
            {
                var uploadResult = await _projectFilesService.UploadProjectPackage(projectEntry.Name, version, inputStream);
                if (!uploadResult)
                {
                    ModelState.AddModelError(string.Empty, "Failed to update the project files");
                    return View();
                }
                ViewBag.SuccessMessage = "Uploaded package.";
                return View();
            }
        }

        [HttpGet]
        [Route("DeleteProject/{projectId}")]
        public IActionResult DeleteProject(Guid projectId)
        {
            var project = _context.DocumentationProjects.FirstOrDefault(p => p.Id == projectId);
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
            var documentationProject = _context.DocumentationProjects.FirstOrDefault(project => project.Id == model.ProjectId);
            if (documentationProject == null)
            {
                return NotFound();
            }
            if (documentationProject.FolderGuid != Guid.Empty)
            {
                // Check if physical files present and if yes, delete them
                var physicalDirectory = Path.Combine(_hostingEnvironment.WebRootPath, "App_Data/" + documentationProject.FolderGuid);
                if (Directory.Exists(physicalDirectory))
                {
                    Directory.Delete(physicalDirectory, true);
                }
            }
            _context.DocumentationProjects.Remove(documentationProject);
            _context.SaveChanges();
            ViewBag.SuccessMessage = $"Deleted project {documentationProject.Name}.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult ManageUsers()
        {
            var adminRoleId = _context.Roles.FirstOrDefault(role => role.Name == "Admin").Id;
            var model = new ManageUsersViewModel();
            model.Users = _context.Users.Select(user => new UserAdminRoleViewModel { Name = user.Email, IsAdmin = user.Roles.Any(role => role.RoleId == adminRoleId)});
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ManageUsers(IEnumerable<string> adminUsers)
        {
            var adminRole = _context.Roles.FirstOrDefault(role => role.Name == "Admin");
            if (adminRole == null)
            {
                throw new InvalidDataException("Admin role not found");
            }

            // Remove users that are no longer admin
            var oldAdminsToDelete = (from user in _context.Users
                join userRole in _context.UserRoles on user.Id equals userRole.UserId
                join role in _context.Roles on userRole.RoleId equals role.Id
                where role.Name == adminRole.Name
                      && !adminUsers.Contains(user.Email)
                select new {User = user, UserRole = userRole, Role = role}).ToList();
            foreach (var user in oldAdminsToDelete)
            {
                await _userManager.RemoveFromRoleAsync(user.User, adminRole.Name);
            }

            // Add new admin users
            var newAdminsToAdd = (from user in _context.Users
                where _context.UserRoles.Count(userRole => userRole.UserId == user.Id && userRole.RoleId == adminRole.Id) == 0 // As of 04.01.2016, the EF7 RC1 does translate an errorenous SQL when using .Any() in a sub query here, need to fall back to "Count() == 0"
                      && adminUsers.Contains(user.Email)
                select user).ToList();
            foreach (var user in newAdminsToAdd)
            {
                await _userManager.AddToRoleAsync(user, adminRole.Name);
            }

            ViewBag.SuccessMessage = "Updated users.";
            var model = new ManageUsersViewModel();
            model.Users = _context.Users.Select(websiteUser => new UserAdminRoleViewModel { Name = websiteUser.Email, IsAdmin = websiteUser.Roles.Any(role => role.RoleId == adminRole.Id)});
            return View(model);
        }
    }
}
