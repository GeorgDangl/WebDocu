using System;
using System.Linq;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Models
{
    public class DatabaseInitializationTestsFixture : IDisposable
    {
        public DatabaseInitializationTestsFixture()
        {
            var services = new ServiceCollection();
            services.AddEntityFrameworkInMemoryDatabase()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));

            var serviceProvider = services.BuildServiceProvider();

            Context = serviceProvider.GetService<ApplicationDbContext>();
            Context.Database.EnsureCreated();
            DatabaseInitialization.Initialize(Context);
        }

        public ApplicationDbContext Context { get; }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
        }
    }


    public class DatabaseInitializationTests : IClassFixture<DatabaseInitializationTestsFixture>
    {
        public DatabaseInitializationTests(DatabaseInitializationTestsFixture fixture)
        {
            Context = fixture.Context;
        }

        private ApplicationDbContext Context { get; }

        [Fact]
        public void EnsureContextNotNull()
        {
            Assert.NotNull(Context);
        }

        [Fact]
        public void DatabaseHasAdminRole()
        {
            Assert.True(Context.Roles.Any(role => role.Name == AppConstants.ADMIN_ROLE_NAME));
        }

        [Fact]
        public void DatabaseHasNoDuplicatedRoles()
        {
            Assert.Equal(Context.Roles.Count(), Context.Roles.Select(role => role.Name).Distinct().Count());
        }
    }
}