using Dangl.WebDocumentation.Dtos;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class AzureBlobStorageProjectFilesService : IProjectFilesService
    {
        private readonly ApplicationDbContext _context;
        private readonly AspNetCore.FileHandling.IFileManager _fileManager;
        private static readonly FileExtensionContentTypeProvider _fileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();

        public AzureBlobStorageProjectFilesService(ApplicationDbContext context,
            Dangl.AspNetCore.FileHandling.IFileManager fileManager)
        {
            _context = context;
            _fileManager = fileManager;
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
            var versionId = await _context.DocumentationProjectVersionss
                .Where(v => v.ProjectName == projectName && v.Version == version)
                .Select(v => new { v.FileId })
                .FirstOrDefaultAsync();
            if (versionId == null)
            {
                return null;
            }

            var blobFilePath = GetFilePath(projectId.Id, versionId.FileId, filePath);
            var fileResult = await _fileManager.GetFileAsync(AppConstants.PROJECTS_CONTAINER, blobFilePath);
            if (!fileResult.IsSuccess)
            {
                return null;
            }

            return new ProjectFileDto
            {
                MimeType = GetMimeTypeForFilePath(filePath),
                FileStream = fileResult.Value
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

                    await SavedZipFileContentsToBlobStorage(projectId, newVersion.FileId, zipArchiveStream);

                    transaction.Commit();
                    return true;
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

        private async Task SavedZipFileContentsToBlobStorage(Guid projectId, Guid versionId, Stream zipArchiveStream)
        {
            List<string> savedFilePaths = new List<string>();
            using (var zipArchive = new ZipArchive(zipArchiveStream))
            {
                foreach (var archiveEntry in zipArchive.Entries
                    .Where(e => e.Length > 0)) // Length == 0 means it's just a folder
                {
                    using (var entryStream = archiveEntry.Open())
                    {
                        var filePath = GetFilePath(projectId, versionId, archiveEntry.FullName);
                        savedFilePaths.Add(filePath);
                        await _fileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, filePath, entryStream).ConfigureAwait(false);
                    }
                }
            }

            var metadataFileSavePath = GetMetadataSavedFilesPath(projectId, versionId);
            savedFilePaths.Add(metadataFileSavePath);
            using (var memStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memStream, Encoding.UTF8, 4096, true))
                {
                    var metadataArray = JsonConvert.SerializeObject(savedFilePaths);
                    await streamWriter.WriteAsync(metadataArray);
                }

                memStream.Position = 0;
                await _fileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, metadataFileSavePath, memStream);
            }
        }

        // This is a bit hacky, but since there's no real concept of directories or sub folders
        // in Azure blob storage, we're just saving a metadata file of all the file paths belonging
        // to a specific project / version combination so we can easily delete it later
        private string GetMetadataSavedFilesPath(Guid projectId, Guid versionId)
        {
            return GetFilePath(projectId, versionId, "METADATA_SAVED_FILES.json");
        }

        public async Task<bool> DeleteProjectVersionPackageAsync(Guid projectId, string version)
        {
            var projectVersion = await _context.DocumentationProjectVersionss
                .FirstOrDefaultAsync(v => v.Version == version && v.Project.Id == projectId);
            if (projectVersion == null)
            {
                return false;
            }

            var metadataPath = GetMetadataSavedFilesPath(projectId, projectVersion.FileId);

            try
            {
                using (var metadataStream = (await _fileManager.GetFileAsync(AppConstants.PROJECTS_CONTAINER, metadataPath)).Value)
                {
                    using (var streamReader = new StreamReader(metadataStream))
                    {
                        var json = await streamReader.ReadToEndAsync();
                        var filePaths = JsonConvert.DeserializeObject<List<string>>(json);
                        foreach (var filePath in filePaths)
                        {
                            await _fileManager.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, filePath);
                        }
                        _context.DocumentationProjectVersionss.Remove(projectVersion);
                        await _context.SaveChangesAsync();
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private string GetFilePath(Guid projectId, Guid versionId, string filePath)
        {
            filePath = filePath.Replace('/', '\\');
            return Path.Combine(projectId.ToString(), versionId.ToString(), filePath);
        }
    }
}
