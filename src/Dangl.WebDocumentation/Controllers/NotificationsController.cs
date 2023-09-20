using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly IProjectsService _projectsService;
        private readonly IDocuUserInfoService _docuUserInfoService;
        private readonly IUserProjectNotificationsService _userProjectNotificationsService;

        public NotificationsController(IProjectsService projectsService,
            IDocuUserInfoService docuUserInfoService,
            IUserProjectNotificationsService userProjectNotificationsService)
        {
            _projectsService = projectsService;
            _docuUserInfoService = docuUserInfoService;
            _userProjectNotificationsService = userProjectNotificationsService;
        }

        public async Task<IActionResult> Index(string successMessage = null, string errorMessage = null)
        {
            ViewData["Section"] = "Notifications";

            ViewBag.SuccessMessage = successMessage;
            ViewBag.ErrorMessage = errorMessage;

            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();
            var accessibleProjects = await _projectsService.GetAllProjectsForUserAsync(userId);
            var notificationSettings = await _userProjectNotificationsService
                .GetProjectNotificationsForUserAsync(userId.Value);

            var model = new IndexViewModel();
            model.Projects = accessibleProjects;
            model.NotificationLevelsByProject = new Dictionary<System.Guid, NotificationLevel>();
            foreach (var notificationSetting in notificationSettings)
            {
                var notificationLevel = notificationSetting.receiveBetaNotifications
                    ? NotificationLevel.All
                    : NotificationLevel.Stable;
                model.NotificationLevelsByProject.Add(notificationSetting.projectId, notificationLevel);
            }
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SetNotifications(string projectName, NotificationLevel notificationLevel)
        {
            ViewData["Section"] = "Notifications";

            var userId = await _docuUserInfoService.GetCurrentUserIdOrNullAsync();

            if (!await _projectsService.UserHasAccessToProject(projectName, userId))
            {
                // No need to show a detailed error message, this state should
                // not be reached if the user navigates via the WebUI
                return Unauthorized();
            }

            if (userId == null)
            {
                return BadRequest();
            }

            var projectId = await _projectsService.GetIdForProjectByNameAsync(projectName);

            var shouldRemove = notificationLevel == NotificationLevel.None;
            var result = false;
            if (shouldRemove)
            {
                result = await _userProjectNotificationsService.RemoveNotificationsForUserAndProjectAsync(projectId, userId.Value);
            }
            else
            {
                var shouldIncludeBeta = notificationLevel == NotificationLevel.All;
                result = await _userProjectNotificationsService.EnableNotificationsForUserAndProjectAsync(projectId, userId.Value, shouldIncludeBeta);
            }

            if (result)
            {
                var successMessage = "Notification settings updated!";
                return RedirectToAction(nameof(Index), new { successMessage });
            }
            else
            {
                var errorMessage = "Could not update the notifications.";
                return RedirectToAction(nameof(Index), new { errorMessage });
            }
        }
    }
}
