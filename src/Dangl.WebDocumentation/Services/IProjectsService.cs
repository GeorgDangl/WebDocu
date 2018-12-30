﻿using Dangl.WebDocumentation.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectsService
    {
        Task<bool> UserHasAccessToProject(string projectName, string userId = null);

        Task<string> GetProjectNameForApiKey(string apiKey);

        Task<Guid> GetIdForProjectByNameAsync(string projectName);

        Task<List<DocumentationProject>> GetAllProjectsForUser(string userId);
    }
}
