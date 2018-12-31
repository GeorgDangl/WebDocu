using Hangfire.Dashboard;

namespace Dangl.WebDocumentation
{
    public class HangfireAdminRoleAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            return httpContext.User?.IsInRole(AppConstants.ADMIN_ROLE_NAME) ?? false;
        }
    }
}