using Dangl.WebDocumentation.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectVersionPackageSizeBackfillService
    {
        private const int BatchSize = 100;

        private readonly ApplicationDbContext _context;
        private readonly IProjectFilesService _projectFilesService;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly ILogger<ProjectVersionPackageSizeBackfillService> _logger;

        public ProjectVersionPackageSizeBackfillService(ApplicationDbContext context,
            IProjectFilesService projectFilesService,
            IBackgroundJobClient backgroundJobClient,
            ILogger<ProjectVersionPackageSizeBackfillService> logger)
        {
            _context = context;
            _projectFilesService = projectFilesService;
            _backgroundJobClient = backgroundJobClient;
            _logger = logger;
        }

        public async Task BackfillPackageSizesAsync(string afterProjectName = null, string afterVersion = null)
        {
            var versionsWithoutSize = await GetVersionsWithoutSizeAfter(afterProjectName, afterVersion)
                .OrderBy(v => v.ProjectName)
                .ThenBy(v => v.Version)
                .Take(BatchSize)
                .ToListAsync();

            foreach (var entry in versionsWithoutSize)
            {
                try
                {
                    var sizeInBytes = await _projectFilesService.GetProjectPackageSizeInBytesAsync(entry.ProjectName, entry.Version);
                    if (sizeInBytes == null)
                    {
                        _logger.LogWarning("Documentation package no longer exists for {ProjectName} {Version}.", entry.ProjectName, entry.Version);
                        continue;
                    }

                    entry.PackageSizeInBytes = sizeInBytes;
                }
                catch (IOException exception)
                {
                    _logger.LogWarning(exception, "Could not backfill package size for {ProjectName} {Version}.", entry.ProjectName, entry.Version);
                }
            }

            await _context.SaveChangesAsync();

            if (versionsWithoutSize.Count == BatchSize)
            {
                var lastVersion = versionsWithoutSize.Last();
                if (await GetVersionsWithoutSizeAfter(lastVersion.ProjectName, lastVersion.Version).AnyAsync())
                {
                    _backgroundJobClient.Enqueue<ProjectVersionPackageSizeBackfillService>(
                        s => s.BackfillPackageSizesAsync(lastVersion.ProjectName, lastVersion.Version));
                }
            }
        }

        private IQueryable<DocumentationProjectVersion> GetVersionsWithoutSizeAfter(string projectName, string version)
        {
            var query = _context.DocumentationProjectVersions.Where(v => v.PackageSizeInBytes == null);
            if (projectName != null)
            {
                query = query.Where(v => string.Compare(v.ProjectName, projectName) > 0
                    || (v.ProjectName == projectName && string.Compare(v.Version, version) > 0));
            }

            return query;
        }
    }
}
