using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Repository;
using Dangl.WebDocumentation.ViewModels.Admin;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Mvc;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public AdminController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
            UserManager = userManager;
            SignInManager = signInManager;
        }

        private ApplicationDbContext Context { get; }

        private IHostingEnvironment HostingEnvironment { get; }
        private UserManager<ApplicationUser> UserManager { get; }

        private SignInManager<ApplicationUser> SignInManager { get; }

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
            var project = Context.DocumentationProjects.FirstOrDefault(Project => Project.Id == ProjectId);
            if (project == null)
            {
                return HttpNotFound();
            }
            ViewBag.ProjectName = project.Name;
            ViewBag.ApiKey = project.ApiKey;
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
                    using (var archive = new ZipArchive(inputStream))
                    {
                        var physicalRootDirectory = HostingEnvironment.MapPath("App_Data/");
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
                catch (InvalidDataException caughtException)
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

        public IActionResult ManageUsers()
        {
            var adminRoleId = Context.Roles.FirstOrDefault(Role => Role.Name == "Admin").Id;
            var model = new ManageUsersViewModel();
            model.Users = Context.Users.Select(User => new UserAdminRole {Name = User.Email, IsAdmin = User.Roles.Any(Role => Role.RoleId == adminRoleId)});
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ManageUsers(IEnumerable<string> AdminUsers)
        {
            var adminRole = Context.Roles.FirstOrDefault(Role => Role.Name == "Admin");
            if (adminRole == null)
            {
                throw new InvalidDataException("Admin role not found");
            }

            // Remove users that are no longer admin
            var oldAdminsToDelete = (from User in Context.Users
                join UserRole in Context.UserRoles on User.Id equals UserRole.UserId
                join Role in Context.Roles on UserRole.RoleId equals Role.Id
                where Role.Name == adminRole.Name
                      && !AdminUsers.Contains(User.Email)
                select new {User, UserRole, Role}).ToList();
            foreach (var user in oldAdminsToDelete)
            {
                await UserManager.RemoveFromRoleAsync(user.User, adminRole.Name);
                await RefreshUserClaimsStamp(user.User);
            }

            // Add new admin users
            var newAdminsToAdd = (from User in Context.Users
                where Context.UserRoles.Count(UserRole => UserRole.UserId == User.Id && UserRole.RoleId == adminRole.Id) == 0 // As of 04.01.2016, the EF7 RC1 does translate an errorenous SQL when using .Any() in a sub query here, need to fall back to "Count() == 0"
                      && AdminUsers.Contains(User.Email)
                select User).ToList();
            foreach (var user in newAdminsToAdd)
            {
                await UserManager.AddToRoleAsync(user, adminRole.Name);
                await RefreshUserClaimsStamp(user);
            }

            ViewBag.SuccessMessage = "Updated users.";
            var model = new ManageUsersViewModel();
            model.Users = Context.Users.Select(WebsiteUser => new UserAdminRole {Name = WebsiteUser.Email, IsAdmin = WebsiteUser.Roles.Any(Role => Role.RoleId == adminRole.Id)});
            return View(model);
        }

        private async Task RefreshUserClaimsStamp(ApplicationUser user)
        {
            var claim = UserManager.GetClaimsAsync(user).Result.FirstOrDefault(Claim => Claim.Type == "ClaimsStamp");
            if (claim != null)
            {
                await UserManager.RemoveClaimAsync(user, claim);
                //await UserManager.ReplaceClaimAsync(user, claim, new System.Security.Claims.Claim("ClaimsStamp", Guid.NewGuid().ToString()));
            }
            await UserManager.AddClaimAsync(user, new System.Security.Claims.Claim("ClaimsStamp", Guid.NewGuid().ToString()));
        }
    }
}