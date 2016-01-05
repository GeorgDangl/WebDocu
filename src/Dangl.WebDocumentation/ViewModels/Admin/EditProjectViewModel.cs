using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class EditProjectViewModel
    {
        [Required]
        [Display(Name ="Name")]
        public string ProjectName { get; set; }

        [Display(Name ="Public access")]
        public bool IsPublic { get; set; }

        [Required]
        [Display(Name ="Path to index")]
        public string PathToIndexPage { get; set; }

        [Display(Name ="Users with access")]
        public IEnumerable<string> AvailableUsers { get; set; }
        public IEnumerable<string> UsersWithAccess { get; set; }

        [Display(Name ="API Key")]
        [Required]
        public string ApiKey { get; set; }
    }



}
