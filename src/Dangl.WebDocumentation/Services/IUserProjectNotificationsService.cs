using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IUserProjectNotificationsService
    {
        Task<bool> EnableNotificationsForUserAndProjectAsync(Guid projectId, Guid userId, bool receiveBetaNotifications = false);
        
        Task<bool> RemoveNotificationsForUserAndProjectAsync(Guid projectId, Guid userId);
        
        Task<List<(Guid projectId, bool receiveBetaNotifications)>> GetProjectNotificationsForUserAsync(Guid userId);
    }
}
