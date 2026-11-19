using System.Collections.Generic;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Home
{
    public class ManageAccountViewModel
    {
        public bool McpEnabled { get; set; }
        public string McpUrl { get; set; }
        public string RequiredScope { get; set; }
        public IEnumerable<(McpClientConfiguration Configuration, string RenderedJson)> ClientConfigurations { get; set; }
    }
}
