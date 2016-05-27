using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class AccountControllerTestsFixture : IDisposable
    {
        public AccountControllerTestsFixture()
        {
            var services = new ServiceCollection();

            services.AddEntityFrameworkInMemoryDatabase()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase().UseInternalServiceProvider(services.BuildServiceProvider()));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddLogging();

            // Taken from https://github.com/aspnet/MusicStore/blob/dev/test/MusicStore.Test/ManageControllerTest.cs (and modified)
            // IHttpContextAccessor is required for SignInManager, and UserManager
            var context = new DefaultHttpContext();
            context.Features.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature {Handler = new TestAuthHandler()});
            services.AddSingleton<IHttpContextAccessor>(h => new HttpContextAccessor {HttpContext = context});

            var serviceProvider = services.BuildServiceProvider();

            Context = serviceProvider.GetService<ApplicationDbContext>();
            Context.Database.EnsureCreated();
            DatabaseInitialization.Initialize(Context);

            UserManager = serviceProvider.GetService<UserManager<ApplicationUser>>();

            SignInManager = serviceProvider.GetService<SignInManager<ApplicationUser>>();

            LoggerFactory = serviceProvider.GetService<ILoggerFactory>();

            ServiceProvider = serviceProvider;
        }

        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public SignInManager<ApplicationUser> SignInManager { get; }
        public ILoggerFactory LoggerFactory { get; }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
        }

        private class TestAuthHandler : IAuthenticationHandler
        {
            public Task AuthenticateAsync(AuthenticateContext context)
            {
                context.NotAuthenticated();
                return Task.FromResult(0);
            }

            public Task ChallengeAsync(ChallengeContext context)
            {
                throw new NotImplementedException();
            }

            public void GetDescriptions(DescribeSchemesContext context)
            {
                throw new NotImplementedException();
            }

            public Task SignInAsync(SignInContext context)
            {
                throw new NotImplementedException();
            }

            public Task SignOutAsync(SignOutContext context)
            {
                throw new NotImplementedException();
            }

            public void Authenticate(AuthenticateContext context)
            {
                context.NotAuthenticated();
            }
        }
    }


    public class AccountControllerTests
    {
        public class General : IClassFixture<AccountControllerTestsFixture>
        {
            public General(AccountControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                SignInManager = fixture.SignInManager;
                LoggerFactory = fixture.LoggerFactory;
                ServiceProvider = fixture.ServiceProvider;
            }

            public ApplicationDbContext Context { get; }

            public UserManager<ApplicationUser> UserManager { get; }

            public SignInManager<ApplicationUser> SignInManager { get; }
            public ILoggerFactory LoggerFactory { get; }
            public IServiceProvider ServiceProvider { get; }

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

        public class CreateNewUser : IClassFixture<AccountControllerTestsFixture>
        {
            public CreateNewUser(AccountControllerTestsFixture fixture)
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

            public ApplicationDbContext Context { get; }

            public UserManager<ApplicationUser> UserManager { get; }
            public SignInManager<ApplicationUser> SignInManager { get; }

            public ILoggerFactory LoggerFactory { get; }
            public IServiceProvider ServiceProvider { get; }

            private AccountController Controller()
            {
                return new AccountController(UserManager, SignInManager, LoggerFactory, Context, null);
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
                var adminRole = Context.Roles.FirstOrDefault(Role => Role.Name == "Admin");

                // Ensure role is present
                Assert.NotNull(adminRole);

                var userHasAdminRole = Context.UserRoles.Any(Enrollment => Enrollment.RoleId == adminRole.Id && Enrollment.UserId == createdUser.Id);
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
                var adminRole = Context.Roles.FirstOrDefault(Role => Role.Name == "Admin");

                // Ensure role is present
                Assert.NotNull(adminRole);

                var userHasAdminRole = Context.UserRoles.Any(Enrollment => Enrollment.RoleId == adminRole.Id && Enrollment.UserId == createdUser.Id);
                Assert.False(userHasAdminRole);
            }
        }
    }
}