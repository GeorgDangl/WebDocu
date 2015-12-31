using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Home
{
    public class IndexViewModel
    {
        public IEnumerable<DocumentationProject> Projects { get; set; }
    }
}
