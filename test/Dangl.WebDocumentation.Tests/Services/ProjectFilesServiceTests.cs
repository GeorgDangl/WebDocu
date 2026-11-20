using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class ProjectFilesServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IFileManager> _fileManager = new Mock<IFileManager>(MockBehavior.Strict);
        private readonly Mock<IProjectUploadNotificationsService> _notifications = new Mock<IProjectUploadNotificationsService>();
        private readonly ConcurrentVersionUpdateInterceptor _concurrencyInterceptor;

        public ProjectFilesServiceTests()
        {
            _concurrencyInterceptor = new ConcurrentVersionUpdateInterceptor();
            _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(_concurrencyInterceptor)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options);
            _context.DocumentationProjects.Add(new DocumentationProject
            {
                Id = Guid.NewGuid(),
                Name = "Docs",
                PathToIndex = "index.html"
            });
            _context.SaveChanges();
        }

        [Fact]
        public async Task UploadUsesStoredMetadataAfterSavingWithoutInspectingTheInputStream()
        {
            var streamMock = new Mock<Stream>(MockBehavior.Strict);
            streamMock.Setup(s => s.Close());
            using var stream = streamMock.Object;
            var saved = false;
            _fileManager.Setup(m => m.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>(), stream))
                .Callback(() => saved = true)
                .ReturnsAsync(RepositoryResult.Success());
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()))
                .Callback(() => Assert.True(saved))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Success(BlobsModelFactory.BlobProperties(contentLength: 12345)));

            var result = await CreateService().UploadProjectPackageAsync("Docs", "1.0", null, stream);

            Assert.True(result);
            _context.ChangeTracker.Clear();
            Assert.Equal(12345L, (await _context.DocumentationProjectVersions.SingleAsync(TestContext.Current.CancellationToken)).PackageSizeInBytes);
            _notifications.Verify(n => n.ScheduleProjectUploadNotificationsAsync("Docs", "1.0"), Times.Once);
        }

        [Fact]
        public async Task FailedUploadDoesNotQueryMetadataOrNotifyUsers()
        {
            using var stream = new MemoryStream();
            _fileManager.Setup(m => m.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>(), stream))
                .ReturnsAsync(RepositoryResult.Fail("Upload failed"));

            Assert.False(await CreateService().UploadProjectPackageAsync("Docs", "1.0", null, stream));

            _notifications.Verify(n => n.ScheduleProjectUploadNotificationsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AzureSizeQueryUsesBlobPropertiesWithoutDownloadingThePackage(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Success(BlobsModelFactory.BlobProperties(contentLength: 54321)));

            var service = CreateService();
            Assert.Equal(54321L, markdown
                ? await service.GetMarkdownPackageSizeInBytesAsync("Docs", "1.0")
                : await service.GetProjectPackageSizeInBytesAsync("Docs", "1.0"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MetadataFailureIsReportedInsteadOfRecordingAZeroSize(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Fail("Blob unavailable"));

            var service = CreateService();
            var exception = await Assert.ThrowsAsync<IOException>(() => markdown
                ? service.GetMarkdownPackageSizeInBytesAsync("Docs", "1.0")
                : service.GetProjectPackageSizeInBytesAsync("Docs", "1.0"));

            Assert.Contains("Blob unavailable", exception.Message);
        }

        [Fact]
        public async Task UploadDoesNotReportSuccessOrNotifyUsersWhenMetadataCannotBeRead()
        {
            using var stream = new MemoryStream();
            _fileManager.Setup(m => m.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>(), stream))
                .ReturnsAsync(RepositoryResult.Success());
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Fail("Blob unavailable"));

            Assert.False(await CreateService().UploadProjectPackageAsync("Docs", "1.0", null, stream));

            _notifications.Verify(n => n.ScheduleProjectUploadNotificationsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DiskSizeQueryUsesTheStoredFile(bool markdown)
        {
            var folder = Directory.CreateTempSubdirectory("WebDocu-package-size-");
            try
            {
                var path = await AddVersionAsync(markdown);
                var fileManager = new DiskFileManager(folder.FullName);
                using var package = new MemoryStream(new byte[123]);
                Assert.True((await fileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, path, package)).IsSuccess);

                var service = CreateService(fileManager);
                Assert.Equal(123L, markdown
                    ? await service.GetMarkdownPackageSizeInBytesAsync("Docs", "1.0")
                    : await service.GetProjectPackageSizeInBytesAsync("Docs", "1.0"));
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }

        [Theory]
        [InlineData("Unknown", "1.0", false)]
        [InlineData("Docs", "Unknown", false)]
        [InlineData("Unknown", "1.0", true)]
        [InlineData("Docs", "Unknown", true)]
        public async Task UnknownProjectOrVersionDoesNotAccessStorage(string projectName, string version, bool markdown)
        {
            await AddVersionAsync();
            var service = CreateService();

            var result = markdown
                ? await service.GetMarkdownPackageDownloadAsync(projectName, version)
                : await service.GetProjectPackageDownloadAsync(projectName, version);
            Assert.False(result.IsSuccess);
            Assert.Null(await service.GetProjectPackageSizeInBytesAsync(projectName, version));
            Assert.Null(await service.GetMarkdownPackageSizeInBytesAsync(projectName, version));
        }

        [Fact]
        public async Task VersionWithoutMarkdownDoesNotDownloadItsHtmlPackage()
        {
            await AddVersionAsync();

            var result = await CreateService().GetMarkdownPackageDownloadAsync("Docs", "1.0");

            Assert.False(result.IsSuccess);
            Assert.Null(await CreateService().GetMarkdownPackageSizeInBytesAsync("Docs", "1.0"));
            _fileManager.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DownloadReturnsTheOriginalPackageStream(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            _fileManager.Setup(m => m.GetFileAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<Stream>.Success(stream));

            var result = await DownloadAsync(markdown);

            Assert.True(result.IsSuccess);
            Assert.Same(stream, result.Value.stream);
            Assert.Null(result.Value.sasDownloadUrl);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AzureDownloadUsesShortLivedSasLinkWithFriendlyFilenameWithoutReadingPackage(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            var fileName = markdown ? "Docs_1.0_markdown.zip" : "Docs_1.0.zip";
            const string downloadUrl = "https://storage.example/Docs.zip?sas=download";
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetSasDownloadLinkAsync(AppConstants.PROJECTS_CONTAINER, path, 5, fileName))
                .ReturnsAsync(RepositoryResult<SasDownloadLink>.Success(new SasDownloadLink { DownloadLink = downloadUrl }));

            var result = await DownloadAsync(markdown);

            Assert.True(result.IsSuccess);
            Assert.Equal(downloadUrl, result.Value.sasDownloadUrl);
            Assert.Null(result.Value.stream);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedSasGenerationDoesNotFallBackToProxyingThePackage(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            var fileName = markdown ? "Docs_1.0_markdown.zip" : "Docs_1.0.zip";
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetSasDownloadLinkAsync(AppConstants.PROJECTS_CONTAINER, path, 5, fileName))
                .ReturnsAsync(RepositoryResult<SasDownloadLink>.Fail("Cannot generate SAS"));

            var result = await DownloadAsync(markdown);

            Assert.False(result.IsSuccess);
            Assert.Equal("Cannot generate SAS", result.ErrorMessage);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task FailedDiskDownloadReportsStorageError(bool markdown)
        {
            var path = await AddVersionAsync(markdown);
            _fileManager.Setup(m => m.GetFileAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<Stream>.Fail("Package missing"));

            var result = await DownloadAsync(markdown);

            Assert.False(result.IsSuccess);
            Assert.Equal("Package missing", result.ErrorMessage);
        }

        [Fact]
        public async Task VersionIsRemovedBeforeItsPackageFilesAreDeleted()
        {
            var packagePath = await AddVersionAsync();
            var markdownFileId = Guid.NewGuid();
            var version = await _context.DocumentationProjectVersions.SingleAsync(TestContext.Current.CancellationToken);
            version.MarkdownFileId = markdownFileId;
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _fileManager.Setup(m => m.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()))
                .Callback<string, string>((_, _) =>
                    Assert.False(_context.DocumentationProjectVersions.Any()))
                .ReturnsAsync(RepositoryResult.Success());

            var result = await CreateService().DeleteProjectVersionPackageAsync(
                _context.DocumentationProjects.Single().Id, "1.0");

            Assert.True(result);
            _fileManager.Verify(m => m.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, packagePath), Times.Once);
            _fileManager.Verify(m => m.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER,
                MarkdownDocumentsService.GetPackagePath(markdownFileId)), Times.Once);
        }

        [Fact]
        public async Task ConcurrentVersionChangePreventsDeletingPackageFiles()
        {
            await AddVersionAsync();
            var originalRevision = Guid.NewGuid();
            var version = await _context.DocumentationProjectVersions.SingleAsync(TestContext.Current.CancellationToken);
            version.MarkdownRevision = originalRevision;
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _concurrencyInterceptor.ThrowOnNextDeleteConflict = true;

            var result = await CreateService().DeleteProjectVersionPackageAsync(
                _context.DocumentationProjects.Single().Id, "1.0");

            Assert.False(result);
            _fileManager.Verify(m => m.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()), Times.Never);
            var storedVersion = await _context.DocumentationProjectVersions.AsNoTracking()
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(originalRevision, storedVersion.MarkdownRevision);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        private async Task<string> AddVersionAsync(bool markdown = false)
        {
            var version = new DocumentationProjectVersion
            {
                ProjectName = "Docs", Version = "1.0", MarkdownFileId = markdown ? Guid.NewGuid() : null
            };
            _context.DocumentationProjectVersions.Add(version);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return markdown
                ? MarkdownDocumentsService.GetPackagePath(version.MarkdownFileId.Value)
                : Path.Combine(_context.DocumentationProjects.Single().Id.ToString(), version.FileId + ".zip");
        }

        private Task<RepositoryResult<(string sasDownloadUrl, Stream stream)>> DownloadAsync(bool markdown)
        {
            var service = CreateService();
            return markdown
                ? service.GetMarkdownPackageDownloadAsync("Docs", "1.0")
                : service.GetProjectPackageDownloadAsync("Docs", "1.0");
        }

        private ProjectFilesService CreateService(IFileManager fileManager = null)
        {
            return new ProjectFilesService(_context, fileManager ?? _fileManager.Object,
                Mock.Of<IProjectVersionAssetFilesService>(), _notifications.Object, NullLogger<ProjectFilesService>.Instance);
        }

        private sealed class ConcurrentVersionUpdateInterceptor : SaveChangesInterceptor
        {
            public bool ThrowOnNextDeleteConflict { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (ThrowOnNextDeleteConflict
                    && eventData.Context.ChangeTracker.Entries<DocumentationProjectVersion>()
                        .Any(entry => entry.State == EntityState.Deleted))
                {
                    ThrowOnNextDeleteConflict = false;
                    throw new DbUpdateConcurrencyException("Simulated concurrent Markdown upload.");
                }

                return ValueTask.FromResult(result);
            }
        }
    }
}
