using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            // Add Admin role
            if (Context.Roles.All(Role => Role.Name != "Admin"))
            {
                Context.Roles.Add(new Microsoft.AspNet.Identity.EntityFramework.IdentityRole {Name = "Admin"});
                Context.SaveChanges();
            }
        }
    }
}
