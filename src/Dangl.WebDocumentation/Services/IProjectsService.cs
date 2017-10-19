using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectsService
    {
        Task<bool> UserHasAccessToProject(string projectName, string userId = null);
        Task<string> GetProjectNameForApiKey(string apiKey);
    }
}
