using System.IO;
using System.Linq;
using System.Security.Claims;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        public ProjectsController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
        }

        private ApplicationDbContext Context { get; }
        private IHostingEnvironment HostingEnvironment { get; }

        /// <summary>
        /// Returns a requested file for the project if the user has access or the project is public.
        /// </summary>
        /// <param name="ProjectName"></param>
        /// <param name="PathToFile"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Projects/{ProjectName}/{*PathToFile}")]
        public IActionResult GetFile(string ProjectName, string PathToFile)
        {
            var userId = User.GetUserId();
            // Find only public projects or projects where the user has access to (if logged in)
            var project = (from Project in Context.DocumentationProjects
                where Project.Name.ToUpper() == ProjectName.ToUpper()
                      && (Project.IsPublic || (!string.IsNullOrWhiteSpace(userId) && Context.UserProjects.Any(ProjectAccess => ProjectAccess.UserId == userId && ProjectAccess.ProjectId == Project.Id)))
                select Project).FirstOrDefault();
            if (project == null)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return NotFound();
            }
            var projectFolder = HostingEnvironment.MapPath("App_Data/" + project.FolderGuid);
            if (string.IsNullOrWhiteSpace(PathToFile))
            {
                return RedirectToAction(nameof(GetFile), new {ProjectName, PathToFile = project.PathToIndex});
            }
            var filePath = Path.Combine(projectFolder, PathToFile);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }
            string mimeType;
            if (!new FileExtensionContentTypeProvider().TryGetContentType(filePath, out mimeType))
            {
                mimeType = "application/octet-stream";
            }
            var fileData = System.IO.File.ReadAllBytes(filePath);
            return File(fileData, mimeType);
        }
    }
}