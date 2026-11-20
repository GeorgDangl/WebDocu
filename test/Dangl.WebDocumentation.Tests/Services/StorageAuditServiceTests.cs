using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class StorageAuditServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("WebDocu-storage-audit-");
        private readonly DiskFileManager _disk;
        private readonly DocumentationProject _project = new DocumentationProject { Name = "Docs", PathToIndex = "index.html" };
        private readonly DocumentationProjectVersion _version = new DocumentationProjectVersion
        {
            ProjectName = "Docs", Version = "1.0", MarkdownFileId = Guid.NewGuid()
        };
        private readonly ProjectVersionAssetFile _asset = new ProjectVersionAssetFile
        {
            ProjectName = "Docs", Version = "1.0", FileName = "My Asset [1].ZIP", FileSizeInBytes = 7
        };

        public StorageAuditServiceTests()
        {
            _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            _disk = new DiskFileManager(_folder.FullName);
        }

        [Fact]
        public async Task DiskAuditFindsOnlyOldUnreferencedFilesAcrossBothContainersAndNeverDeletesFiles()
        {
            await AddReferencesAsync();
            await SaveFileAsync(AppConstants.PROJECTS_CONTAINER, HtmlPath(), 3);
            await SaveFileAsync(AppConstants.PROJECTS_CONTAINER, MarkdownDocumentsService.GetPackagePath(_version.MarkdownFileId.Value), 5);
            await SaveFileAsync(AppConstants.PROJECT_ASSETS_CONTAINER, _asset.FileName, 7, fileId: _asset.FileId);
            await SaveFileAsync(AppConstants.PROJECTS_CONTAINER, ProjectFilesService.GetPackagePath(Guid.NewGuid(), Guid.NewGuid()), 11);
            await SaveFileAsync(AppConstants.PROJECTS_CONTAINER, MarkdownDocumentsService.GetPackagePath(Guid.NewGuid()), 13);
            await SaveFileAsync(AppConstants.PROJECT_ASSETS_CONTAINER, "deleted.zip", 17, fileId: Guid.NewGuid());
            await SaveFileAsync(AppConstants.PROJECTS_CONTAINER, "upload-in-progress.zip", 19, recent: true);
            await SaveFileAsync(AppConstants.DATA_PROTECTION_KEYS_CONTAINER, "keys.xml", 23);
            var filesBefore = Directory.GetFiles(_folder.FullName, "*", SearchOption.AllDirectories).OrderBy(f => f).ToArray();

            var result = await CreateService(_disk).CheckForDanglingFilesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(7, result.ScannedFileCount);
            Assert.Equal(1, result.SkippedRecentFileCount);
            Assert.Equal(new long[] { 11, 13, 17 }, result.DanglingFiles.Select(f => f.SizeInBytes).OrderBy(s => s));
            Assert.Equal(41, result.DanglingSizeInBytes);
            Assert.DoesNotContain(result.DanglingFiles, f => f.Container == AppConstants.DATA_PROTECTION_KEYS_CONTAINER);
            Assert.Equal(filesBefore, Directory.GetFiles(_folder.FullName, "*", SearchOption.AllDirectories).OrderBy(f => f));
            Assert.Equal(1, await _context.DocumentationProjectVersions.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(1, await _context.ProjectVersionAssetFiles.CountAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AzureAuditReadsAllPagesAndMatchesActualProviderPathsWithoutDownloadingOrDeleting()
        {
            await AddReferencesAsync();
            var azure = new AzureBlobFileManager("UseDevelopmentStorage=true");
            var clients = new Mock<BlobServiceClient>(MockBehavior.Strict);
            var projects = new Mock<BlobContainerClient>(MockBehavior.Strict);
            var assets = new Mock<BlobContainerClient>(MockBehavior.Strict);
            clients.Setup(c => c.GetBlobContainerClient(AppConstants.PROJECTS_CONTAINER)).Returns(projects.Object);
            clients.Setup(c => c.GetBlobContainerClient(AppConstants.PROJECT_ASSETS_CONTAINER)).Returns(assets.Object);
            var orphanPath = azure.GetBlobReference(AppConstants.PROJECTS_CONTAINER,
                ProjectFilesService.GetPackagePath(Guid.NewGuid(), Guid.NewGuid())).Name;
            var pages = new[]
            {
                Page<BlobItem>.FromValues(new[]
                {
                    Blob(azure.GetBlobReference(AppConstants.PROJECTS_CONTAINER, HtmlPath()).Name, 3),
                    Blob(azure.GetBlobReference(AppConstants.PROJECTS_CONTAINER,
                        MarkdownDocumentsService.GetPackagePath(_version.MarkdownFileId.Value)).Name, 5)
                }, "next-page", Mock.Of<Response>()),
                Page<BlobItem>.FromValues(new[] { Blob(orphanPath, 11), Blob("recent.zip", 19, recent: true) },
                    null, Mock.Of<Response>())
            };
            projects.Setup(c => c.GetBlobsAsync(It.IsAny<GetBlobsOptions>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncPageable<BlobItem>.FromPages(pages));
            assets.Setup(c => c.GetBlobsAsync(It.IsAny<GetBlobsOptions>(), It.IsAny<CancellationToken>()))
                .Returns(AsyncPageable<BlobItem>.FromPages(new[]
                {
                    Page<BlobItem>.FromValues(new[]
                    {
                        Blob(azure.GetBlobReference(_asset.FileId, AppConstants.PROJECT_ASSETS_CONTAINER, _asset.FileName).Name, 7),
                        Blob("unreferenced-asset.zip", 13)
                    }, null, Mock.Of<Response>())
                }));

            var result = await CreateService(azure, clients.Object).CheckForDanglingFilesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(6, result.ScannedFileCount);
            Assert.Equal(1, result.SkippedRecentFileCount);
            Assert.Equal(new[] { orphanPath, "unreferenced-asset.zip" }, result.DanglingFiles.Select(f => f.Path));
            Assert.Equal(24, result.DanglingSizeInBytes);
            clients.Verify(c => c.GetBlobContainerClient(AppConstants.DATA_PROTECTION_KEYS_CONTAINER), Times.Never);
        }

        [Fact]
        public async Task UnusedDiskContainersDoNotNeedToExist()
        {
            var result = await CreateService(_disk).CheckForDanglingFilesAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, result.ScannedFileCount);
            Assert.Empty(result.DanglingFiles);
        }

        [Fact]
        public async Task MissingReferencedDiskContainerFailsTheAudit()
        {
            await AddReferencesAsync();

            await Assert.ThrowsAnyAsync<IOException>(() =>
                CreateService(_disk).CheckForDanglingFilesAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task AzureListingFailureIsNotReportedAsASuccessfulEmptyAudit()
        {
            var clients = new Mock<BlobServiceClient>(MockBehavior.Strict);
            var container = new Mock<BlobContainerClient>(MockBehavior.Strict);
            clients.Setup(c => c.GetBlobContainerClient(AppConstants.PROJECTS_CONTAINER)).Returns(container.Object);
            container.Setup(c => c.GetBlobsAsync(It.IsAny<GetBlobsOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new RequestFailedException(403, "Storage access denied"));

            await Assert.ThrowsAsync<RequestFailedException>(() =>
                CreateService(new AzureBlobFileManager("UseDevelopmentStorage=true"), clients.Object)
                    .CheckForDanglingFilesAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task UnsupportedStorageFailsExplicitly()
        {
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                CreateService(Mock.Of<IFileManager>()).CheckForDanglingFilesAsync(TestContext.Current.CancellationToken));
        }

        private async Task AddReferencesAsync()
        {
            _context.DocumentationProjects.Add(_project);
            _context.DocumentationProjectVersions.Add(_version);
            _context.ProjectVersionAssetFiles.Add(_asset);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        private string HtmlPath() => ProjectFilesService.GetPackagePath(_project.Id, _version.FileId);

        private StorageAuditService CreateService(IFileManager fileManager, BlobServiceClient client = null) =>
            new StorageAuditService(_context, fileManager, NullLogger<StorageAuditService>.Instance, client);

        private async Task SaveFileAsync(string container, string path, int size, Guid? fileId = null, bool recent = false)
        {
            using var stream = new MemoryStream(new byte[size]);
            var result = fileId.HasValue
                ? await _disk.SaveFileAsync(fileId.Value, container, path, stream)
                : await _disk.SaveFileAsync(container, path, stream);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            File.SetLastWriteTimeUtc(_disk.GetFilePath(fileId, container, path),
                recent ? DateTime.UtcNow : DateTime.UtcNow.AddDays(-2));
        }

        private static BlobItem Blob(string name, long size, bool recent = false) =>
            BlobsModelFactory.BlobItem(name: name, properties: BlobsModelFactory.BlobItemProperties(
                accessTierInferred: false, lastModified: recent ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddDays(-2),
                contentLength: size));

        public void Dispose()
        {
            _context.Dispose();
            _folder.Delete(recursive: true);
        }
    }
}
