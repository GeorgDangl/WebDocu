using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddLogging();

            // Taken from https://github.com/aspnet/MusicStore/blob/dev/test/MusicStore.Test/ManageControllerTest.cs (and modified)
            // IHttpContextAccessor is required for SignInManager, and UserManager
            var context = new DefaultHttpContext();
            services.AddSingleton<IHttpContextAccessor>(h => new HttpContextAccessor {HttpContext = context});

            var serviceProvider = services.BuildServiceProvider();

            Context = serviceProvider.GetService<ApplicationDbContext>();
            Context.Database.EnsureCreated();
            DatabaseInitialization.Initialize(Context);

            UserManager = serviceProvider.GetService<UserManager<ApplicationUser>>();
        }

        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
        }
    }

    public class AdminControllerTests
    {
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
                DatabaseInitialization.Initialize(Context);
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
                await Context.SaveChangesAsync();

                // Check that no user/project relationships exist
                Assert.Equal(0, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName));

                // Add users to project
                var controller = Controller();
                controller.EditProject(firstProject.Id, new ViewModels.Admin.EditProjectViewModel
                {
                    ApiKey = "123",
                    IsPublic = false,
                    PathToIndexPage = "index.html",
                    ProjectName = "First Project"
                }, new System.Collections.Generic.List<string> {"First@user.com"});

                // Check that one user/project relationships exists for the first project
                Assert.Equal(1, Context.UserProjects.Count(rel => rel.User.UserName == user.UserName && rel.ProjectId == firstProject.Id));

                // Add the same user to another project
                controller.EditProject(secondProject.Id, new ViewModels.Admin.EditProjectViewModel
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
