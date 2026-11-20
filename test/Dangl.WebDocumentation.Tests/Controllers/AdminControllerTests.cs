using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class AdminControllerTestsFixture : IDisposable
    {
        public AdminControllerTestsFixture()
        {
            var services = new ServiceCollection();
            services.AddEntityFrameworkInMemoryDatabase()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddLogging();

            // Taken from https://github.com/aspnet/MusicStore/blob/dev/test/MusicStore.Test/ManageControllerTest.cs (and modified)
            // IHttpContextAccessor is required for SignInManager, and UserManager
            var context = new DefaultHttpContext();
            services.AddSingleton<IHttpContextAccessor>(h => new HttpContextAccessor {HttpContext = context});

            var serviceProvider = services.BuildServiceProvider();

            Context = serviceProvider.GetService<ApplicationDbContext>();
            Context.Database.EnsureCreated();

            UserManager = serviceProvider.GetService<UserManager<ApplicationUser>>();
        }

        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
        }
    }

    public static class AdminControllerTests
    {
        public class Storage
        {
            [Fact]
            public async Task IndexTotalsAllPackageTypesAndAssetsPerProjectAndMarksUnknownSizes()
            {
                using var connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlite(connection).Options);
                await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                foreach (var name in new[] { "Empty", "Beta", "Alpha" })
                {
                    context.DocumentationProjects.Add(new DocumentationProject { Name = name, PathToIndex = "index.html" });
                }
                context.DocumentationProjectVersions.AddRange(
                    new DocumentationProjectVersion
                    {
                        ProjectName = "Alpha", Version = "1.0", PackageSizeInBytes = 3_000_000_000,
                        MarkdownFileId = Guid.NewGuid(), MarkdownPackageSizeInBytes = 10
                    },
                    new DocumentationProjectVersion
                    {
                        ProjectName = "Alpha", Version = "2.0-beta", PackageSizeInBytes = 4_000_000_000,
                        MarkdownFileId = Guid.NewGuid(), MarkdownPackageSizeInBytes = 30
                    },
                    new DocumentationProjectVersion
                    {
                        ProjectName = "Beta", Version = "1.0", MarkdownFileId = Guid.NewGuid()
                    },
                    new DocumentationProjectVersion
                    {
                        ProjectName = "Beta", Version = "2.0", PackageSizeInBytes = 0,
                        MarkdownPackageSizeInBytes = 999
                    });
                context.ProjectVersionAssetFiles.AddRange(
                    new ProjectVersionAssetFile
                    {
                        ProjectName = "Alpha", Version = "1.0", FileName = "one.zip", FileSizeInBytes = 20
                    },
                    new ProjectVersionAssetFile
                    {
                        ProjectName = "Alpha", Version = "2.0-beta", FileName = "two.zip", FileSizeInBytes = 40
                    },
                    new ProjectVersionAssetFile
                    {
                        ProjectName = "Beta", Version = "1.0", FileName = "beta.zip", FileSizeInBytes = 50
                    });
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
                var controller = new AdminController(context, null, null, null, null, null, null);

                var view = Assert.IsType<ViewResult>(await controller.Index(null, null));
                var projects = Assert.IsType<IndexViewModel>(view.Model).Projects.ToList();

                Assert.Equal(new[] { "Alpha", "Beta", "Empty" }, projects.Select(p => p.Name));
                Assert.Equal(7_000_000_000, projects[0].PackageSizeInBytes);
                Assert.Equal(40, projects[0].MarkdownPackageSizeInBytes);
                Assert.Equal(60, projects[0].AssetSizeInBytes);
                Assert.Equal(7_000_000_100, projects[0].TotalSizeInBytes);
                Assert.Equal(0, projects[0].PackagesWithoutSize);
                Assert.Equal(50, projects[1].TotalSizeInBytes);
                Assert.Equal(2, projects[1].PackagesWithoutSize);
                Assert.Equal(0, projects[2].TotalSizeInBytes);
                Assert.Equal(0, projects[2].PackagesWithoutSize);
            }
        }

        public class General : IClassFixture<AdminControllerTestsFixture>
        {
            public General(AdminControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
            }

            private ApplicationDbContext Context { get; }

            private UserManager<ApplicationUser> UserManager { get; }

            [Fact]
            public void ContextNotNull()
            {
                Assert.NotNull(Context);
            }

            [Fact]
            public void UserManagerNotNull()
            {
                Assert.NotNull(UserManager);
            }
        }

        public class EditProject : IClassFixture<AdminControllerTestsFixture>
        {
            public EditProject(AdminControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                Context.Database.EnsureDeleted();
                Context.Database.EnsureCreated();
            }

            private ApplicationDbContext Context { get; }
            private UserManager<ApplicationUser> UserManager { get; }

            private AdminController Controller()
            {
                return new AdminController(Context, null, null, null, null, null, null);
            }

            /// <summary>
            ///     This just tests the test setup, if the controller can be instantiated from the configured service provider
            /// </summary>
            [Fact]
            public void CanInstantiateController()
            {
                var controller = Controller();
                Assert.NotNull(controller);
            }

            [Fact]
            public async Task CanAddUserToMultipleProjects()
            {
                // Arrange
                // Add user to context
                var user = new ApplicationUser {UserName = "First@user.com", Email = "First@user.com"};
                await UserManager.CreateAsync(user, "SomePassword123!");
                user = Context.Users.FirstOrDefault(dbUser => dbUser.UserName == user.UserName);
                // Add two projects
                var firstProject = new DocumentationProject
                {
                    Name = "First Project",
                    PathToIndex = "index.html"
                };
                var secondProject = new DocumentationProject
                {
                    Name = "Second Project",
                    PathToIndex = "index.html"
                };
                Context.DocumentationProjects.Add(firstProject);
                Context.DocumentationProjects.Add(secondProject);
                await Context.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Check that no user/project relationships exist
                Assert.Equal(0, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName));

                // Add users to project
                var controller = Controller();
                await controller.EditProject(firstProject.Id, new ViewModels.Admin.EditProjectViewModel
                {
                    ApiKey = "123",
                    IsPublic = false,
                    PathToIndexPage = "index.html",
                    ProjectName = "First Project"
                }, new System.Collections.Generic.List<string> {"First@user.com"});

                // Check that one user/project relationships exists for the first project
                Assert.Equal(1, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName && rel.ProjectId == firstProject.Id));

                // Add the same user to another project
                await controller.EditProject(secondProject.Id, new ViewModels.Admin.EditProjectViewModel
                {
                    ApiKey = "456",
                    IsPublic = false,
                    PathToIndexPage = "index.html",
                    ProjectName = "Second Project"
                }, new System.Collections.Generic.List<string> {"First@user.com", "Second@user.com"});

                // Check that user is now in both projects
                Assert.Equal(1, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName && rel.ProjectId == firstProject.Id));
                Assert.Equal(1, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName && rel.ProjectId == secondProject.Id));
                Assert.Equal(2, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName));
            }
        }
    }
}
