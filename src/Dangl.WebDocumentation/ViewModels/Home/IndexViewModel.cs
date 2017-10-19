using System.Collections.Generic;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Home
{
    public class IndexViewModel
    {
        public IEnumerable<DocumentationProject> Projects { get; set; }
    }
}
