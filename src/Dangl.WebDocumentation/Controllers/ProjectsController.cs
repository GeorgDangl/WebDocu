using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
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
        private readonly IProjectFilesService _projectFilesService;
        private readonly IProjectsService _projectsService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(UserManager<ApplicationUser> userManager,
            IProjectFilesService projectFilesService,
            IProjectsService projectsService)
        {
            _projectFilesService = projectFilesService;
            _projectsService = projectsService;
            _userManager = userManager;
        }

        /// <summary>
        /// Returns a requested file for the project if the user has access or the project is public.
        /// </summary>
        /// <param name="projectName"></param>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("Projects/{projectName}/{*pathToFile}")]
        public async Task<IActionResult> GetFile(string projectName, string pathToFile)
        {
            var userId = _userManager.GetUserId(User);
            var hasProjectAccess = await _projectsService.UserHasAccessToProject(projectName, userId);
            if (!hasProjectAccess)
            {
                // HttpNotFound for either the project not existing or the user not having access
                return NotFound();
            }
            var projectFile = await _projectFilesService.GetFileForProject(projectName, pathToFile);
            if (projectFile == null)
            {
                return NotFound();
            }
            return File(projectFile.FileStream, projectFile.MimeType);
        }
    }
}
