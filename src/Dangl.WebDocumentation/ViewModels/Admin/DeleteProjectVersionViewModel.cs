using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class DeleteProjectVersionViewModel
    {
        [Display(Name = "Project Id")]
        public Guid ProjectId { get; set; }

        [Display(Name = "Name")]
        public string ProjectName { get; set; }

        [Display(Name = "Version")]
        public string Version { get; set; }

        [Display(Name = "Confirm Delete")]
        public bool ConfirmDelete { get; set; }
    }
}
