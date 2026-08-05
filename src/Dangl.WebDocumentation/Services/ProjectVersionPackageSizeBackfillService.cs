using Dangl.WebDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class ProjectVersionPackageSizeBackfillService
    {
        private readonly ApplicationDbContext _context;
        private readonly IProjectFilesService _projectFilesService;

        public ProjectVersionPackageSizeBackfillService(ApplicationDbContext context,
            IProjectFilesService projectFilesService)
        {
            _context = context;
            _projectFilesService = projectFilesService;
        }

        public async Task BackfillPackageSizesAsync()
        {
            var versionsWithoutSize = await _context.DocumentationProjectVersions
                .Where(v => v.PackageSizeInBytes == null)
                .Select(v => new { v.ProjectName, v.Version })
                .ToListAsync();

            foreach (var entry in versionsWithoutSize)
            {
                var packageStream = await _projectFilesService.GetProjectPackageAsync(entry.ProjectName, entry.Version);
                if (packageStream == null)
                {
                    continue;
                }

                long sizeInBytes;
                using (packageStream)
                {
                    if (packageStream.CanSeek)
                    {
                        sizeInBytes = packageStream.Length;
                    }
                    else
                    {
                        var buffer = new byte[4096];
                        long totalRead = 0;
                        int bytesRead;
                        while ((bytesRead = await packageStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            totalRead += bytesRead;
                        }
                        sizeInBytes = totalRead;
                    }
                }

                var version = await _context.DocumentationProjectVersions
                    .FirstOrDefaultAsync(v => v.ProjectName == entry.ProjectName && v.Version == entry.Version);
                if (version != null)
                {
                    version.PackageSizeInBytes = sizeInBytes;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
