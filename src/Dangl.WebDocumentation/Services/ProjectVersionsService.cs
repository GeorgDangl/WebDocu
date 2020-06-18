using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectVersionsService : IProjectVersionsService
    {
        private readonly ApplicationDbContext _context;

        public ProjectVersionsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<(string version, bool hasAssets, bool hasChangelog)>> GetProjectVersionsAsync(string projectName)
        {
            var versions = await _context.DocumentationProjectVersions
                .Where(v => v.ProjectName == projectName)
                .Select(v => new { v.Version, HasAssets = v.AssetFiles.Any(), HasChangelog = v.MarkdownChangelog != null})
                .ToListAsync();
            var semVerOrderer = new SemanticVersionsOrderer(versions.Select(v => v.Version).ToList());
            var orderedVersions = semVerOrderer.GetVersionsOrderedBySemanticVersionDescending();
            return orderedVersions
                .Select(ov => (ov, versions.Single(v => v.Version == ov).HasAssets, versions.Single(v => v.Version == ov).HasChangelog))
                .ToList();
        }

        public async Task<List<string>> GetAllPreviewVersionsExceptFirstAndLastAsync(string projectName)
        {
            var versions = await GetProjectVersionsAsync(projectName);

            return GetAllPreviewVersionsExceptFirstAndLast(versions.Select(v => v.version));
        }

        public static List<string> GetAllPreviewVersionsExceptFirstAndLast(IEnumerable<string> versions)
        {
            var previewVersions = versions
                .Skip(1)
                .Reverse()
                .Skip(1)
                .Reverse()
                .Where(v => !SemanticVersionsOrderer.IsStableVersion(v))
                .ToList();
            return previewVersions;
        }

        public Task<bool> ProjectVersionExistsAsync(string projectName, string version)
        {
            return _context
                .DocumentationProjectVersions
                .AnyAsync(pv => pv.ProjectName == projectName && pv.Version == version);
        }
    }
}
