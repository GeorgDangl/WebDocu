using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dangl.WebDocumentation.Mcp
{
    [McpServerToolType]
    public class DocumentationTools
    {
        private readonly IProjectsService _projects;
        private readonly IProjectVersionsService _versions;
        private readonly ApplicationDbContext _context;
        private readonly string _baseUrl;

        public DocumentationTools(IProjectsService projects, IProjectVersionsService versions,
            ApplicationDbContext context, IConfiguration configuration)
        {
            _projects = projects;
            _versions = versions;
            _context = context;
            _baseUrl = configuration["BaseUrl"].TrimEnd('/');
        }

        [McpServerTool(Name = "list_projects", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("List only projects the authenticated user can access, and whether Markdown documentation is available.")]
        public async Task<Page<ProjectInfo>> ListProjects(ClaimsPrincipal user, CancellationToken cancellationToken,
            int offset = 0, int limit = 50)
        {
            ValidatePage(offset, limit, 100);
            var projects = (await _projects.GetAllProjectsForPrincipalAsync(user)).Skip(offset).Take(limit + 1).ToList();
            var names = projects.Select(p => p.Name).ToArray();
            var withMarkdown = await _context.MarkdownDocuments.Where(d => names.Contains(d.ProjectName))
                .Select(d => d.ProjectName).Distinct().ToListAsync(cancellationToken);
            return MakePage(projects.Select(p => new ProjectInfo(p.Name, p.IsPublic,
                withMarkdown.Contains(p.Name))).ToList(), offset, limit);
        }

        [McpServerTool(Name = "list_versions", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("List project versions, newest first, including Markdown availability. latest means the newest hosted version, including prereleases.")]
        public async Task<Page<VersionInfo>> ListVersions(ClaimsPrincipal user, string projectName,
            CancellationToken cancellationToken, int offset = 0, int limit = 50)
        {
            ValidatePage(offset, limit, 100);
            var project = await GetProjectAsync(user, projectName);
            var withMarkdown = await _context.MarkdownDocuments.Where(d => d.ProjectName == project)
                .Select(d => d.Version).Distinct().ToListAsync(cancellationToken);
            var versions = (await _versions.GetProjectVersionsAsync(project)).Skip(offset).Take(limit + 1)
                .Select(v => new VersionInfo(v.version, withMarkdown.Contains(v.version))).ToList();
            return MakePage(versions, offset, limit);
        }

        [McpServerTool(Name = "list_documents", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("List Markdown document paths and titles for an accessible project/version. Use nextOffset for pagination.")]
        public async Task<Page<DocumentInfo>> ListDocuments(ClaimsPrincipal user, string projectName,
            CancellationToken cancellationToken, string version = "latest", int offset = 0, int limit = 50)
        {
            ValidatePage(offset, limit, 100);
            var resolved = await ResolveVersionAsync(user, projectName, version, cancellationToken);
            var documents = await Documents(resolved.Project, resolved.Version).OrderBy(d => d.Path)
                .Skip(offset).Take(limit + 1)
                .Select(d => new { d.Path, d.Title }).ToListAsync(cancellationToken);
            return MakePage(documents.Select(d => new DocumentInfo(resolved.Project, resolved.Version,
                d.Path, d.Title, SourceUrl(resolved.Project, resolved.Version, d.Path))).ToList(), offset, limit);
        }

        [McpServerTool(Name = "search_docs", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("Search Markdown documentation in one accessible project/version using case-insensitive keywords (all words must match). Returns bounded excerpts and source URLs.")]
        public async Task<Page<SearchResult>> SearchDocs(ClaimsPrincipal user, string projectName, string query,
            CancellationToken cancellationToken, string version = "latest", int offset = 0, int limit = 10)
        {
            ValidatePage(offset, limit, 20);
            if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            {
                throw new McpException("Specify a search query of 1 to 200 characters.");
            }
            var terms = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length > 10)
            {
                throw new McpException("Search queries may contain at most 10 keywords.");
            }
            var resolved = await ResolveVersionAsync(user, projectName, version, cancellationToken);
            var matches = Documents(resolved.Project, resolved.Version);
            foreach (var term in terms)
            {
                var keyword = term.ToUpperInvariant();
                matches = matches.Where(d => d.Title.ToUpper().Contains(keyword) || d.Content.ToUpper().Contains(keyword));
            }
            var titleQuery = query.ToUpperInvariant();
            var documents = await matches.OrderByDescending(d => d.Title.ToUpper().Contains(titleQuery))
                .ThenBy(d => d.Path).Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);
            return MakePage(documents.Select(d =>
            {
                var match = d.Content.IndexOf(terms[0], StringComparison.OrdinalIgnoreCase);
                var start = Math.Max(0, match - 120);
                var excerpt = d.Content.Substring(start, Math.Min(600, d.Content.Length - start));
                return new SearchResult(d.ProjectName, d.Version, d.Path, d.Title, excerpt,
                    SourceUrl(d.ProjectName, d.Version, d.Path));
            }).ToList(), offset, limit);
        }

        [McpServerTool(Name = "get_document", ReadOnly = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
        [Description("Read a Markdown document, preserving code examples. offset and nextOffset are character offsets; follow nextOffset until null to read the complete document.")]
        public async Task<DocumentContent> GetDocument(ClaimsPrincipal user, string projectName, string path,
            CancellationToken cancellationToken, string version = "latest", int offset = 0, int maxCharacters = 12000)
        {
            if (offset < 0 || maxCharacters < 100 || maxCharacters > 20000)
            {
                throw new McpException("offset must be non-negative and maxCharacters must be between 100 and 20000.");
            }
            string normalizedPath;
            try
            {
                normalizedPath = MarkdownDocumentsService.NormalizePath(path).ToUpperInvariant();
            }
            catch (InvalidDataException exception)
            {
                throw new McpException(exception.Message);
            }
            var resolved = await ResolveVersionAsync(user, projectName, version, cancellationToken);
            var document = await Documents(resolved.Project, resolved.Version)
                .SingleOrDefaultAsync(d => d.Path.ToUpper() == normalizedPath, cancellationToken);
            if (document == null)
            {
                throw new McpException("Document not found.");
            }
            if (offset > document.Content.Length
                || (offset > 0 && offset < document.Content.Length && char.IsLowSurrogate(document.Content[offset])))
            {
                throw new McpException("offset is outside the document or splits a Unicode character.");
            }
            var end = offset + Math.Min(maxCharacters, document.Content.Length - offset);
            if (end < document.Content.Length && end > offset && char.IsHighSurrogate(document.Content[end - 1]))
            {
                end--;
            }
            return new DocumentContent(document.ProjectName, document.Version, document.Path, document.Title,
                document.Content.Substring(offset, end - offset), offset, document.Content.Length,
                end < document.Content.Length ? end : null, SourceUrl(document.ProjectName, document.Version, document.Path));
        }

        private async Task<string> GetProjectAsync(ClaimsPrincipal user, string projectName)
        {
            var project = (await _projects.GetAllProjectsForPrincipalAsync(user))
                .FirstOrDefault(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
            return project?.Name ?? throw new McpException("Project not found or access denied.");
        }

        private async Task<(string Project, string Version)> ResolveVersionAsync(ClaimsPrincipal user,
            string projectName, string version, CancellationToken cancellationToken)
        {
            var project = await GetProjectAsync(user, projectName);
            if (string.IsNullOrWhiteSpace(version) || version.Length > 40)
            {
                throw new McpException("Specify a version or latest.");
            }
            var versions = await _versions.GetProjectVersionsAsync(project);
            var resolved = string.Equals(version, "latest", StringComparison.OrdinalIgnoreCase)
                ? versions.Select(v => v.version).FirstOrDefault()
                : versions.Where(v => v.version == version).Select(v => v.version).FirstOrDefault();
            if (resolved == null)
            {
                throw new McpException("Project version not found. Use list_versions to select an available version.");
            }
            if (!await Documents(project, resolved).AnyAsync(cancellationToken))
            {
                throw new McpException("This version has no Markdown package. Use list_versions to find versions with Markdown documentation.");
            }
            return (project, resolved);
        }

        private IQueryable<MarkdownDocument> Documents(string project, string version) =>
            _context.MarkdownDocuments.AsNoTracking().Where(d => d.ProjectName == project && d.Version == version);

        private string SourceUrl(string project, string version, string path) =>
            $"{_baseUrl}/MarkdownDocuments/{Uri.EscapeDataString(project)}/{Uri.EscapeDataString(version)}/"
            + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

        private static void ValidatePage(int offset, int limit, int maximum)
        {
            if (offset < 0 || offset > 100000 || limit < 1 || limit > maximum)
            {
                throw new McpException($"offset must be between 0 and 100000 and limit between 1 and {maximum}.");
            }
        }

        private static Page<T> MakePage<T>(List<T> items, int offset, int limit) =>
            new Page<T>(items.Take(limit).ToArray(), items.Count > limit ? offset + limit : null);

        public record Page<T>(IReadOnlyList<T> Items, int? NextOffset);
        public record ProjectInfo(string Name, bool IsPublic, bool HasMarkdown);
        public record VersionInfo(string Version, bool HasMarkdown);
        public record DocumentInfo(string Project, string Version, string Path, string Title, string SourceUrl);
        public record SearchResult(string Project, string Version, string Path, string Title, string Excerpt, string SourceUrl);
        public record DocumentContent(string Project, string Version, string Path, string Title, string Content,
            int Offset, int TotalCharacters, int? NextOffset, string SourceUrl);
    }
}
