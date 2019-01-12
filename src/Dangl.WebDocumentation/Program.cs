using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Dangl.WebDocumentation
{
    public class Program
    {
        public static async Task Main(string[] args)
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

                    // If using azure, containers must be intialized before they can be accessed
                    if (scope.ServiceProvider.GetRequiredService<IFileManager>() is AzureBlobFileManager azureBlobHandler)
                    {
                        await azureBlobHandler.EnsureContainerCreated(AppConstants.PROJECTS_CONTAINER);
                        await azureBlobHandler.EnsureContainerCreated(AppConstants.PROJECT_ASSETS_CONTAINER);
                    }
                }
            }
            catch
            {
                /* Don't catch database initialization error at startup */
            }

            await host.RunAsync();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging(c => c.AddAzureWebAppDiagnostics())
                .UseStartup<Startup>();
    }
}
