using System;
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
            await _context.SaveChangesAsync();
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
            await _context.SaveChangesAsync();
            var service = CreateService();

            Assert.True(await service.ProjectExistsAsync("dangl.documentation"));
            _context.DocumentationProjects.RemoveRange(_context.DocumentationProjects);
            await _context.SaveChangesAsync();

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
            await _context.SaveChangesAsync();
            var service = CreateService();

            Assert.True(await service.UserHasAccessToProjectAsync("Dangl.PublicDocumentation"));
            _context.DocumentationProjects.RemoveRange(_context.DocumentationProjects);
            await _context.SaveChangesAsync();

            var hasAccess = await service.UserHasAccessToProjectAsync("Dangl.PrivateDocumentation");

            Assert.False(hasAccess);
            _userInfoService.Verify(service => service.GetUserClaimsAsync(), Times.Never);
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
