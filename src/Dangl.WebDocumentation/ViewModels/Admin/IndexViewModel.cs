using System.Collections.Generic;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class IndexViewModel
    {
        public IEnumerable<DocumentationProject> Projects { get; set; }
    }
}
