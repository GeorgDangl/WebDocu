using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Models
{
    public class UserProjectAccess
    {
        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }

        public Guid ProjectId { get; set; }

        public virtual DocumentationProject Project { get; set; }
    }
}
