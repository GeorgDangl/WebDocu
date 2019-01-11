using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Dangl.AspNetCore.FileHandling;
using Dangl.AspNetCore.FileHandling.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.AspNetCore.DataProtection;
using System.Collections.Generic;
using Hangfire;

namespace Dangl.WebDocumentation
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
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

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]));
            services.AddHangfire(x => x.UseSqlServerStorage(Configuration["Data:DefaultConnection:ConnectionString"]));

            services.AddIdentity<ApplicationUser, IdentityRole>(identityOptions =>
                {
                    identityOptions.Password.RequireDigit = false;
                    identityOptions.Password.RequiredLength = 12;
                    identityOptions.Password.RequireLowercase = false;
                    identityOptions.Password.RequireNonAlphanumeric = false;
                    identityOptions.Password.RequireUppercase = false;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc()
                .SetCompatibilityVersion(Microsoft.AspNetCore.Mvc.CompatibilityVersion.Latest);

            services.Configure<AppSettings>(Configuration);
            services.Configure<EmailSettings>(Configuration.GetSection(nameof(AppSettings.EmailSettings)));

            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IProjectVersionsService, ProjectVersionsService>();
            services.AddTransient<IProjectsService, ProjectsService>();
            services.AddTransient<IProjectUploadNotificationsService, ProjectUploadNotificationsService>();

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

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
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

            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseMvc(routes => { routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}"); });
        }
    }
}