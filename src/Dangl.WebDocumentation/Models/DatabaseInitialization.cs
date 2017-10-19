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
            if (context.Roles.All(role => role.Name != "Admin"))
            {
                context.Roles.Add(new IdentityRole {Name = "Admin", NormalizedName = "ADMIN"});
                context.SaveChanges();
            }
            else if (context.Roles.Any(role => role.Name == "Admin" && role.NormalizedName != "Admin"))
            {
                var adminRole = context.Roles.FirstOrDefault(role => role.Name == "Admin");
                adminRole.NormalizedName = "ADMIN";
                context.SaveChanges();
            }
        }
    }
}
