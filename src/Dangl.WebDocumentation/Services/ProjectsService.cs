using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectsService : IProjectsService
    {
        private readonly ApplicationDbContext _context;

        public ProjectsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<bool> UserHasAccessToProject(string projectName, string userId = null)
        {
            // Find only public projects or projects where the user has access to (if logged in)
            var projectIsPublicOrUserHasAccess = (from dbProject in _context.DocumentationProjects
                                                  where dbProject.Name.ToUpper() == projectName.ToUpper()
                                                        && (dbProject.IsPublic || (!string.IsNullOrWhiteSpace(userId) && _context.UserProjects.Any(projectAccess => projectAccess.UserId == userId && projectAccess.ProjectId == dbProject.Id)))
                                                  select dbProject).AnyAsync();
            return projectIsPublicOrUserHasAccess;
        }

        public Task<string> GetProjectNameForApiKey(string apiKey)
        {
            var projectName = _context.DocumentationProjects
                .Where(p => p.ApiKey == apiKey)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();
            return projectName;
        }

        public Task<Guid> GetIdForProjectByNameAsync(string projectName)
        {
            return _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => p.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<List<DocumentationProject>> GetAllProjectsForUser(string userId)
        {
            // Get a list of all projects that the user has access to
            var accessibleProjects = await _context
                .DocumentationProjects
                .AsNoTracking()
                .Where(project => project.IsPublic)
                .ToListAsync(); // Show all public projects

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var projectsWithUserAccess = await _context
                    .UserProjects
                    .AsNoTracking()
                    .Where(assignment => assignment.UserId == userId).Select(assignment => assignment.Project)
                    .ToListAsync();
                accessibleProjects = accessibleProjects.Union(projectsWithUserAccess).ToList();
            }

            var distinctProjects = new List<DocumentationProject>();

            foreach (var project in accessibleProjects)
            {
                if (distinctProjects.Any(p => p.Name == project.Name))
                {
                    continue;
                }
                distinctProjects.Add(project);
            }

            return distinctProjects
                .OrderBy(project => project.Name)
                .ToList();
        }
    }
}
