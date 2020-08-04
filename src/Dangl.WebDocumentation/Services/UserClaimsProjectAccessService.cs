using Dangl.Identity.ApiClient;
using Dangl.WebDocumentation.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    /// <summary>
    /// This service queries the identity provider (Dangl.Identity) for
    /// information about claims associated with a user and then either
    /// enables or removes project access for the specific user. This should
    /// be run periodically to sync access data.
    /// When project access is assigned manually, nothing is done, but synced
    /// project access via user claims from the identity provider are automatically
    /// added or removed if anything changes upstream.
    /// </summary>
    public class UserClaimsProjectAccessService
    {
        private readonly IUserClaimsClient _userClaimsClient;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserClaimsProjectAccessService> _logger;

        public UserClaimsProjectAccessService(IUserClaimsClient userClaimsClient,
            ApplicationDbContext context,
            ILoggerFactory loggerFactory)
        {
            _userClaimsClient = userClaimsClient;
            _context = context;
            _logger = loggerFactory.CreateLogger<UserClaimsProjectAccessService>();
        }

        [DisableConcurrentExecution(timeoutInSeconds: 3600)]
        public async Task SyncUserClaimsForProjectAccess()
        {
            _logger.LogInformation("Begin to sync project access from Dangl.Identity user claims");
            var userIds = await _context
                .Users
                .Select(u => u.Id)
                .ToListAsync();
            var projects = await _context
                .DocumentationProjects
                .Select(p => new
                {
                    p.Id,
                    p.Name
                })
                .ToListAsync();

            foreach (var userId in userIds)
            {
                try
                {
                    var teamClaimsTask = _userClaimsClient.GetTeamClaimsForUserAsync(userId);
                    var userClaimsTask = _userClaimsClient.GetClaimsForUserAsync(userId);
                    var existingAccessTask = _context.UserProjects
                        .Where(up => up.UserId == userId)
                        .Select(up => new
                        {
                            ProjectAccess = up,
                            ProjectName = up.Project.Name
                        })
                        .ToListAsync();
                    await Task.WhenAll(teamClaimsTask, userClaimsTask, existingAccessTask);

                    var projectsFromClaims = userClaimsTask
                        .Result
                        .Where(c => c.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME)
                        .Select(c => c.Value)
                        .Concat(teamClaimsTask.Result
                            .SelectMany(tc => tc.ClaimAssignments
                                .Where(tca => tca.Type == AppConstants.PROJECT_ACCESS_CLAIM_NAME)
                                .Select(tca => tca.Value)))
                        .Distinct()
                        .ToList();
                    var existingAccess = existingAccessTask.Result;

                    // Add new claims
                    foreach (var projectFromClaims in projectsFromClaims)
                    {
                        var alreadySetAccess = existingAccess.FirstOrDefault(ea => ea.ProjectName == projectFromClaims);
                        if (alreadySetAccess == null)
                        {
                            var projectId = projects.SingleOrDefault(p => p.Name == projectFromClaims)?.Id;
                            if (projectId != null)
                            {
                                _context.UserProjects.Add(new UserProjectAccess
                                {
                                    SetFromIdentityProviderClaim = true,
                                    UserId = userId,
                                    ProjectId = projectId.Value
                                });
                            }
                        }
                        else if (!alreadySetAccess.ProjectAccess.SetFromIdentityProviderClaim)
                        {
                            alreadySetAccess.ProjectAccess.SetFromIdentityProviderClaim = true;
                        }
                    }

                    // Remove no longer present claims
                    foreach (var existingClaim in existingAccess.Where(ea => ea.ProjectAccess.SetFromIdentityProviderClaim))
                    {
                        var claimStillExists = projectsFromClaims.Any(p => p == existingClaim.ProjectName);
                        if (!claimStillExists)
                        {
                            _context.UserProjects.Remove(existingClaim.ProjectAccess);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to update claims for user with id {userId}");
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("End syncing project access from Dangl.Identity user claims");
        }
    }
}
