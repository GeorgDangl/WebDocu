using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dangl.AspNetCore.FileHandling;
using Dangl.Data.Shared;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class MarkdownDocumentsServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly MarkdownDocumentsService _service;
        private readonly SqliteConnection _connection;
        private readonly Dictionary<string, byte[]> _blobs = new();
        private readonly Mock<IFileManager> _fileManager = new(MockBehavior.Strict);

        public MarkdownDocumentsServiceTests()
        {
            _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            _connection.Open();
            _context = CreateContext();
            _context.Database.EnsureCreated();
            _context.DocumentationProjects.Add(new DocumentationProject { Name = "Docs", PathToIndex = "index.html" });
            _context.DocumentationProjectVersions.Add(new DocumentationProjectVersion { ProjectName = "Docs", Version = "1.0" });
            _context.SaveChanges();
            _fileManager.Setup(m => m.SaveFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>(), It.IsAny<Stream>()))
                .Callback<string, string, Stream>((_, path, stream) =>
                {
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    _blobs[path] = copy.ToArray();
                })
                .ReturnsAsync(RepositoryResult.Success());
            _fileManager.Setup(m => m.GetFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()))
                .ReturnsAsync((string _, string path) => _blobs.TryGetValue(path, out var bytes)
                    ? RepositoryResult<Stream>.Success(new MemoryStream(bytes))
                    : RepositoryResult<Stream>.Fail("Blob not found."));
            _fileManager.Setup(m => m.DeleteFileAsync(AppConstants.PROJECTS_CONTAINER, It.IsAny<string>()))
                .Callback<string, string>((_, path) => _blobs.Remove(path))
                .ReturnsAsync(RepositoryResult.Success());
            _service = new MarkdownDocumentsService(_context, _fileManager.Object,
                NullLogger<MarkdownDocumentsService>.Instance);
        }

        [Fact]
        public async Task UploadPreservesMarkdownAndCodeAndIgnoresOtherFiles()
        {
            const string content = "# Setup\n\n```csharp\nvar project = \"Docs\";\n```\n";
            using var package = Package(("howto/", ""), ("guide\\setup.MD", content), ("images/logo.png", "ignored"));

            var result = await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(1, result.Value);
            var document = await _context.MarkdownDocuments.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("guide/setup.MD", document.Path);
            Assert.Equal("Setup", document.Title);
            Assert.Equal(content, await _service.GetDocumentContentAsync("Docs", "1.0", document.Path,
                TestContext.Current.CancellationToken));
            var searchResults = await _service.SearchDocumentsAsync("Docs", "1.0", new[] { document },
                new[] { "project" }, TestContext.Current.CancellationToken);
            Assert.Contains("project", Assert.Single(searchResults).Excerpt, StringComparison.OrdinalIgnoreCase);
            Assert.Single(_blobs);
            Assert.Equal(package.Length,
                (await _context.DocumentationProjectVersions.SingleAsync(TestContext.Current.CancellationToken)).MarkdownPackageSizeInBytes);
        }

        [Fact]
        public async Task ReplacementUpdatesAddsAndRemovesDocumentsOnlyWithinTheVersion()
        {
            using var first = Package(("same.md", "# Before"), ("removed.md", "obsolete"));
            Assert.True((await _service.UploadPackageAsync("Docs", "1.0", first, TestContext.Current.CancellationToken)).IsSuccess);
            _context.DocumentationProjectVersions.Add(new DocumentationProjectVersion { ProjectName = "Docs", Version = "2.0" });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            using var second = Package(("keep.md", "Other version"));
            Assert.True((await _service.UploadPackageAsync("Docs", "2.0", second, TestContext.Current.CancellationToken)).IsSuccess);
            using var replacement = Package(("same.md", "# After"), ("added.md", "new"));

            Assert.True((await _service.UploadPackageAsync("Docs", "1.0", replacement, TestContext.Current.CancellationToken)).IsSuccess);

            _context.ChangeTracker.Clear();
            var documents = await _context.MarkdownDocuments.OrderBy(d => d.Path).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "added.md", "keep.md", "same.md" }, documents.Select(d => d.Path));
            Assert.Equal("# After", await _service.GetDocumentContentAsync("Docs", "1.0", "same.md",
                TestContext.Current.CancellationToken));
            Assert.Equal("Other version", await _service.GetDocumentContentAsync("Docs", "2.0", "keep.md",
                TestContext.Current.CancellationToken));
            Assert.Equal(2, _blobs.Count);
            Assert.Equal(replacement.Length, (await _context.DocumentationProjectVersions
                .SingleAsync(v => v.Version == "1.0", TestContext.Current.CancellationToken)).MarkdownPackageSizeInBytes);
            Assert.Equal(second.Length, (await _context.DocumentationProjectVersions
                .SingleAsync(v => v.Version == "2.0", TestContext.Current.CancellationToken)).MarkdownPackageSizeInBytes);
        }

        [Theory]
        [InlineData("../secret.md")]
        [InlineData("/absolute.md")]
        [InlineData("C:\\absolute.md")]
        [InlineData("guide/../secret.md")]
        [InlineData("./readme.md")]
        [InlineData("guide//readme.md")]
        public async Task UnsafePathsAreRejectedWithoutReplacingExistingContent(string path)
        {
            using var original = Package(("readme.md", "# Original"));
            await _service.UploadPackageAsync("Docs", "1.0", original, TestContext.Current.CancellationToken);
            using var invalid = Package(("valid.md", "Must not be saved"), (path, "Unsafe"));

            var result = await _service.UploadPackageAsync("Docs", "1.0", invalid, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal("# Original", await _service.GetDocumentContentAsync("Docs", "1.0", "readme.md",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DuplicateCaseInsensitivePathsAreRejected()
        {
            using var package = Package(("Readme.md", "one"), ("README.md", "two"));

            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken)).IsSuccess);
            Assert.Empty(_context.MarkdownDocuments);
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("latest")]
        public async Task UploadRequiresAnExistingExplicitVersion(string version)
        {
            using var package = Package(("readme.md", "# Docs"));

            Assert.False((await _service.UploadPackageAsync("Docs", version, package, TestContext.Current.CancellationToken)).IsSuccess);
            Assert.Empty(_context.MarkdownDocuments);
        }

        [Fact]
        public async Task UploadStoresTheProjectAndVersionCasingFromTheExistingVersion()
        {
            _context.DocumentationProjectVersions.Add(new DocumentationProjectVersion
            {
                ProjectName = "Docs", Version = "1.0.0-Beta"
            });
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
            using var package = Package(("readme.md", "# Docs"));

            var result = await _service.UploadPackageAsync("docs", "1.0.0-beta", package,
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var document = await _context.MarkdownDocuments.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Docs", document.ProjectName);
            Assert.Equal("1.0.0-Beta", document.Version);
        }

        [Fact]
        public async Task InvalidZipAndPackagesWithoutMarkdownAreRejected()
        {
            using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("not a ZIP"));
            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", invalid, TestContext.Current.CancellationToken)).IsSuccess);
            using var noMarkdown = Package(("index.html", "<html></html>"));
            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", noMarkdown, TestContext.Current.CancellationToken)).IsSuccess);
        }

        [Fact]
        public async Task NonUtf8ContentIsRejected()
        {
            using var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                using var entry = archive.CreateEntry("readme.md").Open();
                entry.Write(new byte[] { 0xC3, 0x28 });
            }
            package.Position = 0;

            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken)).IsSuccess);
        }

        [Fact]
        public async Task HighlyCompressedOversizedDocumentIsRejected()
        {
            using var package = Package(("huge.md", new string('x', MarkdownDocumentsService.MaxDocumentBytes + 1)));

            var result = await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Empty(_context.MarkdownDocuments);
        }

        [Fact]
        public async Task DocumentAtTheByteLimitIsAcceptedAndOversizedCompressedInputsAreRejected()
        {
            using var allowed = Package(("limit.md", new string('x', MarkdownDocumentsService.MaxDocumentBytes)));
            Assert.True((await _service.UploadPackageAsync("Docs", "1.0", allowed, TestContext.Current.CancellationToken)).IsSuccess);
            using var oversized = new MemoryStream(new byte[MarkdownDocumentsService.MaxPackageBytes + 1]);

            var result = await _service.UploadPackageAsync("Docs", "1.0", oversized, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal(MarkdownDocumentsService.MaxDocumentBytes,
                (await _service.GetDocumentContentAsync("Docs", "1.0", "limit.md",
                    TestContext.Current.CancellationToken)).Length);
        }

        [Fact]
        public async Task TotalUncompressedContentAndDocumentCountAreBounded()
        {
            var content = new string('x', MarkdownDocumentsService.MaxDocumentBytes);
            using var large = Package(Enumerable.Range(0, 21)
                .Select(i => ($"{i}.md", content)).ToArray());
            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", large, TestContext.Current.CancellationToken)).IsSuccess);

            using var many = Package(Enumerable.Range(0, MarkdownDocumentsService.MaxDocuments + 1)
                .Select(i => ($"{i}.md", "content")).ToArray());
            Assert.False((await _service.UploadPackageAsync("Docs", "1.0", many, TestContext.Current.CancellationToken)).IsSuccess);
            Assert.Empty(_context.MarkdownDocuments);
        }

        [Fact]
        public async Task PackageAtTheDocumentAndZipEntryLimitsIsAccepted()
        {
            using var package = Package(Enumerable.Range(0, MarkdownDocumentsService.MaxDocuments)
                .Select(i => ($"{i}.md", "# Document"))
                .Concat(Enumerable.Range(0, MarkdownDocumentsService.MaxZipEntries - MarkdownDocumentsService.MaxDocuments)
                    .Select(i => ($"directory-{i}/", ""))).ToArray());

            var result = await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal(MarkdownDocumentsService.MaxDocuments, result.Value);
            Assert.Equal(MarkdownDocumentsService.MaxDocuments,
                await _context.MarkdownDocuments.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal("# Document", await _service.GetDocumentContentAsync("Docs", "1.0", "9999.md",
                TestContext.Current.CancellationToken));
            Assert.Single(_blobs);
        }

        [Fact]
        public async Task TooManyZipEntriesAreRejectedEvenWhenNonMarkdown()
        {
            using var package = Package(Enumerable.Range(0, MarkdownDocumentsService.MaxZipEntries)
                .Select(i => ($"{i}.txt", ""))
                .Append(("readme.md", "# Docs")).ToArray());

            var result = await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Equal($"A Markdown package may contain at most {MarkdownDocumentsService.MaxZipEntries} ZIP entries.",
                result.ErrorMessage);
            Assert.Empty(_context.MarkdownDocuments);
            Assert.Empty(_blobs);
        }

        [Fact]
        public async Task ConcurrentVersionChangesRollBackTheEntireReplacement()
        {
            using (var other = CreateContext())
            {
                var version = await other.DocumentationProjectVersions.SingleAsync(TestContext.Current.CancellationToken);
                version.MarkdownRevision = Guid.NewGuid();
                other.MarkdownDocuments.Add(new MarkdownDocument
                {
                    ProjectName = "Docs", Version = "1.0", Path = "accepted.md", Title = "Accepted"
                });
                await other.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            using var package = Package(("stale.md", "Must be rolled back"));

            var result = await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Contains("changed during upload", result.ErrorMessage);
            using var verification = CreateContext();
            Assert.Equal("accepted.md", (await verification.MarkdownDocuments.SingleAsync(TestContext.Current.CancellationToken)).Path);
            Assert.Empty(_blobs);
        }

        [Fact]
        public async Task DeletingAVersionAlsoDeletesItsMarkdownDocuments()
        {
            using var package = Package(("readme.md", "# Docs"));
            Assert.True((await _service.UploadPackageAsync("Docs", "1.0", package, TestContext.Current.CancellationToken)).IsSuccess);
            _context.ChangeTracker.Clear();
            _context.DocumentationProjectVersions.Remove(await _context.DocumentationProjectVersions
                .SingleAsync(TestContext.Current.CancellationToken));

            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Empty(_context.MarkdownDocuments);
        }

        private ApplicationDbContext CreateContext() => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

        public static MemoryStream Package(params (string Path, string Content)[] files)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in files)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(file.Path).Open(), new UTF8Encoding(false));
                    writer.Write(file.Content);
                }
            }
            stream.Position = 0;
            return stream;
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
        }
    }
}
