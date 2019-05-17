using Dangl.WebDocumentation.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectUploadNotificationsService : IProjectUploadNotificationsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IProjectsService _projectsService;
        private readonly AppSettings _appSettings;
        private readonly IProjectChangelogService _projectChangelogService;

        public ProjectUploadNotificationsService(ApplicationDbContext context,
            IProjectsService projectsService,
            IProjectChangelogService projectChangelogService,
            IOptions<AppSettings> options)
        {
            _context = context;
            _projectsService = projectsService;
            _appSettings = options.Value;
            _projectChangelogService = projectChangelogService;
        }

        public async Task ScheduleProjectUploadNotifications(string projectName, string version)
        {
            var requiredClaimType = SemanticVersionsOrderer.IsStableVersion(version)
                ? AppConstants.PROJECT_NOTIFICATIONS_CLAIM_STABLE
                : AppConstants.PROJECT_NOTIFICATIONS_CLAIM_BETA;

            var usersWithEnabledNotifications = await (from userClaims in _context.UserClaims
                      join users in _context.Users on userClaims.UserId equals users.Id
                      where userClaims.ClaimType == requiredClaimType
                        && userClaims.ClaimValue == projectName
                      select new
                      {
                          users.Id,
                          users.Email
                      })
                      .ToListAsync();

            foreach (var user in usersWithEnabledNotifications)
            {
                if (!await _projectsService.UserHasAccessToProject(projectName, user.Id))
                {
                    continue;
                }

                var emailData = await GetEmailContent(projectName, version, _appSettings.BaseUrl);
                BackgroundJob.Enqueue<IEmailSender>(emailSender => emailSender.SendMessage(user.Email, emailData.subject, emailData.body));
            }
        }

        private async Task<(string subject, string body)> GetEmailContent(string projectName, string version, string danglDocuBaseUrl)
        {
            var isPrereleaseVersion = !SemanticVersionsOrderer.IsStableVersion(version);

            var subject = isPrereleaseVersion
                ? $"New Beta Release Available for {projectName}"
                : $"New Release Available for {projectName}";

            var htmlChangelog = await _projectChangelogService.GetChangelogInHtmlFormat(projectName, version);
            var emailBody = GetEmailBody(isPrereleaseVersion, projectName, version, danglDocuBaseUrl, htmlChangelog);

            return (subject, emailBody);
        }

        private static string GetEmailBody(bool isPrereleaseVersion,
            string projectName,
            string version,
            string danglDocuBaseUrl,
            string htmlChangelog)
        {
            return @"<div style=""display: none; font-size: 1px; color: #fefefe; line-height: 1px; font-family: 'Roboto', Helvetica, Arial, sans-serif; max-height: 0px; max-width: 0px; opacity: 0; overflow: hidden; mso-hide: all;"">Dangl<strong>IT</strong> Release Notification</div>
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" bgcolor=""#00acc1"">
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""600"" class=""content-table"" align=""center"" style=""min-width: 320px;"">
<tbody>
<tr>
<td align=""center"" valign=""top"" style=""padding: 16px 16px 16px 16px;""><span style=""color: #fff; font-family: 'Roboto', Helvetica, Arial, sans-serif; text-decoration: none; font-size: 2.5em;"">Dangl<strong>IT</strong> - Release Notification</span></td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" style=""padding: 32px 0 32px 0;"" bgcolor=""#ffffff"">
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"">
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""600"" class=""content-table"" align=""center"">
<tbody>
<tr>
<td align=""center"" valign=""top"" style=""padding: 0 16px 0 16px;""><!-- content -->
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" style=""padding: 0 0 8px 0; color: #424242; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 28px; font-weight: 400; line-height: 32px;"">A new "
+ (isPrereleaseVersion ? "beta " : "")
+ "version for <strong>" + projectName + "</strong> is available: " + version + @"</td>
</tr>
<tr>
<td align=""center"" style=""padding: 0 0 16px 0; color: #424242; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 16px; font-weight: normal; line-height: 28px;"">"
+ @"<p><a href=""" + danglDocuBaseUrl + @""">You can go directly to Dangl<strong>Docu</strong></a> to view the new release. Thank you for using Dangl<strong>IT</strong> products!</p>"
+ @"</td>
</tr>
<tr>
<td align=""center"" style=""padding: 0 0 16px 0; color: #424242; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 16px; font-weight: normal; line-height: 28px;"">
<p>Do you want to stop receiving notifications from Dangl<strong>Docu</strong>? Go to " + danglDocuBaseUrl + @" and disable your notifications.</p>
<p>You can reply to this email if you have any questions.</p>
</td>
</tr>" + (string.IsNullOrWhiteSpace(htmlChangelog)
? string.Empty
: (@"<tr>
<td align=""center"" style=""padding: 0 0 16px 0; color: #424242; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 28px; font-weight: normal; line-height: 32px;"">
<p><strong>Changelog:</strong></p>
</td>
</tr>
<tr>
<td  style=""padding: 0 0 16px 0; color: #424242; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 16px; font-weight: normal; line-height: 28px;"">
" + htmlChangelog + @"
</td>
</tr>"))

+ @"</tbody>
</table>
</td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" style=""padding: 32px 0 0 0;"">
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" bgcolor=""#546E7A"">
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""600"" class=""content-table"" align=""center"">
<tbody>
<tr>
<td align=""center"" valign=""top"" style=""padding: 16px 16px 16px 16px;""><!-- content -->
<table border=""0"" cellspacing=""0"" cellpadding=""0"" width=""100%"">
<tbody>
<tr>
<td align=""center"" style=""color: #90a4ae; font-family: 'Roboto', Helvetica, Arial, sans-serif; font-size: 14px; font-weight: normal; line-height: 21px;""><a href=""https://www.dangl-it.com"" style=""color: #cfd8dc; text-decoration: none;"">Dangl<strong>IT</strong> GmbH - www.dangl-it.com</a></td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>
</td>
</tr>
</tbody>
</table>";
        }


    }
}
