using Dangl.WebDocumentation.Models;
using Ganss.Xss;
using Markdig;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectChangelogService : IProjectChangelogService
    {
        private readonly ApplicationDbContext _context;

        public ProjectChangelogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetChangelogInHtmlFormatAsync(string projectName, string version)
        {
            var markdownChangelog = await GetChangelogInMarkdownFormatAsync(projectName, version);
            if (markdownChangelog == null)
            {
                return null;
            }

            var html = Markdown.ToHtml(markdownChangelog);
            var htmlSanitizer = new HtmlSanitizer();
            return htmlSanitizer.Sanitize(html);
        }

        public Task<string> GetChangelogInMarkdownFormatAsync(string projectName, string version)
        {
            return _context
                .DocumentationProjectVersions
                .Where(v => v.ProjectName == projectName && v.Version == version)
                .Select(v => v.MarkdownChangelog)
                .FirstOrDefaultAsync();
        }
    }
}
