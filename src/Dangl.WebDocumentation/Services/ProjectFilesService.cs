using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectFilesService : IProjectFilesService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _projectsRootFolder;

        public ProjectFilesService(ApplicationDbContext context,
            string projectsRootFolder)
        {
            _context = context;
            _projectsRootFolder = projectsRootFolder;
        }

        public async Task<ProjectFileDto> GetFileForProject(string projectName, string filePath)
        {
            var projectFolderId = await _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => new { p.FolderGuid })
                .FirstOrDefaultAsync();
            if (projectFolderId == null)
            {
                return null;
            }

            var projectFilePath = Path.Combine(_projectsRootFolder, projectFolderId.FolderGuid.ToString(), filePath);
            if (!File.Exists(projectFilePath))
            {
                return null;
            }
            if (!new FileExtensionContentTypeProvider().TryGetContentType(projectFilePath, out var mimeType))
            {
                mimeType = "application/octet-stream";
            }
            var fileStream = File.OpenRead(projectFilePath);
            return new ProjectFileDto
            {
                FileStream = fileStream,
                MimeType = mimeType
            };
        }
    }
}
