using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();

            // Initialize the database
            try
            {
                using (var scope = host.Services.CreateScope())
                {
                    using (var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>())
                    {
                        dbContext.Database.Migrate();
                    }

                    // If using azure, containers must be intialized before they can be accessed
                    if (scope.ServiceProvider.GetRequiredService<IFileManager>() is AzureBlobFileManager azureBlobHandler)
                    {
                        await azureBlobHandler.EnsureContainerCreatedAsync(AppConstants.PROJECTS_CONTAINER);
                        await azureBlobHandler.EnsureContainerCreatedAsync(AppConstants.PROJECT_ASSETS_CONTAINER);
                    }
                }
            }
            catch
            {
                /* Don't catch database initialization error at startup */
            }

            await host.RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            new HostBuilder()
            .ConfigureDefaults(args)
            .ConfigureWebHostDefaults(c => c
                .ConfigureLogging(c => c
                    .AddAzureWebAppDiagnostics())
                .UseStartup<Startup>());
    }
}
