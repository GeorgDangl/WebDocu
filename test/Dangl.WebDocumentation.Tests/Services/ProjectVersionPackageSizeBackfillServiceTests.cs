using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class ProjectVersionPackageSizeBackfillServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IProjectFilesService> _files = new Mock<IProjectFilesService>(MockBehavior.Strict);
        private readonly Mock<IBackgroundJobClient> _jobs = new Mock<IBackgroundJobClient>();
        private readonly Queue<Job> _queuedJobs = new Queue<Job>();

        public ProjectVersionPackageSizeBackfillServiceTests()
        {
            _context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            _jobs.Setup(j => j.Create(It.IsAny<Job>(), It.IsAny<IState>()))
                .Callback<Job, IState>((job, state) => _queuedJobs.Enqueue(job))
                .Returns("next-job");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(99)]
        [InlineData(100)]
        [InlineData(101)]
        [InlineData(201)]
        public async Task BackfillsAtMostOneHundredVersionsAndOnlySchedulesIfMoreRemain(int count)
        {
            await AddVersionsAsync(count);
            _context.DocumentationProjectVersions.Add(new DocumentationProjectVersion
            {
                ProjectName = "Existing", Version = "1.0", PackageSizeInBytes = 123
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            _files.Setup(f => f.GetProjectPackageSizeInBytesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(4096L);
            var service = CreateService();

            await service.BackfillPackageSizesAsync();

            _files.Verify(f => f.GetProjectPackageSizeInBytesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(Math.Min(100, count)));
            Assert.Equal(count > 100 ? 1 : 0, _queuedJobs.Count);
            for (var batchStart = 100; batchStart < count; batchStart += 100)
            {
                var job = Assert.Single(_queuedJobs);
                _queuedJobs.Clear();
                await service.BackfillPackageSizesAsync((string)job.Args[0], (string)job.Args[1]);
            }

            Assert.Empty(_queuedJobs);
            _context.ChangeTracker.Clear();
            Assert.Equal(count, await _context.DocumentationProjectVersions.CountAsync(v => v.PackageSizeInBytes == 4096, TestContext.Current.CancellationToken));
            Assert.Equal(123L, (await _context.DocumentationProjectVersions.SingleAsync(v => v.ProjectName == "Existing", TestContext.Current.CancellationToken)).PackageSizeInBytes);
            _files.Verify(f => f.GetProjectPackageSizeInBytesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(count));
        }

        [Fact]
        public async Task UnreadablePackagesDoNotBlockLaterBatchesOrCauseEndlessRescheduling()
        {
            await AddVersionsAsync(101);
            _files.Setup(f => f.GetProjectPackageSizeInBytesAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string project, string version) => version == "100"
                    ? Task.FromResult<long?>(4096)
                    : version == "000"
                        ? Task.FromResult<long?>(null)
                        : Task.FromException<long?>(new IOException("Missing package")));
            var service = CreateService();

            await service.BackfillPackageSizesAsync();

            var job = Assert.Single(_queuedJobs);
            _queuedJobs.Clear();
            await service.BackfillPackageSizesAsync((string)job.Args[0], (string)job.Args[1]);

            Assert.Empty(_queuedJobs);
            _context.ChangeTracker.Clear();
            Assert.Equal(100, await _context.DocumentationProjectVersions.CountAsync(v => v.PackageSizeInBytes == null, TestContext.Current.CancellationToken));
            Assert.Equal(4096L, (await _context.DocumentationProjectVersions.SingleAsync(v => v.Version == "100", TestContext.Current.CancellationToken)).PackageSizeInBytes);
            _files.Verify(f => f.GetProjectPackageSizeInBytesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(101));
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        private async Task AddVersionsAsync(int count)
        {
            for (var index = count - 1; index >= 0; index--)
            {
                _context.DocumentationProjectVersions.Add(new DocumentationProjectVersion
                {
                    ProjectName = index < 50 ? "First" : "Second",
                    Version = index.ToString("D3")
                });
            }

            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        private ProjectVersionPackageSizeBackfillService CreateService()
        {
            return new ProjectVersionPackageSizeBackfillService(_context, _files.Object, _jobs.Object,
                NullLogger<ProjectVersionPackageSizeBackfillService>.Instance);
        }
    }
}
