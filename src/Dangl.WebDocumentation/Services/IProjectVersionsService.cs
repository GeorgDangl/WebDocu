using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectVersionsService
    {
        Task<List<string>> GetProjectVersions(string projectName);
    }
}
