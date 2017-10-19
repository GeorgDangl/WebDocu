using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectFilesService
    {
        Task<string> GetEntryFilePathForProject(string projectName);
        Task<ProjectFileDto> GetFileForProject(string projectName, string version, string filePath);
        Task<bool> UploadProjectPackage(string projectName, string version, Stream zipArchiveStream);
    }
}
