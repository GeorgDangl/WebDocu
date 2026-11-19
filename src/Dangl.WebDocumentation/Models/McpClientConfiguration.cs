using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Dangl.WebDocumentation.Models
{
    public class McpClientConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required, MaxLength(200)]
        public string ClientId { get; set; }

        [MaxLength(1000)]
        public string Instructions { get; set; }

        [Required, MaxLength(8000)]
        public string ConfigurationTemplate { get; set; }

        public string RenderConfiguration(string mcpUrl) =>
            ConfigurationTemplate
                .Replace("{{mcpUrl}}", JsonSerializer.Serialize(mcpUrl), StringComparison.Ordinal)
                .Replace("{{clientId}}", JsonSerializer.Serialize(ClientId), StringComparison.Ordinal);
    }
}
