using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

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
    }
}
