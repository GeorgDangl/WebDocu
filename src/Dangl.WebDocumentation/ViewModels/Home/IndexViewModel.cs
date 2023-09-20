using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Home
{
    public class IndexViewModel
    {
        [Display(Name = "Filter Projects")]
        public string ProjectsFilter { get; set; }

        public IEnumerable<DocumentationProject> Projects { get; set; }
    }
}
