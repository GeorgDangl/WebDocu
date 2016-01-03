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
using Dangl.WebDocumentation.ViewModels.Account;
using Microsoft.AspNet.Hosting;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private ApplicationDbContext Context { get; }
        private IHostingEnvironment HostingEnvironment { get; }

        public ProjectsController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
        }

        [HttpGet]
        [Route("Projects/{ProjectName}/{*PathToFile}")]
        public IActionResult GetFile(string ProjectName, string PathToFile)
        {
            var userId = User.GetUserId();
            // Find only public projects or projects where the user has access to (if logged in)
            var project = (from Project in Context.DocumentationProjects
                where Project.Name.ToUpper() == ProjectName.ToUpper()
                      && Project.IsPublic || (!string.IsNullOrWhiteSpace(userId) && Context.UserProjects.Any(ProjectAccess => ProjectAccess.UserId == userId && ProjectAccess.ProjectId == Project.Id)) 
                      select Project).FirstOrDefault();
            if (project == null)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return HttpNotFound();
            }
            var projectFolder = HostingEnvironment.MapPath("App_Data/" + project.FolderGuid);
            var filePath = Path.Combine(projectFolder, PathToFile);
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound();
            }
            string mimeType;
            if (!(new Microsoft.AspNet.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType(filePath, out mimeType)))
            {
                mimeType = "application/octet-stream";
            }
            var fileData = System.IO.File.ReadAllBytes(filePath);
            return File(fileData, mimeType);
        }
    }
}
