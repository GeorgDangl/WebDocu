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
                if (!entry.PackageSizeInBytes.HasValue)
                {
                    entry.PackageSizeInBytes = await GetPackageSizeAsync(entry, markdown: false);
                }
                if (entry.MarkdownFileId.HasValue && !entry.MarkdownPackageSizeInBytes.HasValue)
                {
                    entry.MarkdownPackageSizeInBytes = await GetPackageSizeAsync(entry, markdown: true);
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

        private async Task<long?> GetPackageSizeAsync(DocumentationProjectVersion entry, bool markdown)
        {
            try
            {
                var size = markdown
                    ? await _projectFilesService.GetMarkdownPackageSizeInBytesAsync(entry.ProjectName, entry.Version)
                    : await _projectFilesService.GetProjectPackageSizeInBytesAsync(entry.ProjectName, entry.Version);
                if (!size.HasValue)
                {
                    _logger.LogWarning("Package no longer exists for {ProjectName} {Version} (Markdown: {Markdown}).",
                        entry.ProjectName, entry.Version, markdown);
                }
                return size;
            }
            catch (System.Exception exception) when (exception is IOException || exception is System.UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Could not backfill package size for {ProjectName} {Version} (Markdown: {Markdown}).",
                    entry.ProjectName, entry.Version, markdown);
                return null;
            }
        }

        private IQueryable<DocumentationProjectVersion> GetVersionsWithoutSizeAfter(string projectName, string version)
        {
            var query = _context.DocumentationProjectVersions.Where(v => v.PackageSizeInBytes == null
                || (v.MarkdownFileId != null && v.MarkdownPackageSizeInBytes == null));
            if (projectName != null)
            {
                query = query.Where(v => string.Compare(v.ProjectName, projectName) > 0
                    || (v.ProjectName == projectName && string.Compare(v.Version, version) > 0));
            }

            return query;
        }
    }
}
