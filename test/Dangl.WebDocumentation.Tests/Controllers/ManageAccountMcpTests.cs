using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Mcp;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Home;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class ManageAccountMcpTests : IDisposable
    {
        private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task AccountPageShowsPublishedClientConfigurationWithCurrentEndpoint()
        {
            _context.McpClientConfigurations.Add(new McpClientConfiguration
            {
                Name = "VS Code",
                ClientId = "vscode-client",
                ConfigurationTemplate = "{\"url\": {{mcpUrl}}, \"clientId\": {{clientId}}}"
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string> { ["BaseUrl"] = "https://docs.example.com/" }).Build();
            var controller = new HomeController(null, null, null, _context, configuration,
                new McpSettings { Enabled = true, RequiredScope = "docs:read" });

            var result = await controller.ManageAccount();

            var model = Assert.IsType<ManageAccountViewModel>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(result).Model);
            Assert.Equal("https://docs.example.com/mcp", model.McpUrl);
            Assert.Equal("docs:read", model.RequiredScope);
            var client = Assert.Single(model.ClientConfigurations);
            Assert.Equal("VS Code", client.Configuration.Name);
            using var rendered = JsonDocument.Parse(client.RenderedJson);
            Assert.Equal("vscode-client", rendered.RootElement.GetProperty("clientId").GetString());
        }

        public void Dispose() => _context.Dispose();
    }
}
