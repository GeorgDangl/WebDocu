using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectVersionsService
    {
        /// <summary>
        /// This should order project versions descending by their Semantiv Versioning name.
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        Task<List<string>> GetProjectVersionsAsync(string projectName);
    }
}
