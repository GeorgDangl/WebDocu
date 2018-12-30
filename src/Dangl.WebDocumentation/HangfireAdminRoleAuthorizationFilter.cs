using Hangfire.Dashboard;

namespace Dangl.WebDocumentation
{
    public class HangfireAdminRoleAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            // TODO USE CONSTANTS FOR ADMIN ROLE NAME
            return httpContext.User?.IsInRole("Admin") ?? false;
        }
    }
}