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
        private readonly IProjectsService _projects;
        private readonly IDocuUserInfoService _userInfo;
        private readonly ApplicationDbContext _context;
        private readonly MarkdownDocumentsService _documents;

        public MarkdownDocumentsController(ApplicationDbContext context, IProjectsService projects,
            IDocuUserInfoService userInfo, MarkdownDocumentsService documents)
        {
            _context = context;
            _projects = projects;
            _userInfo = userInfo;
            _documents = documents;
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
                .Select(d => d.Path).SingleOrDefaultAsync(cancellationToken);
            if (document == null)
            {
                return NotFound();
            }
            var content = await _documents.GetDocumentContentAsync(projectName, version, document, cancellationToken);
            return content == null ? NotFound() : Content(content, "text/markdown; charset=utf-8");
        }
    }
}
