using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class DeleteProjectViewModel
    {
        public Guid ProjectId { get; set; }

        public string ProjectName { get; set; }

        public bool ConfirmDelete { get; set; }
    }
}
