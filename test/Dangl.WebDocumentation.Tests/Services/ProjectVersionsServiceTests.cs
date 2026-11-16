using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public static class ProjectVersionsServiceTests
    {
        public class GetProjectVersions
        {
            [Fact]
            public async Task ReturnsStoredPackageSizesInSemanticVersionOrder()
            {
                using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
                context.DocumentationProjectVersions.AddRange(
                    new DocumentationProjectVersion { ProjectName = "Docs", Version = "1.0", PackageSizeInBytes = 12345 },
                    new DocumentationProjectVersion { ProjectName = "Docs", Version = "2.0" },
                    new DocumentationProjectVersion { ProjectName = "Other", Version = "3.0", PackageSizeInBytes = 54321 });
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                var versions = await new ProjectVersionsService(context).GetProjectVersionsAsync("Docs");

                Assert.Collection(versions,
                    version =>
                    {
                        Assert.Equal("2.0", version.version);
                        Assert.Null(version.packageSizeInBytes);
                    },
                    version =>
                    {
                        Assert.Equal("1.0", version.version);
                        Assert.Equal(12345L, version.packageSizeInBytes);
                    });
            }
        }

        public class GetAllPreviewVersionsExceptFirstAndLast
        {
            [Fact]
            public void WithoutPreviewVersions_01()
            {
                var input = new[]
                {
                    "1.0.0"
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Empty(actual);
            }

            [Fact]
            public void WithoutPreviewVersions_02()
            {
                var input = new[]
                {
                    "1.0.2",
                    "1.0.1",
                    "1.0.0"
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Empty(actual);
            }

            [Fact]
            public void WithPreviewVersions_01()
            {
                var input = new[]
                {
                    "1.2.0-beta0001",
                    "1.0.2",
                    "1.0.1",
                    "1.0.0"
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Empty(actual);
            }

            [Fact]
            public void WithPreviewVersions_02()
            {
                var input = new[]
                {
                    "1.2.0-beta0001",
                    "1.0.2",
                    "1.0.1",
                    "1.0.0",
                    "1.0.0-beta0009",
                    "1.0.0-beta0007",
                    "1.0.0-beta0004"
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Equal(2, actual.Count);
                Assert.Equal("1.0.0-beta0009", actual[0]);
                Assert.Equal("1.0.0-beta0007", actual[1]);
            }

            [Fact]
            public void WithPreviewVersions_03()
            {
                var input = new[]
                {
                    "1.2.0-beta0001",
                    "1.1.0-beta0001",
                    "1.0.2",
                    "1.0.1",
                    "1.0.1beta0004",
                    "1.0.0",
                    "1.0.0-beta0004"
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Equal(2, actual.Count);
                Assert.Equal("1.1.0-beta0001", actual[0]);
                Assert.Equal("1.0.1beta0004", actual[1]);
            }

            [Fact]
            public void WithMultiplePreviewVersions_01()
            {
                var input = new[]
                {
                    "1.2.0-beta0001",
                    "1.1.0",
                    "1.1.0-beta0001",
                    "1.0.0",
                    "1.0.0-beta0004",
                    "1.0.0-beta0003",
                    "1.0.0-beta0001",
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Equal(3, actual.Count);
                Assert.Equal("1.1.0-beta0001", actual[0]);
                Assert.Equal("1.0.0-beta0004", actual[1]);
                Assert.Equal("1.0.0-beta0003", actual[2]);
            }

            [Fact]
            public void WithMultiplePreviewVersions_02()
            {
                var input = new[]
                {
                    "1.2.0-beta0004",
                    "1.2.0-beta0003",
                    "1.2.0-beta0001",
                    "1.1.0",
                    "1.1.0-beta0001",
                    "1.0.0",
                    "1.0.0-beta0004",
                    "1.0.0-beta0003",
                    "1.0.0-beta0001",
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Equal(5, actual.Count);
                Assert.Equal("1.2.0-beta0003", actual[0]);
                Assert.Equal("1.2.0-beta0001", actual[1]);
                Assert.Equal("1.1.0-beta0001", actual[2]);
                Assert.Equal("1.0.0-beta0004", actual[3]);
                Assert.Equal("1.0.0-beta0003", actual[4]);
            }

            [Fact]
            public void WithMultiplePreviewVersions_03()
            {
                var input = new[]
                {
                    "1.2.0-beta0004",
                    "1.2.0-beta0003",
                    "1.2.0-beta0001",
                    "1.1.0-beta0001",
                    "1.0.0-beta0004",
                    "1.0.0-beta0003",
                    "1.0.0-beta0001",
                }.ToList();
                var actual = ProjectVersionsService.GetAllPreviewVersionsExceptFirstAndLast(input);
                Assert.Equal(5, actual.Count);
                Assert.Equal("1.2.0-beta0003", actual[0]);
                Assert.Equal("1.2.0-beta0001", actual[1]);
                Assert.Equal("1.1.0-beta0001", actual[2]);
                Assert.Equal("1.0.0-beta0004", actual[3]);
                Assert.Equal("1.0.0-beta0003", actual[4]);
            }
        }
    }
}
