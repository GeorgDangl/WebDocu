using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class DeleteBetaVersionsViewModel
    {
        public string ProjectName { get; set; }
        public List<string> VersionsToDelete { get; set; }
    }
}
