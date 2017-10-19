using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Repository;
using Microsoft.AspNetCore.Hosting.Internal;
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

        public async Task<bool> UploadProjectPackage(string projectName, Stream zipArchiveStream)
        {
            var projectId = await _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => p.Id)
                .FirstAsync();
            // Try to read as zip file
            try
            {
                using (var archive = new ZipArchive(zipArchiveStream))
                {
                    var result = ProjectWriter.CreateProjectFilesFromZip(archive, _projectsRootFolder, projectId, _context);
                    if (!result)
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (InvalidDataException)
            {
                // Could not read the file as zip
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
