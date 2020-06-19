using Dangl.Identity.Client.Mvc.Services;
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
        private readonly IUserInfoService _userInfoService;

        public ProjectsService(ApplicationDbContext context,
            IUserInfoService userInfoService)
        {
            _context = context;
            _userInfoService = userInfoService;
        }

        public async Task<bool> UserHasAccessToProject(string projectName, Guid? userId = null)
        {
            var userClaims = await _userInfoService.GetUserClaimsAsync();
            if (userClaims.Any(c => c.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME
                && c.Value == projectName))
            {
                return true;
            }

            // Find only public projects or projects where the user has access to (if logged in)
            var projectIsPublicOrUserHasAccess = await (from dbProject in _context.DocumentationProjects
                                                  where dbProject.Name.ToUpper() == projectName.ToUpper()
                                                        && (dbProject.IsPublic || (userId != null && _context.UserProjects.Any(projectAccess => projectAccess.UserId == userId && projectAccess.ProjectId == dbProject.Id)))
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

        public async Task<List<DocumentationProject>> GetAllProjectsForUser(Guid? userId)
        {
            // Get a list of all projects that the user has access to
            var accessibleProjects = await _context
                .DocumentationProjects
                .AsNoTracking()
                .Where(project => project.IsPublic)
                .ToListAsync(); // Show all public projects

            if (userId != null)
            {
                var projectsWithUserAccess = await _context
                    .UserProjects
                    .AsNoTracking()
                    .Where(assignment => assignment.UserId == userId).Select(assignment => assignment.Project)
                    .ToListAsync();
                accessibleProjects = accessibleProjects.Union(projectsWithUserAccess).ToList();

                var projectAccessUserClaims = (await _userInfoService
                    .GetUserClaimsAsync())
                    .Where(c => c.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME)
                    .Select(c => c.Value);
                if(projectAccessUserClaims.Any())
                {
                    var projectsFromUserClaims = await _context
                        .DocumentationProjects
                        .AsNoTracking()
                        .Where(p => projectAccessUserClaims.Contains(p.Name))
                        .ToListAsync();
                    accessibleProjects = accessibleProjects.Union(projectsFromUserClaims).ToList();
                }
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

        public Task<bool> ProjectExistsAsync(string projectName)
        {
            return _context.DocumentationProjects.AnyAsync(p => p.Name == projectName);
        }
    }
}
