using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Models
{
    public class DocumentationProject
    {

        public DocumentationProject()
        {
            ApiKey = Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpperInvariant();
        }

        public Guid Id { get; set; }

        // TODO max length
        [MaxLength(60, ErrorMessage ="Project name may not exceed 60 characters"), Required]
        public string Name { get; set; }

        [Display(Name="Path to start page"), Required]
        public string PathToIndex { get; set; }

        public bool IsPublic { get; set; }

        public Guid FolderGuid { get; set; }

        public virtual ICollection<UserProjectAccess> UserAccess { get; set; }

        [Required, MaxLength(32)]
        public string ApiKey { get; set; }
    }
}
