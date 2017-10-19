using System;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class DeleteProjectViewModel
    {
        [Display(Name = "Project Id")]
        public Guid ProjectId { get; set; }

        [Display(Name = "Name")]
        public string ProjectName { get; set; }

        [Display(Name = "Confirm Delete")]
        public bool ConfirmDelete { get; set; }
    }
}
