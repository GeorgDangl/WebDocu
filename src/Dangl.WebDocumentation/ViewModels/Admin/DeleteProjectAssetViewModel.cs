using System;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class DeleteProjectAssetViewModel
    {
        [Display(Name = "Project Id")]
        public Guid ProjectId { get; set; }

        [Display(Name = "Name")]
        public string ProjectName { get; set; }

        [Display(Name = "Version")]
        public string Version { get; set; }

        [Display(Name = "File Name")]
        public string FileName { get; set; }

        [Display(Name = "File Id")]
        public Guid FileId { get; set; }

        [Display(Name = "Confirm Delete")]
        public bool ConfirmDelete { get; set; }
    }
}
