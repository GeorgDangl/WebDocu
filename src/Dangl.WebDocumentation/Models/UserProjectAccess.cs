using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Models
{
    public class UserProjectAccess
    {
        [Key]
        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }

        public string ProjectId { get; set; }

        public virtual DocumentationProject Project { get; set; }
    }
}
