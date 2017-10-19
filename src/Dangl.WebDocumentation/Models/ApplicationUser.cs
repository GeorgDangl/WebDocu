using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Dangl.WebDocumentation.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public IList<IdentityUserRole<string>> Roles { get; set; }
    }
}
