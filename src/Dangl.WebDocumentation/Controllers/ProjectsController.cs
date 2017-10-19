using System.IO;
using System.Linq;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        public ProjectsController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment, UserManager<ApplicationUser> userManager)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
            UserManager = userManager;
        }

        private ApplicationDbContext Context { get; }
        private IHostingEnvironment HostingEnvironment { get; }
        private UserManager<ApplicationUser> UserManager { get; }

        /// <summary>
        /// Returns a requested file for the project if the user has access or the project is public.
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Projects/{projectName}/{*pathToFile}")]
        public IActionResult GetFile(string projectName, string pathToFile)
        {
            var userId = UserManager.GetUserId(User);
            // Find only public projects or projects where the user has access to (if logged in)
            var project = (from dbProject in Context.DocumentationProjects
                where dbProject.Name.ToUpper() == projectName.ToUpper()
                      && (dbProject.IsPublic || (!string.IsNullOrWhiteSpace(userId) && Context.UserProjects.Any(projectAccess => projectAccess.UserId == userId && projectAccess.ProjectId == dbProject.Id)))
                select dbProject).FirstOrDefault();
            if (project == null)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return NotFound();
            }
            var projectFolder = Path.Combine(HostingEnvironment.WebRootPath, "App_Data", project.FolderGuid.ToString());
            if (string.IsNullOrWhiteSpace(pathToFile))
            {
                return RedirectToAction(nameof(GetFile), new {ProjectName = projectName, PathToFile = project.PathToIndex});
            }
            var filePath = Path.Combine(projectFolder, pathToFile);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }
            if (!new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var mimeType))
            {
                mimeType = "application/octet-stream";
            }
            var fileData = System.IO.File.ReadAllBytes(filePath);
            return File(fileData, mimeType);
        }
    }
}
