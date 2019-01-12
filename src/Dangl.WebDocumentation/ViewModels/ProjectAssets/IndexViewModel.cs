using System;
using System.Collections.Generic;

namespace Dangl.WebDocumentation.ViewModels.ProjectAssets
{
    public class IndexViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string ProjectVersion { get; set; }
        public IList<ProjectAssetFileViewModel> Files { get; set; }
    }
}
