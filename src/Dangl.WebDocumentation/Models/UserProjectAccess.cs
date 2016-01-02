using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Models
{
    public class UserProjectAccess
    {
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        public string ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual DocumentationProject Project { get; set; }
    }
}
