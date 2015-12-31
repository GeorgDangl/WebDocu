using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features.Authentication;
using Microsoft.AspNet.Http.Features.Authentication.Internal;
using Microsoft.AspNet.Http.Internal;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Data.Entity;
using Xunit;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Http.Features;
using Microsoft.AspNet.Http.Features.Authentication;
using Microsoft.AspNet.Http.Features.Authentication.Internal;
using Microsoft.AspNet.Http.Internal;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Mvc;
using Microsoft.Data.Entity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dangl.WebDocumentation.Tests.Controllers
{

    public class AccountControllerTestsFixture : IDisposable
    {
        public AccountControllerTestsFixture()
        {
            var services = new ServiceCollection();
            services.AddEntityFramework()
                .AddInMemoryDatabase()
                .AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase());

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            //.AddDefaultTokenProviders();


            // Taken from https://github.com/aspnet/MusicStore/blob/dev/test/MusicStore.Test/ManageControllerTest.cs (and modified)
            // IHttpContextAccessor is required for SignInManager, and UserManager
            var context = new DefaultHttpContext();
            //context.Features.Set<IHttpAuthenticationFeature>(new HttpAuthenticationFeature() { Handler = new TestAuthHandler() });
            services.AddSingleton<IHttpContextAccessor>((h) => new HttpContextAccessor() {HttpContext = context});

            //services.AddSingleton<IHttpContextAccessor>(
            //    new HttpContextAccessor()
            //    {
            //        HttpContext = context,
            //    });

            services.AddMvc();

            services.AddLogging();

            //services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            //services.AddSingleton<HttpContext, HttpContext>();

            //var wow = new HttpContext();

            var serviceProvider = services.BuildServiceProvider();

            Context = serviceProvider.GetService<ApplicationDbContext>();
            Context.Database.EnsureCreated();
            DatabaseInitialization.Initialize(Context);

            UserManager = serviceProvider.GetService<UserManager<ApplicationUser>>();

                SignInManager = serviceProvider.GetService<SignInManager<ApplicationUser>>();

            LoggerFactory = serviceProvider.GetService<ILoggerFactory>();

        }

        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> UserManager { get; }

        public SignInManager<ApplicationUser> SignInManager { get; }
        public ILoggerFactory LoggerFactory { get; }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
        }
    }


    public class AccountControllerTests
    {
        public class General : IClassFixture<AccountControllerTestsFixture>
        {
            public ApplicationDbContext Context { get; }

            public UserManager<ApplicationUser> UserManager { get; } 

            public SignInManager<ApplicationUser> SignInManager { get; }

            public General(AccountControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                SignInManager = fixture.SignInManager;
            }

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
        }

        public class Register : IClassFixture<AccountControllerTestsFixture>
        {

            public ApplicationDbContext Context { get; }

            public UserManager<ApplicationUser> UserManager { get; }
            public SignInManager<ApplicationUser> SignInManager { get; }

            public ILoggerFactory LoggerFactory { get; }

            public Register(AccountControllerTestsFixture fixture)
            {
                Context = fixture.Context;
                UserManager = fixture.UserManager;
                SignInManager = fixture.SignInManager;
                LoggerFactory = fixture.LoggerFactory;
            }

            private Dangl.WebDocumentation.Controllers.AccountController Controller()
            {
                return new WebDocumentation.Controllers.AccountController(UserManager, SignInManager, null, null, LoggerFactory, Context);
            }

            [Fact]
            public void CanInstantiateController()
            {
                var controller = Controller();
                Assert.NotNull(controller);
            }
        }


    }


    public class TestAuthHandler : IAuthenticationHandler
    {
        public void Authenticate(AuthenticateContext context)
        {
            context.NotAuthenticated();
        }

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
    }

}
