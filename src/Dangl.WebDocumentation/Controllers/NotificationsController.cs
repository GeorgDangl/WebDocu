using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IProjectsService _projectsService;

        public NotificationsController(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IProjectsService projectsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _projectsService = projectsService;
        }

        public async Task<IActionResult> Index(string successMessage = null)
        {
            ViewData["Section"] = "Notifications";

            ViewBag.SuccessMessage = successMessage;

            var userId = _userManager.GetUserId(User);
            var accessibleProjects = await _projectsService.GetAllProjectsForUser(userId);

            var model = new IndexViewModel();
            model.Projects = accessibleProjects;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SetNotifications(string projectName, NotificationLevel notificationLevel)
        {
            ViewData["Section"] = "Notifications";

            var userId = _userManager.GetUserId(User);

            if (!await _projectsService.UserHasAccessToProject(projectName, userId))
            {
                // No need to show a detailed error message, this state should
                // not be reached if the user navigates via the WebUI
                return Unauthorized();
            }

            var user = await _userManager.GetUserAsync(User);
            var userClaims = await _userManager.GetClaimsAsync(user);

            var claimsToRemove = userClaims.Where(c => (c.Type == AppConstants.PROJECT_NOTIFICATIONS_CLAIM_BETA
                    || c.Type == AppConstants.PROJECT_NOTIFICATIONS_CLAIM_STABLE)
                    && c.Value == projectName);

            foreach (var claimToRemove in claimsToRemove)
            {
                await _userManager.RemoveClaimAsync(user, claimToRemove);
            }

            switch (notificationLevel)
            {
                case NotificationLevel.Stable:
                    await _userManager.AddClaimAsync(user, new Claim(AppConstants.PROJECT_NOTIFICATIONS_CLAIM_STABLE, projectName));
                    break;

                case NotificationLevel.All:
                    await _userManager.AddClaimAsync(user, new Claim(AppConstants.PROJECT_NOTIFICATIONS_CLAIM_STABLE, projectName));
                    await _userManager.AddClaimAsync(user, new Claim(AppConstants.PROJECT_NOTIFICATIONS_CLAIM_BETA, projectName));
                    break;
            }

            await _signInManager.RefreshSignInAsync(user);

            var successMessage = $"Notification settings updated!";

            return RedirectToAction(nameof(Index), new { successMessage });
        }
    }
}
