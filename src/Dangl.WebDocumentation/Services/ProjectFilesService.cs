using System;
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
        private static readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();

        public ProjectFilesService(ApplicationDbContext context,
            string projectsRootFolder)
        {
            _context = context;
            _projectsRootFolder = projectsRootFolder;
        }

        public Task<string> GetEntryFilePathForProject(string projectName)
        {
            var entryPath = _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => p.PathToIndex)
                .FirstAsync();
            return entryPath;
        }

        public async Task<ProjectFileDto> GetFileForProject(string projectName, string version, string filePath)
        {
            var projectId = await _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync();
            if (projectId == null)
            {
                return null;
            }
            var versionPackageId = await _context.DocumentationProjectVersionss
                .Where(v => v.ProjectName == projectName && v.Version == version)
                .Select(v => new { v.FileId })
                .FirstOrDefaultAsync();
            if (versionPackageId == null)
            {
                return null;
            }
            var archivePath = GetPackagePath(projectId.Id, versionPackageId.FileId);
            var fileResult = await GetFileFromZipArchive(archivePath, filePath);
            return fileResult;
        }

        private async Task<ProjectFileDto> GetFileFromZipArchive(string archivePath, string filePath)
        {
            if (!File.Exists(archivePath))
            {
                return null;
            }
            var mimeType = GetMimeTypeForFilePath(filePath);
                    var memoryStream = new MemoryStream();
            using (var fileStream = File.OpenRead(archivePath))
            {
                using (var zipArchive = new ZipArchive(fileStream))
                {
                    var fileEntry = zipArchive.Entries.FirstOrDefault(e => e.FullName.Replace('/', '\\') == filePath.Replace('/', '\\'));
                    if (fileEntry == null)
                    {
                        return null;
                    }
                    var zipEntryStream = fileEntry.Open();
                    await zipEntryStream.CopyToAsync(memoryStream);
                }
            }
            memoryStream.Position = 0;
            return new ProjectFileDto
            {
                MimeType = mimeType,
                FileStream = memoryStream
            };
        }

        private string GetMimeTypeForFilePath(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            if (_fileExtensionContentTypeProvider.Mappings.ContainsKey(extension))
            {
                return _fileExtensionContentTypeProvider.Mappings[extension];
            }
            return "application/octet-stream";
        }

        public async Task<bool> UploadProjectPackageAsync(string projectName, string version, Stream zipArchiveStream)
        {
            var projectId = await _context.DocumentationProjects
                .Where(p => p.Name == projectName)
                .Select(p => p.Id)
                .FirstAsync();
            // Try to read as zip file
            try
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    var newVersion = new DocumentationProjectVersion
                    {
                        ProjectName = projectName,
                        Version = version
                    };
                    _context.DocumentationProjectVersionss.Add(newVersion);
                    await _context.SaveChangesAsync();
                    var packagePath = GetPackagePath(projectId, newVersion.FileId);
                    Directory.CreateDirectory(Path.Combine(_projectsRootFolder, projectId.ToString()));
                    using (var fileStream = File.Create(packagePath))
                    {
                        await zipArchiveStream.CopyToAsync(fileStream);
                    }
                    transaction.Commit();
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

        public async Task<bool> DeleteProjectVersionPackageAsync(Guid projectId, string version)
        {
            var projectVersion = await _context.DocumentationProjectVersionss
                .FirstOrDefaultAsync(v => v.Version == version && v.Project.Id == projectId);
            if (projectVersion == null)
            {
                return false;
            }
            var packagePath = GetPackagePath(projectId, projectVersion.FileId);
            try
            {
                File.Delete(packagePath);
            }
            catch
            {
                return false;
            }
            _context.DocumentationProjectVersionss.Remove(projectVersion);
            await _context.SaveChangesAsync();
            return true;
        }

        private string GetPackagePath(Guid projectId, Guid versionFileId)
        {
            var packagePath = Path.Combine(_projectsRootFolder, projectId.ToString(), versionFileId + ".zip");
            return packagePath;
        }
    }
}
