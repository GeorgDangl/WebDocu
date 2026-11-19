using Dangl.Identity.Client.Mvc.Services;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectsService : IProjectsService
    {
        private const string PUBLIC_PROJECT_NAMES_CACHE_KEY = "PublicProjectNames";
        private const string PROJECT_EXISTS_CACHE_KEY_PREFIX = "ProjectExists_";
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _memoryCache;
        private readonly IUserInfoService _userInfoService;

        public ProjectsService(ApplicationDbContext context,
            IUserInfoService userInfoService,
            IMemoryCache memoryCache)
        {
            _context = context;
            _userInfoService = userInfoService;
            _memoryCache = memoryCache;
        }

        public async Task<bool> UserHasAccessToProjectAsync(string projectName, Guid? userId = null)
        {
            if (userId == null)
            {
                var publicProjectNames = await _memoryCache.GetOrCreateAsync(PUBLIC_PROJECT_NAMES_CACHE_KEY, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    return await _context.DocumentationProjects
                        .AsNoTracking()
                        .Where(project => project.IsPublic)
                        .Select(project => project.Name)
                        .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
                });

                return publicProjectNames.Contains(projectName);
            }

            var userClaims = await _userInfoService.GetUserClaimsAsync();
            if (userClaims.Any(c => c.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME
                && c.Value == projectName))
            {
                return true;
            }

            // Find only public projects or projects where the user has access to (if logged in)
#pragma warning disable RCS1155 // Use StringComparison when comparing strings.
            var projectIsPublicOrUserHasAccess = await (from dbProject in _context.DocumentationProjects
                                                  where dbProject.Name.ToUpper() == projectName.ToUpper()
                                                        && (dbProject.IsPublic || (userId != null && _context.UserProjects.Any(projectAccess => projectAccess.UserId == userId && projectAccess.ProjectId == dbProject.Id)))
                                                  select dbProject).AnyAsync();
#pragma warning restore RCS1155 // Use StringComparison when comparing strings.
            return projectIsPublicOrUserHasAccess;
        }

        public Task<string> GetProjectNameForApiKeyAsync(string apiKey)
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

        public async Task<List<DocumentationProject>> GetAllProjectsForUserAsync(Guid? userId, string filter = null)
        {
            var projectClaims = userId == null
                ? Array.Empty<string>()
                : (await _userInfoService.GetUserClaimsAsync())
                    .Where(c => c.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME)
                    .Select(c => c.Value)
                    .ToArray();
            var projects = await GetAccessibleProjectsAsync(userId, projectClaims);
            return projects
                .Where(p => filter == null || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public Task<List<DocumentationProject>> GetAllProjectsForPrincipalAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true
                || !Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId))
            {
                throw new UnauthorizedAccessException("An authenticated user with a valid subject is required.");
            }
            return GetAccessibleProjectsAsync(userId, principal.FindAll(AppConstants.PROJECT_ACCESS_CLAIM_NAME)
                .Select(c => c.Value).ToArray());
        }

        private Task<List<DocumentationProject>> GetAccessibleProjectsAsync(Guid? userId, string[] projectClaims)
        {
            return _context
                .DocumentationProjects
                .AsNoTracking()
                .Where(project => project.IsPublic
                    || (userId != null && _context.UserProjects.Any(access => access.UserId == userId && access.ProjectId == project.Id))
                    || projectClaims.Contains(project.Name))
                .OrderBy(project => project.Name)
                .ToListAsync();
        }

        public Task<bool> ProjectExistsAsync(string projectName)
        {
            var cacheKey = PROJECT_EXISTS_CACHE_KEY_PREFIX + projectName.ToUpperInvariant();
            return _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _context.DocumentationProjects
                    .AsNoTracking()
                    .AnyAsync(project => project.Name.ToUpper() == projectName.ToUpper());
            });
        }
    }
}
