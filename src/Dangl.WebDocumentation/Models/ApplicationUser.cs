using System;
using System.Collections.Generic;
using Dangl.Identity.Shared;
using Microsoft.AspNetCore.Identity;

namespace Dangl.WebDocumentation.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser<Guid>, IDanglIdentityUser
    {
        public IList<IdentityUserRole<Guid>> Roles { get; set; }
        public Guid IdenticonId { get; set; }
    }
}
