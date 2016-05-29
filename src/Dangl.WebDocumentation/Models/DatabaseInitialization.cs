using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Dangl.WebDocumentation.Models
{
    public static class DatabaseInitialization
    {
        public static void Initialize(ApplicationDbContext Context)
        {
            SetUpRoles(Context);
        }

        private static void SetUpRoles(ApplicationDbContext Context)
        {
            // Add Admin role if not present
            if (Context.Roles.All(role => role.Name != "Admin"))
            {
                Context.Roles.Add(new IdentityRole {Name = "Admin", NormalizedName = "ADMIN"});
                Context.SaveChanges();
            }
            else if (Context.Roles.Any(role => role.Name == "Admin" && role.NormalizedName != "Admin"))
            {
                var adminRole = Context.Roles.FirstOrDefault(role => role.Name == "Admin");
                adminRole.NormalizedName = "ADMIN";
                Context.SaveChanges();
            }
        }
    }
}