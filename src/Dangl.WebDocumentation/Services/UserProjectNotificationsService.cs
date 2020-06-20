using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class UserProjectNotificationsService : IUserProjectNotificationsService
    {
        private readonly ApplicationDbContext _context;

        public UserProjectNotificationsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> EnableNotificationsForUserAndProjectAsync(Guid projectId, Guid userId, bool receiveBetaNotifications = false)
        {
            var existingNotification = await _context
                .UserProjectNotifications
                .Where(n => n.ProjectId == projectId && n.UserId == userId)
                .FirstOrDefaultAsync();
            if (existingNotification != null)
            {
                existingNotification.ReceiveBetaNotifications = receiveBetaNotifications;
                await _context.SaveChangesAsync();
                return true;
            }

            _context.UserProjectNotifications
                .Add(new UserProjectNotification
                {
                    ProjectId = projectId,
                    UserId = userId,
                    ReceiveBetaNotifications = receiveBetaNotifications
                });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveNotificationsForUserAndProjectAsync(Guid projectId, Guid userId)
        {
            var existingNotification = await _context
                .UserProjectNotifications
                .FirstOrDefaultAsync(n => n.ProjectId == projectId && n.UserId == userId);
            if (existingNotification == null)
            {
                return false;
            }

            _context.UserProjectNotifications.Remove(existingNotification);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<(Guid projectId, bool receiveBetaNotifications)>> GetProjectNotificationsForUserAsync(Guid userId)
        {
            return (await _context
                .UserProjectNotifications
                .Where(n => n.UserId == userId)
                .Select(n => new {n.ProjectId, n.ReceiveBetaNotifications})
                .ToListAsync())
                .Select(n => (n.ProjectId, n.ReceiveBetaNotifications))
                .ToList();
        }
    }
}
