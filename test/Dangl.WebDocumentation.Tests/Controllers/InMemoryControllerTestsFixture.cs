using System;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class InMemoryControllerTestsFixture : IDisposable
    {
        public InMemoryControllerTestsFixture()
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
}
