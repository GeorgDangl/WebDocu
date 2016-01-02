using System;
using System.Collections.Generic;
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

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private ApplicationDbContext Context { get; }

        public ProjectsController(ApplicationDbContext context)
        {
            Context = context;
        }

        [HttpGet]
        [Route("Projects/{ProjectName}/{PathToFile}")]
        public IActionResult GetFile(string ProjectName, string PathToFile)
        {
            var userId = User.GetUserId();


            var projectAccess = (from Project in Context.DocumentationProjects
                where Project.Name.ToUpper() == ProjectName.ToUpper()
                      && Project.IsPublic || (!string.IsNullOrWhiteSpace(userId) && Context.UserProjects.Any(ProjectAccess => ProjectAccess.UserId == userId && ProjectAccess.ProjectId == Project.Name)) 
                      select Project).FirstOrDefault();
            if (projectAccess == null)
            {
                return HttpNotFound();
            }

                return HttpNotFound();
            throw new NotImplementedException();


            //return File()


        }

    }
}
