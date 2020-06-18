using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dangl.WebDocumentation.Models
{
    public class UserProjectAccess
    {
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        public Guid ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual DocumentationProject Project { get; set; }
    }
}
