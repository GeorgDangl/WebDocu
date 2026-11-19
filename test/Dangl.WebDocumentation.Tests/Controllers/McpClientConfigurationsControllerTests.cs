using System;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.ViewModels.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class McpClientConfigurationsControllerTests : IDisposable
    {
        private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        [Fact]
        public async Task AdminCanAddClientConfiguration()
        {
            var controller = CreateController();
            var model = new McpClientConfigurationInputViewModel
            {
                Name = "VS Code",
                ClientId = "vscode-client",
                ConfigurationTemplate = "{\"url\": {{mcpUrl}}, \"clientId\": {{clientId}}}"
            };

            var result = await controller.AddMcpClientConfiguration(model);

            Assert.IsType<RedirectToActionResult>(result);
            var saved = Assert.Single(_context.McpClientConfigurations);
            Assert.Equal("VS Code", saved.Name);
            Assert.Equal("vscode-client", saved.ClientId);
        }

        [Fact]
        public async Task InvalidClientConfigurationIsNotSaved()
        {
            var controller = CreateController();
            var model = new McpClientConfigurationInputViewModel
            {
                Name = "VS Code",
                ClientId = "vscode-client",
                ConfigurationTemplate = "{\"url\": {{mcpUrl}}}"
            };

            var result = await controller.AddMcpClientConfiguration(model);

            Assert.IsType<ViewResult>(result);
            Assert.Empty(_context.McpClientConfigurations);
            Assert.Contains(controller.ModelState, entry => entry.Key == nameof(model.ConfigurationTemplate));
        }

        [Fact]
        public async Task AdminCanDeleteClientConfiguration()
        {
            var configuration = new McpClientConfiguration
            {
                Name = "VS Code",
                ClientId = "vscode-client",
                ConfigurationTemplate = "{\"url\": {{mcpUrl}}, \"clientId\": {{clientId}}}"
            };
            _context.McpClientConfigurations.Add(configuration);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            var result = await CreateController().DeleteMcpClientConfiguration(configuration.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.Empty(_context.McpClientConfigurations);
        }

        private AdminController CreateController() =>
            new(_context, null, null, null, null, null, null);

        public void Dispose() => _context.Dispose();
    }
}
