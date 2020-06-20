using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.IdentityMigration.Standalone
{
    public class DocumentationProject
    {
        public DocumentationProject()
        {
            // Ensures key is created on initialization.
            ApiKey = Guid.NewGuid().ToString().Replace("-", string.Empty).ToUpperInvariant();
        }

        public Guid Id { get; set; }

        /// <summary>
        /// This property is referenced as a principal key from <see cref="DocumentationProjectVersion"/>,
        /// thus making it a unique index
        /// </summary>
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
