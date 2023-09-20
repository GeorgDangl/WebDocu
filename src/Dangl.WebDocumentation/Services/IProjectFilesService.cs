using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectFilesService
    {
        Task<string> GetEntryFilePathForProjectAsync(string projectName);
        Task<ProjectFileDto> GetFileForProjectAsync(string projectName, string version, string filePath);
        Task<bool> PackageAlreadyExistsAsync(string projectName, string version);
        Task<bool> UploadProjectPackageAsync(string projectName, string version, string markdownChangelog, Stream zipArchiveStream);
        Task<bool> DeleteProjectVersionPackageAsync(Guid projectId, string version);
    }
}
