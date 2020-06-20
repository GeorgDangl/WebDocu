using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dangl.WebDocumentation.IdentityMigration.Standalone
{
    public class UserProjectAccess
    {
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        public Guid ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual DocumentationProject Project { get; set; }
    }
}
