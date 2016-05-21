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
            if (Context.Roles.All(Role => Role.Name != "Admin"))
            {
                Context.Roles.Add(new IdentityRole {Name = "Admin"});
                Context.SaveChanges();
            }
        }
    }
}