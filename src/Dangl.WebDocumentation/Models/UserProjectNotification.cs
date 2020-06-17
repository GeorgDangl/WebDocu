using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dangl.WebDocumentation.Models
{
    public class UserProjectNotification
    {
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        public Guid ProjectId { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual DocumentationProject Project { get; set; }

        [Required]
        public bool ReceiveBetaNotifications { get; set; }
    }
}
