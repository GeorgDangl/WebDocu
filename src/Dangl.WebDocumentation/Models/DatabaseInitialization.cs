using System.Linq;
using Microsoft.AspNetCore.Identity;

namespace Dangl.WebDocumentation.Models
{
    public static class DatabaseInitialization
    {
        public static void Initialize(ApplicationDbContext context)
        {
            SetUpRoles(context);
        }

        private static void SetUpRoles(ApplicationDbContext context)
        {
            // Add Admin role if not present
            if (context.Roles.All(role => role.Name != AppConstants.ADMIN_ROLE_NAME))
            {
                context.Roles.Add(new IdentityRole { Name = AppConstants.ADMIN_ROLE_NAME, NormalizedName = AppConstants.ADMIN_ROLE_NAME.ToUpperInvariant() });
                context.SaveChanges();
            }
            else if (context.Roles.Any(role => role.Name == AppConstants.ADMIN_ROLE_NAME && role.NormalizedName != AppConstants.ADMIN_ROLE_NAME))
            {
                var adminRole = context.Roles.FirstOrDefault(role => role.Name == AppConstants.ADMIN_ROLE_NAME);
                adminRole.NormalizedName = AppConstants.ADMIN_ROLE_NAME.ToUpperInvariant();
                context.SaveChanges();
            }
        }
    }
}
