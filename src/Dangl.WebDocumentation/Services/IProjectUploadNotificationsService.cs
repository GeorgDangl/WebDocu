using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectUploadNotificationsService
    {
        Task ScheduleProjectUploadNotificationsAsync(string projectName, string version);
    }
}
