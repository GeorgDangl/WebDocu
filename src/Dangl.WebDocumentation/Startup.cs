using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Data.Entity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNet.Identity;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNet.Mvc.Filters;
using System.Security.Claims;
using Microsoft.AspNet.Authorization;
using AuthorizationContext = Microsoft.AspNet.Mvc.Filters.AuthorizationContext;


namespace Dangl.WebDocumentation
{
    public class Startup
    {

        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.

            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();

                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddEntityFramework()
                .AddSqlServer()
                .AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(Configuration["Data:DefaultConnection:ConnectionString"]));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            //services.AddMvc();

            services.AddMvc(options =>
            options.Filters.Add(new RefreshUserClaimsFilterAttribute()));

            services.Configure<AppSettings>(Configuration);
        }

        public class RefreshUserClaimsFilterAttribute : IAuthorizationFilter
        {
            public void OnAuthorization(AuthorizationContext context)
            {
                var User = context.HttpContext.User;
                if (!User.Identity.IsAuthenticated)
                {
                    return;
                }
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var stampFromClaims = User.Claims.FirstOrDefault(Claim => Claim.Type == "ClaimsStamp")?.Value;
                var stampFromDb = dbContext.UserClaims.FirstOrDefault(UserClaim => UserClaim.UserId == User.GetUserId() && UserClaim.ClaimType == "ClaimsStamp")?.ClaimValue;
                if (string.IsNullOrWhiteSpace(stampFromClaims) || string.IsNullOrWhiteSpace(stampFromDb) || stampFromClaims != stampFromDb)
                {
                    var dbUser = dbContext.Users.FirstOrDefault(UserInDb => UserInDb.Id == User.GetUserId());
                    // Need to recreate
                    if (string.IsNullOrWhiteSpace(stampFromDb))
                    {
                        // No stamp at all
                        var userManager = context.HttpContext.ApplicationServices.GetRequiredService<UserManager<ApplicationUser>>();
                        userManager.AddClaimAsync(dbUser, new Claim("ClaimsStamp", Guid.NewGuid().ToString())).Wait();
                    }
                    var signInManager = context.HttpContext.ApplicationServices.GetRequiredService<SignInManager<ApplicationUser>>();
                    signInManager.RefreshSignInAsync(dbUser).Wait();
                }
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseApplicationInsightsRequestTelemetry();

            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
                app.UseStatusCodePages();
                using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
                {
                    using (var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>())
                    {
                        dbContext.Database.Migrate();
                        DatabaseInitialization.Initialize(dbContext);
                    }
                }
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");

                // For more details on creating database during deployment see http://go.microsoft.com/fwlink/?LinkID=615859
                try
                {
                    using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>()
                        .CreateScope())
                    {
                        using (var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>())
                        {
                            dbContext.Database.Migrate();
                            DatabaseInitialization.Initialize(dbContext);
                        }
                    }
                }
                catch { }
            }

            app.UseIISPlatformHandler(options => options.AuthenticationDescriptions.Clear());

            app.UseApplicationInsightsExceptionTelemetry();

            app.UseStaticFiles();

            app.UseIdentity();

            // To configure external authentication please see http://go.microsoft.com/fwlink/?LinkID=532715

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            

        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
