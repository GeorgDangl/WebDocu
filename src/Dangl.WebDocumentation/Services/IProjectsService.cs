using Dangl.WebDocumentation.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectsService
    {
        Task<bool> ProjectExistsAsyncAsync(string projectName);
        Task<bool> UserHasAccessToProjectAsync(string projectName, Guid? userId = null);

        Task<string> GetProjectNameForApiKeyAsync(string apiKey);

        Task<Guid> GetIdForProjectByNameAsync(string projectName);

        Task<List<DocumentationProject>> GetAllProjectsForUserAsync(Guid? userId, string? filter = null);
    }
}
