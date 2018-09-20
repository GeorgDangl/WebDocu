using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectVersionsService : IProjectVersionsService
    {
        private readonly ApplicationDbContext _context;

        public ProjectVersionsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<string>> GetProjectVersionsAsync(string projectName)
        {
            var versions = await _context.DocumentationProjectVersions
                .Where(v => v.ProjectName == projectName)
                .OrderByDescending(v => v.Version)
                .Select(v => v.Version)
                .ToListAsync();
            var semVerOrderer = new SemanticVersionsOrderer(versions);
            var orderedVersions = semVerOrderer.GetVersionsOrderedBySemanticVersionDescending();
            return orderedVersions;
        }

        public async Task<List<string>> GetAllPreviewVersionsExceptFirstAndLastAsync(string projectName)
        {
            var versions = await GetProjectVersionsAsync(projectName);

            return GetAllPreviewVersionsExceptFirstAndLast(versions);
        }

        public static List<string> GetAllPreviewVersionsExceptFirstAndLast(List<string> versions)
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
    }
}
