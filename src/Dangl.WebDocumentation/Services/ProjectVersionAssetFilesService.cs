using Dangl.Data.Shared;
using Dangl.WebDocumentation.Models;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectVersionAssetFilesService : IProjectVersionAssetFilesService
    {
        private readonly ApplicationDbContext _context;
        private readonly Dangl.AspNetCore.FileHandling.IFileManager _fileManager;

        public ProjectVersionAssetFilesService(ApplicationDbContext context,
            Dangl.AspNetCore.FileHandling.IFileManager fileManager)
        {
            _context = context;
            _fileManager = fileManager;
        }

        public async Task<bool> UploadAssetFileForProjectVersionAsync(string projectName,
            string version,
            IFormFile assetFile)
        {
            if (assetFile == null)
            {
                return false;
            }

            var projectVersionExists = await _context
                .DocumentationProjectVersions
                .AnyAsync(pv => pv.ProjectName == projectName
                    && pv.Version == version);

            if (!projectVersionExists)
            {
                return false;
            }

            var fileName = NormalizeFilename(assetFile.FileName);
            using (var assetFileStream = assetFile.OpenReadStream())
            {
                var dbFile = new ProjectVersionAssetFile
                {
                    FileName = fileName,
                    FileSizeInBytes = assetFileStream.Length,
                    ProjectName = projectName,
                    Version = version
                };

                var fileSaveResult = await _fileManager.SaveFileAsync(dbFile.FileId,
                    AppConstants.PROJECT_ASSETS_CONTAINER,
                    fileName,
                    assetFileStream);
                if (!fileSaveResult.IsSuccess)
                {
                    return false;
                }

                _context.ProjectVersionAssetFiles.Add(dbFile);
                await _context.SaveChangesAsync();
                return true;
            }
        }

        private static string NormalizeFilename(string originalFilename)
        {
            if (string.IsNullOrWhiteSpace(originalFilename))
            {
                return string.Empty;
            }

            var filename = Path.GetFileName(originalFilename).Trim();
            return filename;
        }

        public async Task<System.Collections.Generic.List<(string fileName, string prettyfiedFileSize, Guid fileId)>> GetAssetsForProjectVersionAsync(string projectName, string version)
        {
            var assets = (await _context
                .ProjectVersionAssetFiles
                .Where(a => a.ProjectName == projectName && a.Version == version)
                .Select(a => new { a.FileName, a.FileSizeInBytes, a.FileId })
                .ToListAsync())
                .Select(a => (a.FileName, a.FileSizeInBytes.Bytes().Humanize("##0.00"), a.FileId))
                .ToList();

            return assets;
        }

        public Task<RepositoryResult<Stream>> GetAssetFileStreamAsync(string fileName, Guid fileId)
        {
            return _fileManager.GetFileAsync(fileId, AppConstants.PROJECT_ASSETS_CONTAINER, fileName);
        }

        public async Task<bool> DeleteProjectAssetFileAsync(Guid fileId)
        {
            var assetFile = await _context
                .ProjectVersionAssetFiles
                .FirstOrDefaultAsync(af => af.FileId == fileId);

            if (assetFile == null)
            {
                return false;
            }

            var fileDeletionResult = await _fileManager
                .DeleteFileAsync(fileId, AppConstants.PROJECT_ASSETS_CONTAINER, assetFile.FileName);

            if (!fileDeletionResult.IsSuccess)
            {
                return false;
            }

            _context.ProjectVersionAssetFiles.Remove(assetFile);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
