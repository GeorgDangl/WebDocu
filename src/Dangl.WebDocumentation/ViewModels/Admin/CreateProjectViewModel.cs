using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class CreateProjectViewModel
    {
        [Display(Name ="Name")]
        [Required]
        public string ProjectName { get; set; }

        [Display(Name ="Public access")]
        public bool IsPublic { get; set; }

        [Display(Name ="Path to index")]
        [Required]
        public string PathToIndexPage { get; set; }

        [Display(Name ="Users with access")]
        public IEnumerable<string> AvailableUsers { get; set; }
        public IEnumerable<string> UsersWithAccess { get; set; }
    }
}
