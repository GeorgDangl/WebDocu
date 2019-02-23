using Dangl.WebDocumentation.Dtos;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectFilesService : IProjectFilesService
    {
        private readonly ApplicationDbContext _context;
        private readonly AspNetCore.FileHandling.IFileManager _fileManager;
        private static readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();
        private readonly IProjectUploadNotificationsService _projectUploadNotificationsService;
        private readonly IProjectVersionAssetFilesService _projectVersionAssetFilesService;

        public ProjectFilesService(ApplicationDbContext context,
            Dangl.AspNetCore.FileHandling.IFileManager fileManager,
            IProjectVersionAssetFilesService projectVersionAssetFilesService,
            IProjectUploadNotificationsService projectUploadNotificationsService)
        {
            _context = context;
            _fileManager = fileManager;
            _projectUploadNotificationsService = projectUploadNotificationsService;
            _projectVersionAssetFilesService = projectVersionAssetFilesService;
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
            var versionPackageId = await _context.DocumentationProjectVersions
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
            var mimeType = GetMimeTypeForFilePath(filePath);
            var memoryStream = new MemoryStream();
            var repoResult = await _fileManager.GetFileAsync(AppConstants.PROJECTS_CONTAINER, archivePath);
            if (!repoResult.IsSuccess)
            {
                return null;
            }
            using (var fileStream = repoResult.Value)
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
                    _context.DocumentationProjectVersions.Add(newVersion);
                    await _context.SaveChangesAsync();
                    var packagePath = GetPackagePath(projectId, newVersion.FileId);

                    var repoResult = await _fileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, packagePath, zipArchiveStream);
                    if (repoResult.IsSuccess)
                    {
                        transaction.Commit();
                        await _projectUploadNotificationsService.ScheduleProjectUploadNotifications(projectName, version);
                        return true;
                    }
                    return false;
                }
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
            var projectVersion = await _context.DocumentationProjectVersions
                .FirstOrDefaultAsync(v => v.Version == version && v.Project.Id == projectId);
            if (projectVersion == null)
            {
                return false;
            }
            var packagePath = GetPackagePath(projectId, projectVersion.FileId);
            try
            {
                await _fileManager.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, packagePath);
            }
            catch
            {
                return false;
            }

            var assetFiles = await _context.ProjectVersionAssetFiles
                .Where(a => a.ProjectVersion.Version == projectVersion.Version
                        && a.ProjectVersion.Project.Id == projectId)
                .ToListAsync();

            foreach (var assetFile in assetFiles)
            {
                await _fileManager.DeleteFileAsync(assetFile.FileId, AppConstants.PROJECT_ASSETS_CONTAINER, assetFile.FileName);
                _context.ProjectVersionAssetFiles.Remove(assetFile);
            }

            _context.DocumentationProjectVersions.Remove(projectVersion);
            await _context.SaveChangesAsync();
            return true;
        }

        private string GetPackagePath(Guid projectId, Guid versionFileId)
        {
            var packagePath = Path.Combine(projectId.ToString(), versionFileId + ".zip");
            return packagePath;
        }
    }
}
