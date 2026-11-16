using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonAdminsCannotDownloadPackages(bool authenticated)
        {
            var controller = CreateController();
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(authenticated ? "test" : null));

            Assert.IsType<ForbidResult>(await controller.DownloadPackage("Docs", "1.0"));
            _files.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task AdminDownloadReturnsZipAttachment()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            _files.Setup(f => f.GetProjectPackageAsync("Docs", "1.0")).ReturnsAsync(stream);

            var result = Assert.IsType<FileStreamResult>(await CreateController(isAdmin: true).DownloadPackage("Docs", "1.0"));

            Assert.Same(stream, result.FileStream);
            Assert.Equal("application/zip", result.ContentType);
            Assert.Equal("Docs_1.0.zip", result.FileDownloadName);
        }

        [Fact]
        public async Task MissingPackageReturnsNotFoundForAdmins()
        {
            _files.Setup(f => f.GetProjectPackageAsync("Docs", "1.0")).ReturnsAsync((Stream)null);

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
            var controller = new ProjectVersionsController(null, Mock.Of<IProjectVersionsService>(), Mock.Of<IProjectsService>(),
                _files.Object, Mock.Of<IDocuUserInfoService>());
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
