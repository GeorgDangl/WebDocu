using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectUploadNotificationsService
    {
        Task ScheduleProjectUploadNotifications(string projectName, string version);
    }
}
