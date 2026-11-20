using System.Threading;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers.API
{
    [AllowAnonymous]
    public class MarkdownDocumentsController : Controller
    {
        private readonly IProjectsService _projects;
        private readonly MarkdownDocumentsService _documents;

        public MarkdownDocumentsController(IProjectsService projects, MarkdownDocumentsService documents)
        {
            _projects = projects;
            _documents = documents;
        }

        [HttpPost("API/MarkdownDocuments/Upload")]
        [RequestSizeLimit(120 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 120 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string apiKey, string version, IFormFile markdownPackage,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Unauthorized();
            }
            var project = await _projects.GetProjectNameForApiKeyAsync(apiKey);
            if (project == null)
            {
                return Unauthorized();
            }
            if (string.IsNullOrWhiteSpace(version) || version.Length > 40 || markdownPackage == null)
            {
                return BadRequest("Specify an existing version and a Markdown ZIP in markdownPackage.");
            }
            if (markdownPackage.Length > MarkdownDocumentsService.MaxPackageBytes)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            using var package = markdownPackage.OpenReadStream();
            var result = await _documents.UploadPackageAsync(project, version, package, cancellationToken);
            return result.IsSuccess
                ? Ok(new { documentCount = result.Value })
                : BadRequest(result.ErrorMessage);
        }
    }
}
