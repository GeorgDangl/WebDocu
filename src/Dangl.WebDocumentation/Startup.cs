using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Dangl.Identity.Client;
using Dangl.Identity.Client.Mvc;
using Dangl.Identity.Client.Mvc.Configuration;
using Dangl.RestClient;
using Dangl.RestClient.Identity.Server;
using Dangl.WebDocumentation.Hangfire;
using Dangl.WebDocumentation.Middleware;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Dangl.WebDocumentation
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            builder.AddUserSecrets<Startup>();

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            var sqlConnectionString = Configuration["Data:DanglDocuSqlConnection:ConnectionString"];
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(sqlConnectionString,
                    // The production instance is on a super small Azure SQL db that has an outtage for half a minute every one or two months,
                    // which produces the following entry in the logs:
                    // An exception has been raised that is likely due to a transient failure. Consider enabling transient error resiliency by adding 'EnableRetryOnFailure()'
                    options => options.EnableRetryOnFailure()));

            GlobalJobFilters.Filters.Add(new HangfireJobExpirationAttribute());
            services.AddHangfire(x => x.UseSqlServerStorage(sqlConnectionString));

            var appSettings = Configuration.Get<AppSettings>();
            var danglIdentityServerConfig = new DanglIdentityServerConfiguration()
                    .SetBaseUri(appSettings.DanglIdentityBaseUrl)
                    .SetClientId(appSettings.DanglIdentityClientId)
                    .SetClientSecret(appSettings.DanglIdentityClientSecret)
                    .SetRequiredScope(appSettings.DanglIdentityRequiredScope)
                    .SetUseDanglIdentityOpenIdCookieAuthentication(true)
                    .SetDanglIdentityOpenIdCookieName("DanglDocuAuthentication")
                    .SetUseMemoryCacheUserInfoUpdater(true);
            services.AddControllersWithDanglIdentity<ApplicationDbContext, ApplicationUser, IdentityRole<Guid>>(danglIdentityServerConfig);
            services.AddMvc();

            services.Configure<AppSettings>(Configuration);
            services.Configure<EmailSettings>(Configuration.GetSection(nameof(AppSettings.EmailSettings)));

            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IProjectVersionsService, ProjectVersionsService>();
            services.AddTransient<IProjectsService, ProjectsService>();
            services.AddTransient<IProjectVersionAssetFilesService, ProjectVersionAssetFilesService>();
            services.AddTransient<IProjectUploadNotificationsService, ProjectUploadNotificationsService>();
            services.AddTransient<IProjectChangelogService, ProjectChangelogService>();
            services.AddTransient<IUserProjectNotificationsService, UserProjectNotificationsService>();
            services.AddTransient<IDocuUserInfoService, DocuUserInfoService>();

            var projectsRootFolder = Configuration["ProjectsRootFolder"];
            if (!string.IsNullOrWhiteSpace(projectsRootFolder))
            {
                services.AddDiskFileManager(Configuration["ProjectsRootFolder"]);
            }
            else
            {
                var azureBlobStorageConnectionString = Configuration["AzureBlobConnectionString"];
                if (!string.IsNullOrWhiteSpace(azureBlobStorageConnectionString))
                {
                    services.AddAzureBlobFileManager(azureBlobStorageConnectionString);
                    ConfigureAzureStorageDataProtectionIfRequired(services, azureBlobStorageConnectionString);
                }
            }
            services.AddTransient<IProjectFilesService, ProjectFilesService>();

            services.AddHttpClient("DanglIdentityHttpClient");
            services.AddSingleton<ITokenStorage, InMemoryTokenStorage>();
            services.AddTransient<ITokenHandler, DanglIdentityServerTokenHandler>(s =>
            {
                var config = new DanglIdentityConfig(appSettings.DanglIdentityClientId,
                    appSettings.DanglIdentityClientSecret,
                    Identity.Shared.DanglIdentityConstants.Authorization.AUTHENTICATION_CONNECTOR_API_SCOPE,
                    appSettings.DanglIdentityBaseUrl);
                var tokenStorage = s.GetRequiredService<ITokenStorage>();
                var httpClient = s.GetRequiredService<IHttpClientFactory>().CreateClient("DanglIdentityHttpClient");
                var loggerFactory = s.GetRequiredService<ILoggerFactory>();
                return new DanglIdentityServerTokenHandler(config, tokenStorage, httpClient, loggerFactory);
            });

            services.AddDanglHttpClient<DanglHttpClientAccessor>(appSettings.DanglIdentityBaseUrl);
            services.AddTransient<Identity.ApiClient.IDeletedUsersClient>(s =>
            {
                var httpClient = s.GetRequiredService<DanglHttpClientAccessor>().HttpClient;
                return new Identity.ApiClient.DeletedUsersClient(httpClient)
                {
                    BaseUrl = appSettings.DanglIdentityBaseUrl
                };
            });
            services.AddTransient<UserDeletionService>();
        }

        private static void ConfigureAzureStorageDataProtectionIfRequired(IServiceCollection services, string azureBlobStorageConnectionString)
        {
            var storageAccount = CloudStorageAccount.Parse(azureBlobStorageConnectionString);
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference(AppConstants.DATA_PROTECTION_KEYS_CONTAINER);

            // The container must exist before calling the DataProtection APIs.
            // The specific file within the container does not have to exist,
            // as it will be created on-demand.
            container.CreateIfNotExistsAsync().GetAwaiter().GetResult();

            services.AddDataProtection()
                .PersistKeysToAzureBlobStorage(container, "dangl-docu-keys.xml");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseStatusCodePages();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.Use(async (context, next) =>
            {
                context?.Response.Headers.TryAdd("X-DANGL-DOCU-VERSION", VersionsService.Version);
                await next();
            });

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseHangfireServer();
            app.UseHangfireDashboard(AppConstants.HANGFIRE_DASHBOARD_LINK, new DashboardOptions()
            {
                Authorization = new[] { new HangfireAdminRoleAuthorizationFilter() }
            });

            var hangfireRecurringInitialized = false;
            app.Use((ctx, next) =>
            {
                if (!hangfireRecurringInitialized)
                {
                    // Setup recurring job to remove users that were deleted in Dangl.Identity
                    hangfireRecurringInitialized = true;
                    var userDeletionService = ctx.RequestServices.GetRequiredService<UserDeletionService>();
                    RecurringJob.AddOrUpdate("DailyUserDeletionSyncFromDanglIdentity", () => userDeletionService.RemoveLocallyCachedDeletedUsersAsync(), Cron.Daily());
                }

                return next();
            });

            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseRouting();
            app.UseAuthorization();

            app.UseChallengeOnUnauthorizedResponse();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
            });
        }
    }
}
