using System.Text.Json;
using Dangl.WebDocumentation.Models;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Models
{
    public class McpClientConfigurationTests
    {
        [Fact]
        public void RenderConfigurationReplacesPlaceholdersAsEscapedJsonValues()
        {
            var configuration = new McpClientConfiguration
            {
                ClientId = "client\"id",
                ConfigurationTemplate = "{\"url\": {{mcpUrl}}, \"clientId\": {{clientId}}}"
            };

            using var rendered = JsonDocument.Parse(configuration.RenderConfiguration("https://docs.example.com/mcp"));

            Assert.Equal("https://docs.example.com/mcp", rendered.RootElement.GetProperty("url").GetString());
            Assert.Equal("client\"id", rendered.RootElement.GetProperty("clientId").GetString());
        }
    }
}
