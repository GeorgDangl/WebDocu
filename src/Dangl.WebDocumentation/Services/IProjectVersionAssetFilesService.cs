﻿using Dangl.Data.Shared;
using Dangl.WebDocumentation.ViewModels.ProjectAssets;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IProjectVersionAssetFilesService
    {
        Task<bool> UploadAssetFileForProjectVersionAsync(string projectName, string version, IFormFile assetFile);

        Task<RepositoryResult<SasUploadResponse>> GetSasBlobUploadLinkAsync(string projectName, string version, SasUploadModel sasUploadModel);

        Task<List<(string fileName, string prettyfiedFileSize, Guid fileId)>> GetAssetsForProjectVersionAsync(string projectName, string version);

        Task<RepositoryResult<(string sasDownloadUrl, Stream stream)>> GetAssetDownloadAsync(string fileName, Guid fileId);

        Task<bool> DeleteProjectAssetFileAsync(Guid fileId);
    }
}
