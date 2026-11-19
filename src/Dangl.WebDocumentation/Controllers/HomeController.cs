using Dangl.Identity.Client.Mvc.Services;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Home;
using Dangl.WebDocumentation.Mcp;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IDocuUserInfoService _docuUserInfoService;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly McpSettings _mcpSettings;

        public HomeController(UserManager<ApplicationUser> userManager,
            IProjectsService projectsService,
            IDocuUserInfoService docuUserInfoService,
            ApplicationDbContext context,
            IConfiguration configuration,
            McpSettings mcpSettings)
        {
            UserManager = userManager;
            _projectsService = projectsService;
            _docuUserInfoService = docuUserInfoService;
            _context = context;
            _configuration = configuration;
            _mcpSettings = mcpSettings;
        }

        private UserManager<ApplicationUser> UserManager { get; }

        public async Task<IActionResult> Index(string projectsFilter = null)
        {
            ViewData["Section"] = "Home";

            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();

            var accessibleProjects = await _projectsService.GetAllProjectsForUserAsync(userId, projectsFilter);

            var model = new IndexViewModel();
            model.Projects = accessibleProjects;
            model.ProjectsFilter = projectsFilter;
            return View(model);
        }

        public IActionResult Privacy()
        {
            ViewData["Section"] = "Privacy";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> ManageAccount()
        {
            var mcpUrl = _configuration["BaseUrl"].TrimEnd('/') + DocumentationMcpExtensions.Endpoint;
            var configurations = await _context.McpClientConfigurations
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(new ManageAccountViewModel
            {
                McpEnabled = _mcpSettings.Enabled,
                McpUrl = mcpUrl,
                RequiredScope = _mcpSettings.RequiredScope,
                ClientConfigurations = configurations.Select(c => (c, c.RenderConfiguration(mcpUrl)))
            });
        }
    }
}
