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

        public Task<List<string>> GetProjectVersions(string projectName)
        {
            var versions = _context.DocumentationProjectVersionss
                .Where(v => v.ProjectName == projectName)
                .OrderByDescending(v => v.Version)
                .Select(v => v.Version)
                .ToListAsync();
            return versions;
        }
    }
}
