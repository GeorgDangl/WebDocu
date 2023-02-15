using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            ConfigureSerilog();
            try
            {
                Log.Information("Starting web host");
                var host = CreateWebHostBuilder(args).Build();

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
                catch (Exception e)
                {
                    /* Don't catch database initialization error at startup */
                    Log.Error(e, "Error during database initialization, the app wil try to continue running.");
                }

                await host.RunAsync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureLogging(c => c
                    .AddAzureWebAppDiagnostics()
                    .AddSerilog())
                .UseSerilog()
                .UseStartup<Startup>();

        private static void ConfigureSerilog()
        {
            var logOutputTemplate = "[{Timestamp:HH:mm:ss} {MachineName} {Level:u3} {RequestId}] {SourceContext}{NewLine}    {Message:lj}{NewLine}{Exception}";
            var loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .WriteTo.Debug(outputTemplate: logOutputTemplate)
                .WriteTo.Console(outputTemplate: logOutputTemplate);

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var appSettings = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                .Get<AppSettings>();

            if (!string.IsNullOrWhiteSpace(appSettings.AzureBlobStorageLogConnectionString))
            {
                loggerConfiguration.WriteTo.AzureBlobStorage(appSettings.AzureBlobStorageLogConnectionString,
                    storageFileName: $"{{yyyy}}/{{MM}}/{{dd}}/{{HH}}/log-{environment}.txt",
                    outputTemplate: logOutputTemplate,
                    storageContainerName: "dangldocu",
                    writeInBatches: true,
                    period: TimeSpan.FromSeconds(15),
                    batchPostingLimit: 10);
            }

            Log.Logger = loggerConfiguration.CreateLogger();
        }
    }
}
