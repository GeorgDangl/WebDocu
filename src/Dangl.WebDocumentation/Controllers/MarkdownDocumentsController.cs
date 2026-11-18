using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Controllers
{
    public class MarkdownDocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IProjectsService _projects;
        private readonly IDocuUserInfoService _userInfo;

        public MarkdownDocumentsController(ApplicationDbContext context, IProjectsService projects, IDocuUserInfoService userInfo)
        {
            _context = context;
            _projects = projects;
            _userInfo = userInfo;
        }

        [HttpGet("MarkdownDocuments/{projectName}/{version}/{*path}")]
        public async Task<IActionResult> GetDocument(string projectName, string version, string path, CancellationToken cancellationToken)
        {
            Response.Headers.CacheControl = "private, no-store";
            if (!await _projects.UserHasAccessToProjectAsync(projectName, await _userInfo.GetCurrentUserIdOrNullAsync()))
            {
                return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
            }
            string normalized;
            try
            {
                normalized = MarkdownDocumentsService.NormalizePath(path).ToUpperInvariant();
            }
            catch (InvalidDataException exception)
            {
                return BadRequest(exception.Message);
            }
            var document = await _context.MarkdownDocuments.AsNoTracking()
                .Where(d => d.ProjectName == projectName && d.Version == version && d.Path.ToUpper() == normalized)
                .Select(d => d.Content).SingleOrDefaultAsync(cancellationToken);
            return document == null ? NotFound() : Content(document, "text/markdown; charset=utf-8");
        }
    }
}
