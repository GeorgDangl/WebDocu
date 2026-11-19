using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Dangl.Identity.Client.Mvc.Services;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class ProjectsServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly MemoryCache _memoryCache;
        private readonly Mock<IUserInfoService> _userInfoService = new Mock<IUserInfoService>();

        public ProjectsServiceTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public async Task AnonymousUserCanAccessPublicProjectWithoutUserInfoLookup()
        {
            _context.DocumentationProjects.Add(new DocumentationProject
            {
                Name = "Dangl.PublicDocumentation",
                PathToIndex = "index.html",
                IsPublic = true
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var service = CreateService();

            var hasAccess = await service.UserHasAccessToProjectAsync("dangl.publicdocumentation");

            Assert.True(hasAccess);
            _userInfoService.Verify(service => service.GetUserClaimsAsync(), Times.Never);
        }

        [Fact]
        public async Task ProjectExistsUsesCachedResult()
        {
            _context.DocumentationProjects.Add(new DocumentationProject
            {
                Name = "Dangl.Documentation",
                PathToIndex = "index.html"
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var service = CreateService();

            Assert.True(await service.ProjectExistsAsync("dangl.documentation"));
            _context.DocumentationProjects.RemoveRange(_context.DocumentationProjects);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.True(await service.ProjectExistsAsync("Dangl.Documentation"));
        }

        [Fact]
        public async Task AnonymousUserCannotAccessPrivateProjectFromCachedProjectList()
        {
            _context.DocumentationProjects.AddRange(
                new DocumentationProject
                {
                    Name = "Dangl.PublicDocumentation",
                    PathToIndex = "index.html",
                    IsPublic = true
                },
                new DocumentationProject
                {
                    Name = "Dangl.PrivateDocumentation",
                    PathToIndex = "index.html",
                    IsPublic = false
                });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var service = CreateService();

            Assert.True(await service.UserHasAccessToProjectAsync("Dangl.PublicDocumentation"));
            _context.DocumentationProjects.RemoveRange(_context.DocumentationProjects);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var hasAccess = await service.UserHasAccessToProjectAsync("Dangl.PrivateDocumentation");

            Assert.False(hasAccess);
            _userInfoService.Verify(service => service.GetUserClaimsAsync(), Times.Never);
        }

        [Fact]
        public async Task BearerPrincipalUsesPublicProjectsDatabaseAssignmentsAndTrustedProjectClaimsWithoutCookieLookups()
        {
            var userId = Guid.NewGuid();
            var publicProject = new DocumentationProject { Name = "Public", IsPublic = true, PathToIndex = "index.html" };
            var assignedProject = new DocumentationProject { Name = "Assigned", PathToIndex = "index.html" };
            _context.DocumentationProjects.AddRange(publicProject, assignedProject,
                new DocumentationProject { Name = "Claimed", PathToIndex = "index.html" },
                new DocumentationProject { Name = "Hidden", PathToIndex = "index.html" });
            _context.UserProjects.Add(new UserProjectAccess { UserId = userId, Project = assignedProject });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(AppConstants.PROJECT_ACCESS_CLAIM_NAME, "Claimed")
            }, "Bearer"));
            var service = CreateService();

            var projects = await service.GetAllProjectsForPrincipalAsync(principal);

            Assert.Equal(new[] { "Assigned", "Claimed", "Public" }, projects.Select(p => p.Name));
            _userInfoService.Verify(service => service.GetUserClaimsAsync(), Times.Never);
            var otherUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString())
            }, "Bearer"));
            Assert.Equal("Public", Assert.Single(await service.GetAllProjectsForPrincipalAsync(otherUser)).Name);
        }

        public void Dispose()
        {
            _memoryCache.Dispose();
            _context.Dispose();
        }

        private ProjectsService CreateService()
        {
            return new ProjectsService(_context, _userInfoService.Object, _memoryCache);
        }
    }
}
