using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.WebDocumentation.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dangl.WebDocumentation.Services
{
    public class StorageAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileManager _fileManager;
        private readonly ILogger<StorageAuditService> _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public StorageAuditService(ApplicationDbContext context, IFileManager fileManager,
            ILogger<StorageAuditService> logger, BlobServiceClient blobServiceClient = null)
        {
            _context = context;
            _fileManager = fileManager;
            _logger = logger;
            _blobServiceClient = blobServiceClient;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 60)]
        public async Task<StorageAuditResult> CheckForDanglingFilesAsync(CancellationToken cancellationToken)
        {
            var result = new StorageAuditResult { CheckedAtUtc = DateTimeOffset.UtcNow };
            var cutoff = result.CheckedAtUtc.AddDays(-1);
            var comparer = _fileManager is DiskFileManager && OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var references = new Dictionary<string, HashSet<string>>
            {
                [AppConstants.PROJECTS_CONTAINER] = new HashSet<string>(comparer),
                [AppConstants.PROJECT_ASSETS_CONTAINER] = new HashSet<string>(comparer)
            };
            var versions = await _context.DocumentationProjectVersions.AsNoTracking()
                .Select(v => new { ProjectId = v.Project.Id, v.FileId, v.MarkdownFileId })
                .ToListAsync(cancellationToken);
            foreach (var version in versions)
            {
                references[AppConstants.PROJECTS_CONTAINER].Add(GetStoragePath(AppConstants.PROJECTS_CONTAINER,
                    ProjectFilesService.GetPackagePath(version.ProjectId, version.FileId)));
                if (version.MarkdownFileId.HasValue)
                {
                    references[AppConstants.PROJECTS_CONTAINER].Add(GetStoragePath(AppConstants.PROJECTS_CONTAINER,
                        MarkdownDocumentsService.GetPackagePath(version.MarkdownFileId.Value)));
                }
            }
            var assets = await _context.ProjectVersionAssetFiles.AsNoTracking()
                .Select(a => new { a.FileId, a.FileName })
                .ToListAsync(cancellationToken);
            foreach (var asset in assets)
            {
                references[AppConstants.PROJECT_ASSETS_CONTAINER].Add(GetStoragePath(
                    AppConstants.PROJECT_ASSETS_CONTAINER, asset.FileName, asset.FileId));
            }

            foreach (var container in references)
            {
                await foreach (var file in EnumerateFilesAsync(container.Key, container.Value.Count > 0, cancellationToken))
                {
                    result.ScannedFileCount++;
                    if (file.LastModifiedUtc > cutoff)
                    {
                        result.SkippedRecentFileCount++;
                        continue;
                    }
                    if (!container.Value.Contains(file.Path))
                    {
                        result.DanglingFiles.Add(file);
                        _logger.LogWarning("Unreferenced storage file: {Container}/{Path} ({SizeInBytes} bytes). No file was deleted.",
                            file.Container, file.Path, file.SizeInBytes);
                    }
                }
            }

            _logger.LogInformation("Storage audit completed: {ScannedFileCount} files scanned, {DanglingFileCount} unreferenced files ({SizeInBytes} bytes), {SkippedRecentFileCount} recent files skipped.",
                result.ScannedFileCount, result.DanglingFiles.Count, result.DanglingSizeInBytes, result.SkippedRecentFileCount);
            return result;
        }

        private string GetStoragePath(string container, string path, Guid? fileId = null)
        {
            if (_fileManager is DiskFileManager disk)
            {
                return disk.GetFilePath(fileId, container, path);
            }
            if (_fileManager is IAzureBlobFileManager azure)
            {
                return fileId.HasValue
                    ? azure.GetBlobReference(fileId.Value, container, path).Name
                    : azure.GetBlobReference(container, path).Name;
            }
            throw new NotSupportedException("The file storage provider does not support storage audits.");
        }

        private async IAsyncEnumerable<StorageAuditFile> EnumerateFilesAsync(string container, bool hasReferences,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_fileManager is DiskFileManager disk)
            {
                var root = Path.GetDirectoryName(disk.GetFilePath(null, container, "storage-audit"));
                try
                {
                    File.GetAttributes(root);
                }
                catch (DirectoryNotFoundException) when (!hasReferences)
                {
                    yield break;
                }
                catch (FileNotFoundException) when (!hasReferences)
                {
                    yield break;
                }
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };
                foreach (var file in new DirectoryInfo(root).EnumerateFiles("*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return new StorageAuditFile
                    {
                        Container = container,
                        Path = file.FullName,
                        SizeInBytes = file.Length,
                        LastModifiedUtc = file.LastWriteTimeUtc
                    };
                }
                yield break;
            }
            if (_fileManager is IAzureBlobFileManager && _blobServiceClient != null)
            {
                var client = _blobServiceClient.GetBlobContainerClient(container);
                await foreach (var blob in client.GetBlobsAsync(cancellationToken: cancellationToken))
                {
                    yield return new StorageAuditFile
                    {
                        Container = container,
                        Path = blob.Name,
                        SizeInBytes = blob.Properties.ContentLength
                            ?? throw new IOException($"Storage did not return a size for '{container}/{blob.Name}'."),
                        LastModifiedUtc = blob.Properties.LastModified
                            ?? throw new IOException($"Storage did not return a last-modified date for '{container}/{blob.Name}'.")
                    };
                }
                yield break;
            }
            throw new NotSupportedException("The file storage provider does not support storage audits.");
        }
    }

    public class StorageAuditResult
    {
        public DateTimeOffset CheckedAtUtc { get; set; }
        public long ScannedFileCount { get; set; }
        public long SkippedRecentFileCount { get; set; }
        public List<StorageAuditFile> DanglingFiles { get; set; } = new List<StorageAuditFile>();
        public long DanglingSizeInBytes => DanglingFiles.Sum(f => f.SizeInBytes);
    }

    public class StorageAuditFile
    {
        public string Container { get; set; }
        public string Path { get; set; }
        public long SizeInBytes { get; set; }
        public DateTimeOffset LastModifiedUtc { get; set; }
    }
}
