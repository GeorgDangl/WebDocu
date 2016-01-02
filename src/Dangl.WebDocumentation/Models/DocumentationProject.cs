using System;
using System.Collections.Generic;
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

        [Key]
        public string Name { get; set; }

        public string PathToIndex { get; set; }

        public bool IsPublic { get; set; }

        public Guid FolderGuid { get; set; }

        public virtual ICollection<UserProjectAccess> UserAccess { get; set; }

        public string ApiKey { get; set; }
    }
}
