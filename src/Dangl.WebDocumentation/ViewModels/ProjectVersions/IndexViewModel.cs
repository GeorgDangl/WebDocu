using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.ProjectVersions
{
    public class IndexViewModel
    {
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string PathToIndex { get; set; }
        public IList<string> Versions { get; set; }
    }
}
