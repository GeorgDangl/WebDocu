using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dangl.WebDocumentation.Models;

namespace Dangl.WebDocumentation.ViewModels.Admin
{
    public class McpClientConfigurationsViewModel
    {
        public IEnumerable<McpClientConfiguration> Configurations { get; set; }

        public McpClientConfigurationInputViewModel NewConfiguration { get; set; } = new();
    }

    public class McpClientConfigurationInputViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Client / implementation name")]
        public string Name { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "OAuth client ID")]
        public string ClientId { get; set; }

        [StringLength(1000)]
        [Display(Name = "Connection instructions")]
        public string Instructions { get; set; }

        [Required, StringLength(8000)]
        [Display(Name = "Client configuration JSON template")]
        public string ConfigurationTemplate { get; set; } = @"{
  ""servers"": {
    ""project-documentation"": {
      ""type"": ""http"",
      ""url"": {{mcpUrl}},
      ""oauth"": {
        ""clientId"": {{clientId}}
      }
    }
  }
}";
    }
}
