using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class IndexViewModel
    {
        public IEnumerable<ProjectStorageViewModel> Projects { get; set; }
    }
}
