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
        public string ProjectName { get; set; }

        public bool IsPublic { get; set; }

        public string PathToIndexPage { get; set; }

        public IEnumerable<string> AvailableUsers { get; set; }
        public IEnumerable<string> UsersWithAccess { get; set; }

        public string ApiKey { get; set; }
    }



}
