using System;
using System.IO;
using System.Linq;
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

        public ProjectFilesServiceTests()
        {
            _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

        [Fact]
        public async Task AzureSizeQueryUsesBlobPropertiesWithoutDownloadingThePackage()
        {
            var path = await AddVersionAsync();
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Success(BlobsModelFactory.BlobProperties(contentLength: 54321)));

            Assert.Equal(54321L, await CreateService().GetProjectPackageSizeInBytesAsync("Docs", "1.0"));
        }

        [Fact]
        public async Task MetadataFailureIsReportedInsteadOfRecordingAZeroSize()
        {
            var path = await AddVersionAsync();
            _fileManager.As<IAzureBlobFileManager>()
                .Setup(m => m.GetBlobPropertiesAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<BlobProperties>.Fail("Blob unavailable"));

            var exception = await Assert.ThrowsAsync<IOException>(() => CreateService().GetProjectPackageSizeInBytesAsync("Docs", "1.0"));

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

        [Fact]
        public async Task DiskSizeQueryUsesTheStoredFile()
        {
            var folder = Directory.CreateTempSubdirectory("WebDocu-package-size-");
            try
            {
                var path = await AddVersionAsync();
                var fileManager = new DiskFileManager(folder.FullName);
                using var package = new MemoryStream(new byte[123]);
                Assert.True((await fileManager.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, path, package)).IsSuccess);

                Assert.Equal(123L, await CreateService(fileManager).GetProjectPackageSizeInBytesAsync("Docs", "1.0"));
            }
            finally
            {
                folder.Delete(recursive: true);
            }
        }

        [Theory]
        [InlineData("Unknown", "1.0")]
        [InlineData("Docs", "Unknown")]
        public async Task UnknownProjectOrVersionDoesNotAccessStorage(string projectName, string version)
        {
            await AddVersionAsync();
            var service = CreateService();

            Assert.Null(await service.GetProjectPackageAsync(projectName, version));
            Assert.Null(await service.GetProjectPackageSizeInBytesAsync(projectName, version));
        }

        [Fact]
        public async Task DownloadReturnsTheOriginalPackageStream()
        {
            var path = await AddVersionAsync();
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            _fileManager.Setup(m => m.GetFileAsync(AppConstants.PROJECTS_CONTAINER, path))
                .ReturnsAsync(RepositoryResult<Stream>.Success(stream));

            Assert.Same(stream, await CreateService().GetProjectPackageAsync("Docs", "1.0"));
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        private async Task<string> AddVersionAsync()
        {
            var version = new DocumentationProjectVersion { ProjectName = "Docs", Version = "1.0" };
            _context.DocumentationProjectVersions.Add(version);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            return Path.Combine(_context.DocumentationProjects.Single().Id.ToString(), version.FileId + ".zip");
        }

        private ProjectFilesService CreateService(IFileManager fileManager = null)
        {
            return new ProjectFilesService(_context, fileManager ?? _fileManager.Object,
                Mock.Of<IProjectVersionAssetFilesService>(), _notifications.Object, NullLogger<ProjectFilesService>.Instance);
        }
    }
}
