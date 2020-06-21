using Dangl.Identity.ApiClient;
using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class UserDeletionService
    {
        private readonly IDeletedUsersClient _deletedUsersClient;
        private readonly ApplicationDbContext _context;

        public UserDeletionService(IDeletedUsersClient deletedUsersClient,
            ApplicationDbContext context)
        {
            _deletedUsersClient = deletedUsersClient;
            _context = context;
        }

        public async Task RemoveLocallyCachedDeletedUsersAsync()
        {
            var deletedUserIds = await _deletedUsersClient.GetDeletedUserIdsAsync();

            if (!deletedUserIds.Any())
            {
                return;
            }

            var existingUsers = await _context
                .Users
                .Where(u => deletedUserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();
            if (!existingUsers.Any())
            {
                return;
            }

            foreach (var deletedUserId in existingUsers)
            {
                // Remove notifications
                var notifications = await _context
                    .UserProjectNotifications
                    .Where(n => n.UserId == deletedUserId)
                    .ToListAsync();
                if (notifications.Any())
                {
                    _context.UserProjectNotifications.RemoveRange(notifications);
                }

                // Remove project access
                var projectAccess = await _context
                    .UserProjects
                    .Where(up => up.UserId == deletedUserId)
                    .ToListAsync();
                if (projectAccess.Any())
                {
                    _context.UserProjects.RemoveRange(projectAccess);
                }

                // Remove roles
                var roles = await _context
                    .UserRoles
                    .Where(ur => ur.UserId == deletedUserId)
                    .ToListAsync();
                if (roles.Any())
                {
                    _context.UserRoles.RemoveRange(roles);
                }

                // Remove users
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == deletedUserId);
                if (user != null)
                {
                    _context.Users.Remove(user);
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
