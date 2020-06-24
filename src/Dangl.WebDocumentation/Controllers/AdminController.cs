using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize(Roles = AppConstants.ADMIN_ROLE_NAME)]
    public class AdminController : Controller
    {
        private readonly IProjectFilesService _projectFilesService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IProjectVersionsService _projectVersionsService;
        private readonly IProjectsService _projectsService;
        private readonly IProjectVersionAssetFilesService _projectVersionAssetFilesService;
        private readonly IEmailSender _emailSender;

        public AdminController(ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProjectFilesService projectFilesService,
            IProjectVersionsService projectVersionsService,
            IProjectVersionAssetFilesService projectVersionAssetFilesService,
            IProjectsService projectsService,
            IEmailSender emailSender)
        {
            _projectFilesService = projectFilesService;
            _context = context;
            _userManager = userManager;
            _projectVersionsService = projectVersionsService;
            _projectsService = projectsService;
            _projectVersionAssetFilesService = projectVersionAssetFilesService;
            _emailSender = emailSender;
        }

        public IActionResult Index(string successMessage, string errorMessage)
        {
            ViewData["Section"] = "Admin";

            if (!string.IsNullOrWhiteSpace(successMessage))
            {
                ViewBag.SuccessMessage = successMessage;
            }
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                ViewBag.ErrorMessage = errorMessage;
            }
            var model = new IndexViewModel();
            model.Projects = _context.DocumentationProjects.OrderBy(project => project.Name);
            return View(model);
        }

        public IActionResult CreateProject()
        {
            ViewData["Section"] = "Admin";
            var model = new CreateProjectViewModel();
            model.AvailableUsers = _context.Users.Select(appUser => appUser.UserName).OrderBy(username => username);
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SendTestEmail()
        {
            var user = await _userManager.GetUserAsync(User);
            var email = user.Email;
            var emailSendResult = await _emailSender.SendMessage(email, "DanglDocu Test Email", $@"<h1>Email Test</h1>
<p>This email was sent from DanglDocu at {DateTime.UtcNow:dd.MM.yyyy HH:mm} (UTC) to {email} by manual invocation from the owner of this user account.</p>");

            if (emailSendResult)
            {
                var successMessage = "The test email was sent. Please check your inbox.";
                return RedirectToAction(nameof(Index), new { successMessage });
            }
            else
            {
                var errorMessage = "The test email could not be sent. Please check the server side configuration.";
                return RedirectToAction(nameof(Index), new { errorMessage });
            }
        }

        [HttpPost]
        public IActionResult CreateProject(CreateProjectViewModel model, List<string> selectedUsers)
        {
            ViewData["Section"] = "Admin";
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
            ViewData["Section"] = "Admin";
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
            ViewData["Section"] = "Admin";
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
                _context.UserProjects.Add(new UserProjectAccess { UserId = newUserId, ProjectId = databaseProject.Id });
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
            ViewData["Section"] = "Admin";
            var project = _context.DocumentationProjects.FirstOrDefault(p => p.Id == projectId);
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
        public async Task<IActionResult> UploadProject(Guid projectId, string version, string markdownChangelog, IFormFile projectPackage)
        {
            ViewData["Section"] = "Admin";
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
                var uploadResult = await _projectFilesService.UploadProjectPackageAsync(projectEntry.Name, version, markdownChangelog, inputStream);
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
            ViewData["Section"] = "Admin";
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

        [HttpGet("Projects/DeleteBetaVersions/{projectName}")]
        public async Task<IActionResult> DeleteBetaVersions(string projectName)
        {
            var previewVersionsToDelete = await _projectVersionsService.GetAllPreviewVersionsExceptFirstAndLastAsync(projectName);
            var model = new DeleteBetaVersionsViewModel
            {
                ProjectName = projectName,
                VersionsToDelete = previewVersionsToDelete
            };
            return View(model);
        }

        [HttpPost("Projects/DeleteBetaVersions/{projectName}")]
        public async Task<IActionResult> ConfirmDeleteBetaVersions(string projectName)
        {
            var projectId = await _projectsService.GetIdForProjectByNameAsync(projectName);
            var previewVersionsToDelete = await _projectVersionsService.GetAllPreviewVersionsExceptFirstAndLastAsync(projectName);
            foreach (var previewVersionToDelete in previewVersionsToDelete)
            {
                await _projectFilesService.DeleteProjectVersionPackageAsync(projectId, previewVersionToDelete);
            }
            ViewBag.SuccessMessage = $"Deleted obsolete beta versions for {projectName}.";
            var model = new DeleteBetaVersionsViewModel
            {
                ProjectName = projectName,
                VersionsToDelete = new List<string>()
            };
            return View(nameof(DeleteBetaVersions), model);
        }

        [HttpPost]
        [Route("DeleteProject/{projectId}")]
        public async Task<IActionResult> DeleteProject(Guid projectId, DeleteProjectViewModel model)
        {
            ViewData["Section"] = "Admin";
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

            var projectVersions = await _projectVersionsService.GetProjectVersionsAsync(documentationProject.Name);

            foreach (var projectVersion in projectVersions)
            {
                await _projectFilesService.DeleteProjectVersionPackageAsync(documentationProject.Id, projectVersion.version);
            }

            _context.DocumentationProjects.Remove(documentationProject);
            _context.SaveChanges();
            ViewBag.SuccessMessage = $"Deleted project {documentationProject.Name}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("DeleteProjectVersion/{projectId}/{version}")]
        public IActionResult DeleteProjectVersion(Guid projectId, string version)
        {
            ViewData["Section"] = "Admin";
            var project = _context.DocumentationProjects.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }
            var model = new DeleteProjectVersionViewModel
            {
                ConfirmDelete = false,
                ProjectId = projectId,
                ProjectName = project.Name,
                Version = version
            };
            return View(model);
        }

        [HttpPost]
        [Route("DeleteProjectVersion/{projectId}/{version}")]
        public async Task<IActionResult> DeleteProjectVersion(Guid projectId, string version, DeleteProjectVersionViewModel model)
        {
            ViewData["Section"] = "Admin";
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
            var deletionResult = await _projectFilesService.DeleteProjectVersionPackageAsync(documentationProject.Id, model.Version);
            if (!deletionResult)
            {
                ModelState.AddModelError("Error", "Could not delete project version.");
                return View(model);
            }
            ViewBag.SuccessMessage = $"Deleted project version {documentationProject.Name}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("DeleteProjectAsset/{projectId}/{version}/{fileId}/{assetFileName}")]
        public IActionResult DeleteProjectAsset(Guid projectId, string version, Guid fileId, string assetFileName)
        {
            ViewData["Section"] = "Admin";
            var project = _context.DocumentationProjects.FirstOrDefault(p => p.Id == projectId);
            if (project == null)
            {
                return NotFound();
            }

            var model = new DeleteProjectAssetViewModel
            {
                ConfirmDelete = false,
                ProjectId = projectId,
                ProjectName = project.Name,
                Version = version,
                FileId = fileId,
                FileName = assetFileName
            };
            return View(model);
        }

        [HttpPost]
        [Route("DeleteProjectAsset/{projectId}/{version}/{fileId}/{assetFileName}")]
        public async Task<IActionResult> DeleteProjectAsset(DeleteProjectAssetViewModel model)
        {
            ViewData["Section"] = "Admin";
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
            var deletionResult = await _projectVersionAssetFilesService.DeleteProjectAssetFileAsync(model.FileId);
            if (!deletionResult)
            {
                ModelState.AddModelError("Error", "Could not delete project asset file.");
                return View(model);
            }
            var successMessage = $"Deleted project asset file {model.FileName}.";
            return RedirectToAction(nameof(Index), "ProjectAssets", new { projectName = model.ProjectName, version = model.Version, successMessage });
        }

        public async Task<IActionResult> ManageUsers()
        {
            ViewData["Section"] = "Admin";
            var adminRoleId = (await _context.Roles.FirstAsync(role => role.Name == AppConstants.ADMIN_ROLE_NAME)).Id;
            var model = new ManageUsersViewModel();
            model.Users = await _context.Users
                .Select(user => new UserAdminRoleViewModel
                {
                    Name = user.UserName,
                    Email = user.Email,
                    IsAdmin = user.Roles.Any(role => role.RoleId == adminRoleId)
                })
                .OrderByDescending(user => user.IsAdmin)
                .ThenBy(user => user.Name)
                .ToListAsync();
            return View(model);
        }
    }
}
