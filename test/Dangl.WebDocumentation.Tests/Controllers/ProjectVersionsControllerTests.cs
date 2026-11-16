using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.ProjectVersions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class ProjectVersionsControllerTests
    {
        private readonly Mock<IProjectFilesService> _files = new Mock<IProjectFilesService>(MockBehavior.Strict);
        private readonly Mock<IProjectsService> _projects = new Mock<IProjectsService>(MockBehavior.Strict);
        private readonly Mock<IDocuUserInfoService> _userInfo = new Mock<IDocuUserInfoService>(MockBehavior.Strict);

        [Fact]
        public async Task AnonymousUsersCannotDownloadEvenPublicPackages()
        {
            var controller = CreateController();
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", It.IsAny<Guid?>())).ReturnsAsync(true);

            Assert.IsType<UnauthorizedResult>(await controller.DownloadPackage("Docs", "1.0"));
            _files.VerifyNoOtherCalls();
            _userInfo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task LoggedInUsersWithoutProjectAccessCannotDownloadPackages()
        {
            var userId = Guid.NewGuid();
            _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
            _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(false);

            Assert.IsType<ForbidResult>(await CreateController().DownloadPackage("Docs", "1.0"));

            _files.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AuthorizedDownloadRedirectsToSasUrl(bool isAdmin)
        {
            var userId = Guid.NewGuid();
            if (!isAdmin)
            {
                _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
                _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(true);
            }
            const string downloadUrl = "https://storage.example/Docs.zip?sas=download";
            _files.Setup(f => f.GetProjectPackageDownloadAsync("Docs", "1.0"))
                .ReturnsAsync(RepositoryResult<(string, Stream)>.Success((downloadUrl, null)));

            var result = Assert.IsType<RedirectResult>(await CreateController(isAdmin).DownloadPackage("Docs", "1.0"));

            Assert.Equal(downloadUrl, result.Url);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AuthorizedDiskDownloadReturnsZipAttachment(bool isAdmin)
        {
            if (!isAdmin)
            {
                var userId = Guid.NewGuid();
                _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
                _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(true);
            }
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            _files.Setup(f => f.GetProjectPackageDownloadAsync("Docs", "1.0"))
                .ReturnsAsync(RepositoryResult<(string, Stream)>.Success((null, stream)));

            var result = Assert.IsType<FileStreamResult>(await CreateController(isAdmin).DownloadPackage("Docs", "1.0"));

            Assert.Same(stream, result.FileStream);
            Assert.Equal("application/zip", result.ContentType);
            Assert.Equal("Docs_1.0.zip", result.FileDownloadName);
        }

        [Fact]
        public async Task MissingPackageReturnsNotFoundForAdmins()
        {
            _files.Setup(f => f.GetProjectPackageDownloadAsync("Docs", "1.0"))
                .ReturnsAsync(RepositoryResult<(string, Stream)>.Fail("Package not found"));

            Assert.IsType<NotFoundResult>(await CreateController(isAdmin: true).DownloadPackage("Docs", "1.0"));
        }

        [Fact]
        public async Task VersionsPageUsesStoredSizesWithoutQueryingPackageStorage()
        {
            var versions = new Mock<IProjectVersionsService>();
            versions.Setup(v => v.GetProjectVersionsAsync("Docs"))
                .ReturnsAsync(new List<(string, bool, bool, DateTimeOffset?, long?)>
                {
                    ("1.0", false, false, null, 12345),
                    ("0.9", false, false, null, null)
                });
            var projects = new Mock<IProjectsService>();
            projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", It.IsAny<Guid?>())).ReturnsAsync(true);
            _files.Setup(f => f.GetEntryFilePathForProjectAsync("Docs")).ReturnsAsync("index.html");
            var controller = new ProjectVersionsController(null, versions.Object, projects.Object, _files.Object,
                Mock.Of<IDocuUserInfoService>());
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            var result = Assert.IsType<ViewResult>(await controller.Index("Docs"));
            var model = Assert.IsType<IndexViewModel>(result.Model);

            Assert.Equal(12345L, model.Versions[0].PackageSizeInBytes);
            Assert.Null(model.Versions[1].PackageSizeInBytes);
            _files.Verify(f => f.GetEntryFilePathForProjectAsync("Docs"), Times.Once);
            _files.VerifyNoOtherCalls();
        }

        private ProjectVersionsController CreateController(bool isAdmin = false)
        {
            var controller = new ProjectVersionsController(null, Mock.Of<IProjectVersionsService>(), _projects.Object,
                _files.Object, _userInfo.Object);
            var identity = new ClaimsIdentity("test");
            if (isAdmin)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, AppConstants.ADMIN_ROLE_NAME));
            }

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
            return controller;
        }
    }
}
