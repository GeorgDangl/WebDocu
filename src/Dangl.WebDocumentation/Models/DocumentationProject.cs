using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.Models
{
    public class DocumentationProject
    {
        public DocumentationProject()
        {
            // Ensures key is created on initialization.
            ApiKey = Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpperInvariant();
        }

        public Guid Id { get; set; }

        [MaxLength(60, ErrorMessage = "Project name may not exceed 60 characters"), Required]
        public string Name { get; set; }

        [Display(Name = "Path to start page"), Required]
        public string PathToIndex { get; set; }

        public bool IsPublic { get; set; }

        public Guid FolderGuid { get; set; }

        public virtual ICollection<UserProjectAccess> UserAccess { get; set; }

        [Required, MaxLength(32)]
        public string ApiKey { get; set; }
    }
}
