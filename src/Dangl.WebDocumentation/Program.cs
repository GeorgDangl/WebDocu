using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dangl.WebDocumentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();

            // Initialize the database
            try
            {
                using (var scope = host.Services.CreateScope())
                {
                    using (var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>())
                    {
                        dbContext.Database.Migrate();
                        DatabaseInitialization.Initialize(dbContext);
                    }
                }
            }
            catch
            {
                /* Don't catch database initialization error at startup */
            }

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>();
    }
}
