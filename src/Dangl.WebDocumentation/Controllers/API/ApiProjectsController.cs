using System.IO;
using System.IO.Compression;
using System.Linq;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class ApiProjectsController : Controller
    {
        public ApiProjectsController(ApplicationDbContext context, IHostingEnvironment hostingEnvironment)
        {
            Context = context;
            HostingEnvironment = hostingEnvironment;
        }

        private ApplicationDbContext Context { get; }
        private IHostingEnvironment HostingEnvironment { get; }

        /// <summary>
        ///     Provides an Api to upload projects.
        ///     Exemplary cURL usage:
        ///     curl -F "ApiKey=123" -F "ProjectPackage=@\"C:\Path\to\file.zip\"" http://localhost:10013/API/Projects/Upload
        /// </summary>
        /// <param name="apiKey">The ApiKey to authorize a project upload.</param>
        /// <param name="projectPackage">The project content as zip file.</param>
        /// <returns></returns>
        [HttpPost]
        [Route("API/Projects/Upload")]
        public IActionResult Upload(string apiKey, IFormFile projectPackage)
        {
            if (projectPackage == null)
            {
                return BadRequest();
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Not accepting empty API key -> Disable API upload to projects by setting the API key empty
                return NotFound();
            }
            var projectEntry = Context.DocumentationProjects.FirstOrDefault(project => project.ApiKey == apiKey);
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
                            return BadRequest();
                        }
                    }
                    return Ok();
                }
                catch (InvalidDataException)
                {
                    return BadRequest(new {Error = "Could not read file as zip."});
                }
                catch
                {
                    return BadRequest();
                }
            }
        }
    }
}
