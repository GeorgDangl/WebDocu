using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Models;
using Dangl.AspNetCore.FileHandling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dangl.WebDocumentation.Services
{
    public class MarkdownDocumentsService
    {
        public const int MaxPackageBytes = 10 * 1024 * 1024;
        public const int MaxDocumentBytes = 1024 * 1024;
        public const int MaxContentBytes = 20 * 1024 * 1024;
        public const int MaxDocuments = 1000;
        private readonly ApplicationDbContext _context;
        private readonly IFileManager _fileManager;
        private readonly ILogger<MarkdownDocumentsService> _logger;

        public MarkdownDocumentsService(ApplicationDbContext context, IFileManager fileManager,
            ILogger<MarkdownDocumentsService> logger)
        {
            _context = context;
            _fileManager = fileManager;
            _logger = logger;
        }

        public async Task<RepositoryResult<int>> UploadPackageAsync(string projectName, string version,
            Stream package, CancellationToken cancellationToken)
        {
            var projectVersion = await _context.DocumentationProjectVersions
                .SingleOrDefaultAsync(v => v.ProjectName == projectName && v.Version == version, cancellationToken);
            if (projectVersion == null)
            {
                return RepositoryResult<int>.Fail("Upload the HTML documentation version before its Markdown package.");
            }

            MemoryStream compressed;
            try
            {
                compressed = await ReadBoundedAsync(package, MaxPackageBytes, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                _logger.LogWarning(exception, "Rejected Markdown package for {ProjectName} {Version}.", projectName, version);
                return RepositoryResult<int>.Fail(exception.Message);
            }
            using var compressedPackage = compressed;
            List<MarkdownDocument> documents;
            try
            {
                documents = await ReadPackageAsync(compressedPackage, cancellationToken);
            }
            catch (InvalidDataException exception)
            {
                _logger.LogWarning(exception, "Rejected Markdown package for {ProjectName} {Version}.", projectName, version);
                return RepositoryResult<int>.Fail(exception.Message);
            }
            var fileId = Guid.NewGuid();
            var packagePath = GetPackagePath(fileId);
            compressedPackage.Position = 0;
            try
            {
                var saveResult = await _fileManager.SaveFileAsync(
                    AppConstants.PROJECTS_CONTAINER, packagePath, compressedPackage);
                if (!saveResult.IsSuccess)
                {
                    await DeletePackageAsync(packagePath);
                    return RepositoryResult<int>.Fail(saveResult.ErrorMessage);
                }
            }
            catch (OperationCanceledException)
            {
                await DeletePackageAsync(packagePath);
                throw;
            }
            catch (Exception exception)
            {
                await DeletePackageAsync(packagePath);
                _logger.LogError(exception, "Failed to store Markdown package for {ProjectName} {Version}.", projectName, version);
                return RepositoryResult<int>.Fail("The Markdown package could not be stored.");
            }

            var previousFileId = projectVersion.MarkdownFileId;
            try
            {
                var existing = await _context.MarkdownDocuments
                    .Where(d => d.ProjectName == projectName && d.Version == version)
                    .ToListAsync(cancellationToken);
                var byPath = existing.ToDictionary(d => d.Path, StringComparer.OrdinalIgnoreCase);
                foreach (var document in documents)
                {
                    if (byPath.Remove(document.Path, out var stored))
                    {
                        stored.Path = document.Path;
                        stored.Title = document.Title;
                    }
                    else
                    {
                        document.ProjectName = projectName;
                        document.Version = version;
                        _context.MarkdownDocuments.Add(document);
                    }
                }

                _context.MarkdownDocuments.RemoveRange(byPath.Values);
                projectVersion.MarkdownFileId = fileId;
                projectVersion.MarkdownRevision = Guid.NewGuid();
                // The revision prevents concurrent uploads from merging two different packages.
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await DeletePackageAsync(packagePath);
                _logger.LogWarning(exception, "Concurrent Markdown upload for {ProjectName} {Version}.", projectName, version);
                return RepositoryResult<int>.Fail("The project version changed during upload. Retry the Markdown upload.");
            }
            catch (OperationCanceledException)
            {
                await DeletePackageAsync(packagePath);
                throw;
            }
            catch (Exception exception)
            {
                await DeletePackageAsync(packagePath);
                _logger.LogError(exception, "Failed to save Markdown package metadata for {ProjectName} {Version}.", projectName, version);
                return RepositoryResult<int>.Fail("The Markdown package could not be saved.");
            }

            if (previousFileId.HasValue)
            {
                await DeletePackageAsync(GetPackagePath(previousFileId.Value));
            }
            return RepositoryResult<int>.Success(documents.Count);
        }

        public async Task<string> GetDocumentContentAsync(string projectName, string version, string path,
            CancellationToken cancellationToken)
        {
            var fileId = await GetPackageFileIdAsync(projectName, version, cancellationToken);
            if (!fileId.HasValue)
            {
                return null;
            }

            var result = await _fileManager.GetFileAsync(AppConstants.PROJECTS_CONTAINER, GetPackagePath(fileId.Value));
            if (!result.IsSuccess)
            {
                return null;
            }

            using var stream = result.Value;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var normalizedPath = NormalizePath(path);
            var entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(NormalizePath(e.FullName), normalizedPath, StringComparison.OrdinalIgnoreCase));
            return entry == null ? null : await ReadEntryContentAsync(entry, cancellationToken);
        }

        public async Task<Dictionary<string, string>> GetDocumentContentsAsync(string projectName, string version,
            CancellationToken cancellationToken)
        {
            var fileId = await GetPackageFileIdAsync(projectName, version, cancellationToken);
            if (!fileId.HasValue)
            {
                return null;
            }

            var result = await _fileManager.GetFileAsync(AppConstants.PROJECTS_CONTAINER, GetPackagePath(fileId.Value));
            if (!result.IsSuccess)
            {
                return null;
            }

            using var stream = result.Value;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    || entry.FullName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                {
                    contents.Add(NormalizePath(entry.FullName), await ReadEntryContentAsync(entry, cancellationToken));
                }
            }
            return contents;
        }

        private async Task<Guid?> GetPackageFileIdAsync(string projectName, string version,
            CancellationToken cancellationToken) =>
            await _context.DocumentationProjectVersions
                .Where(v => v.ProjectName == projectName && v.Version == version)
                .Select(v => v.MarkdownFileId)
                .SingleOrDefaultAsync(cancellationToken);

        private static async Task<List<MarkdownDocument>> ReadPackageAsync(Stream compressed,
            CancellationToken cancellationToken)
        {
            compressed.Position = 0;
            using var archive = new ZipArchive(compressed, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > 2000)
            {
                throw new InvalidDataException("A Markdown package may contain at most 2000 ZIP entries.");
            }

            var documents = new List<MarkdownDocument>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                    && !entry.FullName.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = NormalizePath(entry.FullName);
                if (!paths.Add(path))
                {
                    throw new InvalidDataException("Markdown document paths must be unique, ignoring case.");
                }

                if (entry.Length > MaxDocumentBytes || totalBytes + entry.Length > MaxContentBytes || documents.Count >= MaxDocuments)
                {
                    throw new InvalidDataException("Markdown packages are limited to 1000 documents, 1 MiB per document and 20 MiB of Markdown.");
                }

                using var input = entry.Open();
                using var contentStream = await ReadBoundedAsync(input,
                    (int)Math.Min(MaxDocumentBytes, MaxContentBytes - totalBytes), cancellationToken);
                totalBytes += contentStream.Length;
                string content;
                try
                {
                    using var reader = new StreamReader(contentStream, new UTF8Encoding(false, true),
                        detectEncodingFromByteOrderMarks: false);
                    content = (await reader.ReadToEndAsync(cancellationToken)).TrimStart('\uFEFF');
                }
                catch (DecoderFallbackException exception)
                {
                    throw new InvalidDataException("Markdown documents must be encoded as UTF-8.", exception);
                }

                if (content.Contains('\0'))
                {
                    throw new InvalidDataException("Markdown documents must not contain binary content.");
                }

                var heading = content.Split('\n')
                    .FirstOrDefault(line => line.StartsWith("# ", StringComparison.Ordinal));
                var title = heading?.Substring(2).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Path.GetFileNameWithoutExtension(path);
                }
                documents.Add(new MarkdownDocument
                {
                    Path = path,
                    Title = title.Length > 300 ? title.Substring(0, 300) : title
                });
            }

            if (documents.Count == 0)
            {
                throw new InvalidDataException("The ZIP must contain at least one .md or .markdown document.");
            }

            return documents;
        }

        private static async Task<string> ReadEntryContentAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
        {
            using var input = entry.Open();
            using var contentStream = await ReadBoundedAsync(input, MaxDocumentBytes, cancellationToken);
            try
            {
                using var reader = new StreamReader(contentStream, new UTF8Encoding(false, true),
                    detectEncodingFromByteOrderMarks: false);
                return (await reader.ReadToEndAsync(cancellationToken)).TrimStart('\uFEFF');
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Markdown documents must be encoded as UTF-8.", exception);
            }
        }

        public static string GetPackagePath(Guid fileId) => $"markdown/{fileId}.zip";

        private async Task DeletePackageAsync(string packagePath)
        {
            try
            {
                var result = await _fileManager.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, packagePath);
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Could not delete Markdown package {PackagePath}: {ErrorMessage}",
                        packagePath, result.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not delete Markdown package {PackagePath}.", packagePath);
            }
        }

        public static string NormalizePath(string path)
        {
            var normalized = path?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 300
                || normalized.Any(char.IsControl) || normalized.Contains(':')
                || normalized.Split('/').Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
            {
                throw new InvalidDataException("Document paths must be relative, at most 300 characters, and contain no dot segments.");
            }

            return normalized;
        }

        private static async Task<MemoryStream> ReadBoundedAsync(Stream input, int limit, CancellationToken cancellationToken)
        {
            var output = new MemoryStream();
            try
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    if (output.Length + count > limit)
                    {
                        throw new InvalidDataException($"The package or document exceeds its {limit} byte size limit.");
                    }
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                output.Position = 0;
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }
    }
}
