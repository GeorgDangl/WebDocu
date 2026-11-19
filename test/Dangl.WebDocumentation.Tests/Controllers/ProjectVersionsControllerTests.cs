using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.ProjectVersions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class ProjectVersionsControllerTests
    {
        private readonly Mock<IProjectFilesService> _files = new Mock<IProjectFilesService>(MockBehavior.Strict);
        private readonly Mock<IProjectsService> _projects = new Mock<IProjectsService>(MockBehavior.Strict);
        private readonly Mock<IDocuUserInfoService> _userInfo = new Mock<IDocuUserInfoService>(MockBehavior.Strict);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AnonymousUsersCannotDownloadEvenPublicPackages(bool markdown)
        {
            var controller = CreateController();
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", It.IsAny<Guid?>())).ReturnsAsync(true);

            Assert.IsType<UnauthorizedResult>(await DownloadAsync(controller, markdown));
            _files.VerifyNoOtherCalls();
            _userInfo.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LoggedInUsersWithoutProjectAccessCannotDownloadPackages(bool markdown)
        {
            var userId = Guid.NewGuid();
            _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
            _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(false);

            Assert.IsType<ForbidResult>(await DownloadAsync(CreateController(), markdown));

            _files.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task AuthorizedDownloadRedirectsToSasUrl(bool isAdmin, bool markdown)
        {
            var userId = Guid.NewGuid();
            if (!isAdmin)
            {
                _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
                _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(true);
            }
            const string downloadUrl = "https://storage.example/Docs.zip?sas=download";
            SetupDownload(markdown, RepositoryResult<(string, Stream)>.Success((downloadUrl, null)));

            var result = Assert.IsType<RedirectResult>(await DownloadAsync(CreateController(isAdmin), markdown));

            Assert.Equal(downloadUrl, result.Url);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task AuthorizedDiskDownloadReturnsZipAttachment(bool isAdmin, bool markdown)
        {
            if (!isAdmin)
            {
                var userId = Guid.NewGuid();
                _userInfo.Setup(u => u.GetCurrentUserIdOrNullAsync()).ReturnsAsync(userId);
                _projects.Setup(p => p.UserHasAccessToProjectAsync("Docs", userId)).ReturnsAsync(true);
            }
            using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
            SetupDownload(markdown, RepositoryResult<(string, Stream)>.Success((null, stream)));

            var result = Assert.IsType<FileStreamResult>(await DownloadAsync(CreateController(isAdmin), markdown));

            Assert.Same(stream, result.FileStream);
            Assert.Equal("application/zip", result.ContentType);
            Assert.Equal(markdown ? "Docs_1.0_markdown.zip" : "Docs_1.0.zip", result.FileDownloadName);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MissingPackageReturnsNotFoundForAdmins(bool markdown)
        {
            SetupDownload(markdown, RepositoryResult<(string, Stream)>.Fail("Package not found"));

            Assert.IsType<NotFoundResult>(await DownloadAsync(CreateController(isAdmin: true), markdown));
        }

        [Fact]
        public async Task VersionsPageUsesStoredSizesWithoutQueryingPackageStorage()
        {
            var versions = new Mock<IProjectVersionsService>();
            versions.Setup(v => v.GetProjectVersionsAsync("Docs"))
                .ReturnsAsync(new List<(string, bool, bool, DateTimeOffset?, long?, bool)>
                {
                    ("1.0", false, false, null, 12345, true),
                    ("0.9", false, false, null, null, false)
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
            Assert.True(model.Versions[0].HasMarkdownPackage);
            Assert.False(model.Versions[1].HasMarkdownPackage);
            _files.Verify(f => f.GetEntryFilePathForProjectAsync("Docs"), Times.Once);
            _files.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task VersionsViewOffersMarkdownDownloadOnlyWhenAvailableAndAuthenticated(bool authenticated, bool hasMarkdown)
        {
            var model = new IndexViewModel
            {
                ProjectName = "Docs",
                PathToIndex = "index.html",
                Versions = new List<ProjectVersionViewModel>
                {
                    new ProjectVersionViewModel { Version = "1.0", HasMarkdownPackage = hasMarkdown },
                    new ProjectVersionViewModel { Version = "0.9" }
                }
            };
            using var host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services =>
                    services.AddControllersWithViews().AddApplicationPart(typeof(ProjectVersionsController).Assembly))
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/", async context =>
                        {
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(authenticated ? "test" : null));
                            var result = new PartialViewResult
                            {
                                ViewName = "/Views/ProjectVersions/Index.cshtml",
                                ViewData = new ViewDataDictionary<IndexViewModel>(new EmptyModelMetadataProvider(),
                                    new ModelStateDictionary()) { Model = model }
                            };
                            await result.ExecuteResultAsync(new ActionContext(context, context.GetRouteData(),
                                new ActionDescriptor()));
                        });
                    });
                })).Start();
            using var client = host.GetTestClient();

            var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

            if (authenticated && hasMarkdown)
            {
                Assert.Contains("Download Markdown Docs Package", html);
                Assert.Contains("href=\"/Projects/Docs/DownloadMarkdown/1.0\"", html);
            }
            else
            {
                Assert.DoesNotContain("Download Markdown Docs Package", html);
                Assert.DoesNotContain("/DownloadMarkdown/", html);
            }
            Assert.DoesNotContain("/Projects/Docs/DownloadMarkdown/0.9", html);
        }

        private void SetupDownload(bool markdown, RepositoryResult<(string, Stream)> result)
        {
            if (markdown)
            {
                _files.Setup(f => f.GetMarkdownPackageDownloadAsync("Docs", "1.0")).ReturnsAsync(result);
            }
            else
            {
                _files.Setup(f => f.GetProjectPackageDownloadAsync("Docs", "1.0")).ReturnsAsync(result);
            }
        }

        private static Task<IActionResult> DownloadAsync(ProjectVersionsController controller, bool markdown) =>
            markdown ? controller.DownloadMarkdownPackage("Docs", "1.0") : controller.DownloadPackage("Docs", "1.0");

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
