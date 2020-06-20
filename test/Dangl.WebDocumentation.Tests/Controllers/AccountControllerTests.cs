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
        }
    }
}
