using System;

namespace Dangl.WebDocumentation.ViewModels.ProjectVersions
{
    public class ProjectVersionViewModel
    {
        public string Version { get; set; }
        public bool HasAssetFiles { get; set; }
        public bool HasChangelog { get; set; }
        public DateTimeOffset? DateUtc { get; set; }
    }
}
