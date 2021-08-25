using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.ProjectAssets;
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

        public async Task<RepositoryResult<SasUploadResponse>> GetSasBlobUploadLinkAsync(string projectName, string version, SasUploadModel sasUploadModel)
        {
            if (sasUploadModel == null)
            {
                return RepositoryResult<SasUploadResponse>.Fail("The model can not be empty.");
            }

            var azureBlobManager = _fileManager as AzureBlobFileManager;
            if (azureBlobManager == null)
            {
                return RepositoryResult<SasUploadResponse>.Fail("There is no Azure Blob connection specified, the server is using regular disk storage.");
            }

            var projectVersionExists = await _context
                .DocumentationProjectVersions
                .AnyAsync(pv => pv.ProjectName == projectName
                    && pv.Version == version);

            if (!projectVersionExists)
            {
                return RepositoryResult<SasUploadResponse>.Fail($"The specified project version does not exist, version: {version}, project: {projectName}");
            }

            var fileName = NormalizeFilename(sasUploadModel.FileName);
            var dbFile = new ProjectVersionAssetFile
            {
                FileName = fileName,
                FileSizeInBytes = sasUploadModel.FileSizeInBytes,
                ProjectName = projectName,
                Version = version
            };

            var sasUri = await azureBlobManager.GetSasUploadLinkAsync(dbFile.FileId,
                AppConstants.PROJECT_ASSETS_CONTAINER,
                fileName);
            if (sasUri.IsSuccess)
            {
                _context.ProjectVersionAssetFiles.Add(dbFile);
                await _context.SaveChangesAsync();
                return RepositoryResult<SasUploadResponse>.Success(new SasUploadResponse
                {
                    UploadLink = sasUri .Value.UploadLink
                });
            }

            return RepositoryResult<SasUploadResponse>.Fail(sasUri.ErrorMessage);
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

        public async Task<RepositoryResult<(string sasDownloadUrl, Stream stream)>> GetAssetDownloadAsync(string fileName, Guid fileId)
        {
            if (_fileManager is AzureBlobFileManager azureBlobManager)
            {
                var sasDownloadUrl = await azureBlobManager.GetSasDownloadLinkAsync(fileId,
                    AppConstants.PROJECT_ASSETS_CONTAINER,
                    fileName,
                    validForMinutes: 5);
                if (sasDownloadUrl.IsSuccess)
                {
                    return RepositoryResult<(string sasDownloadUrl, Stream stream)>.Success((sasDownloadUrl.Value.DownloadLink, null));
                }
            }

            var fileManagerResult = await _fileManager.GetFileAsync(fileId, AppConstants.PROJECT_ASSETS_CONTAINER, fileName);

            if (!fileManagerResult.IsSuccess)
            {
                return RepositoryResult<(string sasDownloadUrl, Stream stream)>.Fail(fileManagerResult.ErrorMessage);
            }

            return RepositoryResult<(string sasDownloadUrl, Stream stream)>.Success((null, fileManagerResult.Value));
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
