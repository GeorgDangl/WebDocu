using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class CreateProjectViewModel
    {
        public string ProjectName { get; set; }

        public bool IsPublic { get; set; }

        public string PathToIndexPage { get; set; }

        public IEnumerable<string> AvailableUsers { get; set; }
        public IEnumerable<string> UsersWithAccess { get; set; }
    }
}
