using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Models
{
    public class DocumentationProject
    {
        public Guid Id { get; set; }
        public string Name { get; set; }

        public string PathToIndex { get; set; }

        public bool IsPublic { get; set; }
        
    }
}
