using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Dtos;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectFilesService
    {
        Task<ProjectFileDto> GetFileForProject(string projectName, string filePath);
        Task<bool> UploadProjectPackage(string projectName, Stream zipArchiveStream);
    }
}
