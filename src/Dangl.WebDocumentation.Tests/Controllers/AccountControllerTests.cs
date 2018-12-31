using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class AccountControllerTests
    {
        public class General : IClassFixture<InMemoryControllerTestsFixture>
        {
            public General(InMemoryControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                SignInManager = fixture.SignInManager;
                LoggerFactory = fixture.LoggerFactory;
                ServiceProvider = fixture.ServiceProvider;
            }

            private ApplicationDbContext Context { get; }
            private UserManager<ApplicationUser> UserManager { get; }
            private SignInManager<ApplicationUser> SignInManager { get; }
            private ILoggerFactory LoggerFactory { get; }
            private IServiceProvider ServiceProvider { get; }

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

            [Fact]
            public void SignInManagerNotNull()
            {
                Assert.NotNull(SignInManager);
            }

            [Fact]
            public void LoggerFactoryNotNull()
            {
                Assert.NotNull(LoggerFactory);
            }

            [Fact]
            public void ServiceProviderNotNull()
            {
                Assert.NotNull(ServiceProvider);
            }
        }

        public class CreateNewUser : IClassFixture<InMemoryControllerTestsFixture>
        {
            public CreateNewUser(InMemoryControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                SignInManager = fixture.SignInManager;
                LoggerFactory = fixture.LoggerFactory;
                ServiceProvider = fixture.ServiceProvider;

                Context.Database.EnsureDeleted();
                Context.Database.EnsureCreated();
                DatabaseInitialization.Initialize(Context);
            }

            private ApplicationDbContext Context { get; }
            private UserManager<ApplicationUser> UserManager { get; }
            private SignInManager<ApplicationUser> SignInManager { get; }
            private ILoggerFactory LoggerFactory { get; }
            private IServiceProvider ServiceProvider { get; }

            private AccountController Controller()
            {
                return new AccountController(UserManager, SignInManager, LoggerFactory, Context, null, null);
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
            public async Task FirstUserIsGrantedAdminRole()
            {
                Assert.Equal(0, Context.Users.Count());
                var controller = Controller();
                var model = new RegisterViewModel
                {
                    Email = "admin@example.com",
                    Password = "Hello123!",
                    ConfirmPassword = "Hello123!"
                };

                var user = new ApplicationUser {UserName = model.Email, Email = model.Email};
                var result = await controller.CreateNewUser(user, model);

                Assert.True(result.Succeeded);

                Assert.Equal(1, Context.Users.Count());

                var createdUser = Context.Users.FirstOrDefault();
                var adminRole = Context.Roles.FirstOrDefault(role => role.Name == AppConstants.ADMIN_ROLE_NAME);

                // Ensure role is present
                Assert.NotNull(adminRole);

                var userHasAdminRole = Context.UserRoles.Any(enrollment => enrollment.RoleId == adminRole.Id && enrollment.UserId == createdUser.Id);
                Assert.True(userHasAdminRole);
            }

            [Fact]
            public async Task SecondUserIsNotGrantedAdminRole()
            {
                Assert.Equal(0, Context.Users.Count());
                Context.Users.Add(new ApplicationUser {UserName = "admin@example.com", SecurityStamp = Guid.NewGuid().ToString()});
                Context.SaveChanges();
                Assert.Equal(1, Context.Users.Count());
                var controller = Controller();
                var model = new RegisterViewModel
                {
                    Email = "nonadmin@example.com",
                    Password = "Hello123!",
                    ConfirmPassword = "Hello123!"
                };

                var user = new ApplicationUser {UserName = model.Email, Email = model.Email};
                var result = await controller.CreateNewUser(user, model);

                Assert.True(result.Succeeded);

                Assert.Equal(2, Context.Users.Count());

                var createdUser = Context.Users.FirstOrDefault();
                var adminRole = Context.Roles.FirstOrDefault(role => role.Name == AppConstants.ADMIN_ROLE_NAME);

                // Ensure role is present
                Assert.NotNull(adminRole);

                var userHasAdminRole = Context.UserRoles.Any(enrollment => enrollment.RoleId == adminRole.Id && enrollment.UserId == createdUser.Id);
                Assert.False(userHasAdminRole);
            }
        }
    }
}
