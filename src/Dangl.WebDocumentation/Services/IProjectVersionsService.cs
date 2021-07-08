using System;
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
        Task<List<(string version, bool hasAssets, bool hasChangelog, DateTimeOffset? dateUtc)>> GetProjectVersionsAsync(string projectName);

        /// <summary>
        /// This is supposed to return all versions that are not stable and that do have an earlier and a later
        /// stable version. This is useful to bulk-delete unnecessary prerelease versions.
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        Task<List<string>> GetAllPreviewVersionsExceptFirstAndLastAsync(string projectName);

        Task<bool> ProjectVersionExistsAsync(string projectName, string version);
    }
}
