using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Dangl.AspNetCore.FileHandling;
using Dangl.Data.Shared;
using Dangl.Identity.Client.Mvc.Services;
using Dangl.WebDocumentation.Mcp;
using Dangl.WebDocumentation.Middleware;
using Dangl.WebDocumentation.Models;
using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.Tests.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Mcp
{
    public class DocumentationMcpTests : IDisposable
    {
        private const string Issuer = "https://identity.example.com";
        private const string Audience = "https://docs.example.com/mcp";
        private readonly Guid _userId = Guid.NewGuid();
        private readonly SymmetricSecurityKey _key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        private readonly TestServer _server;
        private readonly IHost _host;
        private readonly HttpClient _client;
        private readonly string _uploadKey;
        private readonly SqliteConnection _connection;
        private readonly Dictionary<string, byte[]> _blobs = new();
        private readonly Mock<IFileManager> _fileManager = new(MockBehavior.Strict);

        public DocumentationMcpTests()
        {
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
            _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            _connection.Open();
            _host = CreateHost();
            _server = _host.GetTestServer();
            _client = _server.CreateClient();
            using var scope = _server.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
            context.Users.Add(new ApplicationUser { Id = _userId });
            var assigned = new DocumentationProject { Name = "Assigned", PathToIndex = "index.html" };
            var publicProject = new DocumentationProject { Name = "Public", IsPublic = true, PathToIndex = "index.html" };
            _uploadKey = publicProject.ApiKey;
            context.DocumentationProjects.AddRange(publicProject, assigned,
                new DocumentationProject { Name = "Claimed", PathToIndex = "index.html" },
                new DocumentationProject { Name = "Hidden", PathToIndex = "index.html" });
            context.UserProjects.Add(new UserProjectAccess { UserId = _userId, Project = assigned });
            foreach (var project in new[] { "Public", "Assigned", "Claimed", "Hidden" })
            {
                var fileId = Guid.NewGuid();
                context.DocumentationProjectVersions.Add(new DocumentationProjectVersion
                {
                    ProjectName = project, Version = "1.0", MarkdownFileId = fileId
                });
                var setupContent = $"# Setup\n\n{project} authentication configuration\n\n```csharp\nvar docs = true;\n```\n"
                    + new string('x', 240);
                var packageDocuments = project == "Public"
                    ? new[] { ("guide/setup.md", setupContent), ("overview.md", "# Overview\nGetting started") }
                    : new[] { ("guide/setup.md", setupContent) };
                using var package = MarkdownDocumentsServiceTests.Package(packageDocuments);
                _blobs[MarkdownDocumentsService.GetPackagePath(fileId)] = package.ToArray();
                context.MarkdownDocuments.Add(new MarkdownDocument
                {
                    ProjectName = project, Version = "1.0", Path = "guide/setup.md", Title = "Setup"
                });
                if (project == "Public")
                {
                    context.MarkdownDocuments.Add(new MarkdownDocument
                    {
                        ProjectName = "Public", Version = "1.0", Path = "overview.md", Title = "Overview"
                    });
                }
            }
            context.SaveChanges();
        }

        private IHost CreateHost(bool enabled = true)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["BaseUrl"] = "https://docs.example.com",
                ["DanglIdentityBaseUrl"] = Issuer,
                ["Mcp:Enabled"] = enabled.ToString(),
                ["Mcp:Audience"] = Audience,
                ["Mcp:RequiredScope"] = "docs:read",
                ["Mcp:AllowedOrigins:0"] = "https://editor.example.com"
            }).Build();
            return new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConfiguration>(configuration);
                    services.AddSingleton<IFileManager>(_fileManager.Object);
                    services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
                    services.AddMemoryCache();
                    services.AddSingleton(new Mock<IUserInfoService>(MockBehavior.Strict).Object);
                    services.AddSingleton(new Mock<IDocuUserInfoService>().Object);
                    services.AddTransient<IProjectsService, ProjectsService>();
                    services.AddTransient<IProjectVersionsService, ProjectVersionsService>();
                    services.AddTransient<MarkdownDocumentsService>();
                    services.AddControllers().AddApplicationPart(typeof(Startup).Assembly);
                    services.AddAuthentication("Browser").AddCookie("Browser");
                    services.AddDocumentationMcp(configuration);
                    services.Configure<JwtBearerOptions>(DocumentationMcpExtensions.AuthenticationScheme, options =>
                    {
                        options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                        options.Configuration.SigningKeys.Add(_key);
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    var settings = app.ApplicationServices.GetRequiredService<McpSettings>();
                    app.UseDocumentationMcp(settings);
                    app.UseAuthorization();
                    if (settings.Enabled)
                    {
                        app.UseRateLimiter();
                    }
                    app.UseChallengeOnUnauthorizedResponse();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapDocumentationMcp(configuration);
                        endpoints.MapControllers();
                    });
                })).Start();
        }

        [Fact]
        public async Task DiscoveryIsAnonymousAndBrowserAuthenticationDefaultsAreUnchanged()
        {
            using var response = await _client.GetAsync(DocumentationMcpExtensions.MetadataEndpoint, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(Audience, metadata.RootElement.GetProperty("resource").GetString());
            Assert.Equal(Issuer, metadata.RootElement.GetProperty("authorization_servers")[0].GetString());
            Assert.Equal("docs:read", metadata.RootElement.GetProperty("scopes_supported")[0].GetString());
            var schemes = _server.Services.GetRequiredService<IAuthenticationSchemeProvider>();
            Assert.Equal("Browser", (await schemes.GetDefaultAuthenticateSchemeAsync()).Name);
            Assert.Equal("Browser", (await schemes.GetDefaultChallengeSchemeAsync()).Name);
        }

        [Fact]
        public async Task MissingTokenReceivesOAuthChallengeWithoutLoginRedirect()
        {
            using var response = await SendRpcAsync("tools/list", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            var challenge = Assert.Single(response.Headers.WwwAuthenticate).ToString();
            Assert.Contains("resource_metadata=\"https://docs.example.com/.well-known/oauth-protected-resource/mcp\"", challenge);
            Assert.Contains("scope=\"docs:read\"", challenge);
            Assert.True(response.Headers.CacheControl.NoStore);
        }

        [Theory]
        [InlineData("audience")]
        [InlineData("issuer")]
        [InlineData("expired")]
        [InlineData("signature")]
        [InlineData("subject")]
        [InlineData("id-token")]
        public async Task InvalidTokensAreRejected(string problem)
        {
            var token = Token(
                audience: problem == "audience" ? "another-api" : Audience,
                issuer: problem == "issuer" ? "https://other.example.com" : Issuer,
                expires: problem == "expired" ? DateTime.UtcNow.AddHours(-1) : null,
                key: problem == "signature" ? new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) : null,
                subject: problem == "subject" ? "not-a-user-guid" : null,
                tokenType: problem == "id-token" ? "JWT" : "at+jwt");
            using var response = await SendRpcAsync("tools/list", token);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Contains("invalid_token", response.Headers.WwwAuthenticate.ToString());
        }

        [Fact]
        public async Task MissingReadScopeIsForbiddenWithScopeChallenge()
        {
            using var response = await SendRpcAsync("tools/list", Token(scope: "unrelated"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("insufficient_scope", response.Headers.WwwAuthenticate.ToString());
            Assert.Null(response.Headers.Location);
        }

        [Fact]
        public async Task ToolsAreReadOnlyAndDoNotExposeIdentityAsAnAgentArgument()
        {
            using var response = await SendRpcAsync("tools/list", Token());
            response.EnsureSuccessStatusCode();
            using var json = await ReadRpcAsync(response);
            var tools = json.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();

            Assert.Equal(new[] { "get_document", "list_documents", "list_projects", "list_versions", "search_docs" },
                tools.Select(t => t.GetProperty("name").GetString()).OrderBy(n => n));
            foreach (var tool in tools)
            {
                Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
                Assert.False(tool.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("user", out _));
            }
        }

        [Fact]
        public async Task ProjectListingRespectsAssignmentsAndClaimsWithoutExposingUploadKeys()
        {
            using var result = await CallToolAsync("list_projects", new { });
            var projects = result.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("items");

            Assert.Equal(new[] { "Assigned", "Claimed", "Public" }, projects.EnumerateArray().Select(p => p.GetProperty("name").GetString()));
            Assert.DoesNotContain(_uploadKey, result.RootElement.ToString());
            Assert.DoesNotContain("apiKey", result.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("list_versions")]
        [InlineData("list_documents")]
        [InlineData("search_docs")]
        [InlineData("get_document")]
        public async Task PrivateProjectMetadataAndContentCannotBeAccessedByGuessingNames(string tool)
        {
            using var result = await CallToolAsync(tool, new { projectName = "Hidden", query = "configuration", path = "guide/setup.md" });

            Assert.True(result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.DoesNotContain("Hidden authentication", result.RootElement.ToString());
            Assert.Contains("Project not found or access denied", result.RootElement.ToString());
        }

        [Fact]
        public async Task AuthenticationAndProjectAccessAreReevaluatedForEachRequest()
        {
            using var first = await CallToolAsync("get_document", new { projectName = "Assigned", path = "guide/setup.md" });
            Assert.Contains("Assigned authentication", first.RootElement.ToString());
            using var denied = await CallToolAsync("get_document", new { projectName = "Assigned", path = "guide/setup.md" },
                Token(subject: Guid.NewGuid().ToString(), includeProjectClaim: false));
            Assert.True(denied.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());

            using (var scope = _server.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.UserProjects.Remove(await context.UserProjects.SingleAsync(TestContext.Current.CancellationToken));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            using var revoked = await CallToolAsync("get_document", new { projectName = "Assigned", path = "guide/setup.md" });
            Assert.True(revoked.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        }

        [Fact]
        public async Task SearchReturnsScopedExcerptsAndResolvableSourceUrls()
        {
            using var result = await CallToolAsync("search_docs", new { projectName = "Public", query = "AUTHENTICATION configuration" });
            var items = result.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("items");
            var item = Assert.Single(items.EnumerateArray());
            Assert.Equal("1.0", item.GetProperty("version").GetString());
            Assert.Contains("Public authentication", item.GetProperty("excerpt").GetString());
            Assert.Equal("https://docs.example.com/MarkdownDocuments/Public/1.0/guide/setup.md", item.GetProperty("sourceUrl").GetString());
            using var source = await _client.GetAsync("/MarkdownDocuments/Public/1.0/guide/setup.md", TestContext.Current.CancellationToken);
            source.EnsureSuccessStatusCode();
            Assert.Contains("```csharp", await source.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.True(source.Headers.CacheControl.NoStore);
        }

        [Fact]
        public async Task DocumentListsAndContentArePaginatedWithoutSilentlyDroppingText()
        {
            using var list = await CallToolAsync("list_documents", new { projectName = "Public", limit = 1 });
            var page = list.RootElement.GetProperty("result").GetProperty("structuredContent");
            Assert.Single(page.GetProperty("items").EnumerateArray());
            Assert.Equal(1, page.GetProperty("nextOffset").GetInt32());

            using var first = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md", maxCharacters = 100 });
            var content = first.RootElement.GetProperty("result").GetProperty("structuredContent");
            Assert.Equal(100, content.GetProperty("content").GetString().Length);
            Assert.Equal(100, content.GetProperty("nextOffset").GetInt32());
            using var remaining = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md", offset = 100 });
            var tail = remaining.RootElement.GetProperty("result").GetProperty("structuredContent");
            Assert.Equal(JsonValueKind.Null, tail.GetProperty("nextOffset").ValueKind);
            Assert.Equal(content.GetProperty("totalCharacters").GetInt32(),
                content.GetProperty("content").GetString().Length + tail.GetProperty("content").GetString().Length);
        }

        [Fact]
        public async Task LatestDoesNotSilentlyFallBackToOlderMarkdown()
        {
            using (var scope = _server.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.DocumentationProjectVersions.Add(new DocumentationProjectVersion { ProjectName = "Public", Version = "2.0" });
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            using var latest = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md" });
            Assert.Contains("no Markdown package", latest.RootElement.ToString());
            using var pinned = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md", version = "1.0" });
            Assert.Contains("Public authentication", pinned.RootElement.ToString());
            using var missing = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md", version = "0.9" });
            Assert.Contains("Project version not found", missing.RootElement.ToString());
        }

        [Fact]
        public async Task InvalidOriginsAreRejectedAndAllowedBrowserPreflightDoesNotRequireLogin()
        {
            using var invalid = new HttpRequestMessage(HttpMethod.Post, "/mcp");
            invalid.Headers.Add("Origin", "https://untrusted.example.com");
            using var rejected = await _client.SendAsync(invalid, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/mcp");
            preflight.Headers.Add("Origin", "https://editor.example.com");
            preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,mcp-protocol-version,mcp-method");
            using var accepted = await _client.SendAsync(preflight, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Assert.Equal("https://editor.example.com", Assert.Single(accepted.Headers.GetValues("Access-Control-Allow-Origin")));
        }

        [Fact]
        public async Task MarkdownUploadUsesTheExistingProjectUploadKeyButReadingDoesNotAcceptIt()
        {
            using var package = MarkdownDocumentsServiceTests.Package(("replacement.md", "# Replacement\nNew documentation"));
            using var form = new MultipartFormDataContent
            {
                { new StringContent(_uploadKey), "apiKey" },
                { new StringContent("1.0"), "version" },
                { new ByteArrayContent(package.ToArray()), "markdownPackage", "markdown.zip" }
            };
            using var upload = await _client.PostAsync("/API/MarkdownDocuments/Upload", form, TestContext.Current.CancellationToken);
            upload.EnsureSuccessStatusCode();
            using var read = await CallToolAsync("get_document", new { projectName = "Public", path = "replacement.md" });
            Assert.Contains("New documentation", read.RootElement.ToString());
            using var unauthorized = await SendRpcAsync("tools/list", _uploadKey);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        }

        [Fact]
        public async Task InvalidUploadKeyCannotReplaceMarkdownAndPrivateSourceUrlsRequireLogin()
        {
            using var package = MarkdownDocumentsServiceTests.Package(("replacement.md", "Must not be stored"));
            using var form = new MultipartFormDataContent
            {
                { new StringContent("invalid-upload-key"), "apiKey" },
                { new StringContent("1.0"), "version" },
                { new ByteArrayContent(package.ToArray()), "markdownPackage", "markdown.zip" }
            };
            using var upload = await _client.PostAsync("/API/MarkdownDocuments/Upload", form, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
            using var original = await CallToolAsync("get_document", new { projectName = "Public", path = "guide/setup.md" });
            Assert.Contains("Public authentication", original.RootElement.ToString());
            using var privateSource = await _client.GetAsync("/MarkdownDocuments/Hidden/1.0/guide/setup.md", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Redirect, privateSource.StatusCode);
            Assert.DoesNotContain("Hidden authentication", await privateSource.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DisabledMcpExposesNeitherProtocolNorDiscoveryEndpoints()
        {
            using var host = CreateHost(enabled: false);
            using var client = host.GetTestClient();
            using var response = await client.PostAsync("/mcp", new StringContent("{}"), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var metadata = await client.GetAsync(DocumentationMcpExtensions.MetadataEndpoint, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
        }

        [Fact]
        public async Task OlderStreamableHttpClientsCanStillInitialize()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = JsonContent.Create(new
                {
                    jsonrpc = "2.0", id = 1, method = "initialize",
                    @params = new { protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "test", version = "1.0" } }
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
            request.Headers.Accept.ParseAdd("application/json, text/event-stream");
            using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            using var json = await ReadRpcAsync(response);
            Assert.Equal("2025-11-25", json.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString());
            using var toolRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", id = 2, method = "tools/list", @params = new { } })
            };
            toolRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
            toolRequest.Headers.Accept.ParseAdd("application/json, text/event-stream");
            toolRequest.Headers.Add("MCP-Protocol-Version", "2025-11-25");
            using var toolResponse = await _client.SendAsync(toolRequest, TestContext.Current.CancellationToken);
            toolResponse.EnsureSuccessStatusCode();
            using var tools = await ReadRpcAsync(toolResponse);
            Assert.Equal(5, tools.RootElement.GetProperty("result").GetProperty("tools").GetArrayLength());
        }

        private string Token(string scope = "docs:read", string audience = Audience, string issuer = Issuer,
            DateTime? expires = null, SecurityKey key = null, string subject = null, string tokenType = "at+jwt",
            bool includeProjectClaim = true)
        {
            var claims = new List<Claim>
            {
                new Claim("sub", subject ?? _userId.ToString()),
                new Claim("scope", scope)
            };
            if (includeProjectClaim)
            {
                claims.Add(new Claim(AppConstants.PROJECT_ACCESS_CLAIM_NAME, "Claimed"));
            }
            var header = new JwtHeader(new SigningCredentials(key ?? _key, SecurityAlgorithms.HmacSha256)) { ["typ"] = tokenType };
            var payload = new JwtPayload(issuer, audience, claims, DateTime.UtcNow.AddHours(-2), expires ?? DateTime.UtcNow.AddMinutes(10));
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
        }

        private async Task<JsonDocument> CallToolAsync(string name, object arguments, string token = null)
        {
            using var response = await SendRpcAsync("tools/call", token ?? Token(), name, arguments);
            response.EnsureSuccessStatusCode();
            return await ReadRpcAsync(response);
        }

        private static async Task<JsonDocument> ReadRpcAsync(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentType?.MediaType == "application/json")
            {
                return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }
            Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
            using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
            await foreach (var item in SseParser.Create(stream).EnumerateAsync(TestContext.Current.CancellationToken))
            {
                var json = JsonDocument.Parse(item.Data);
                if (json.RootElement.TryGetProperty("result", out _) || json.RootElement.TryGetProperty("error", out _))
                {
                    return json;
                }
                json.Dispose();
            }
            throw new InvalidDataException("MCP response stream contained no JSON-RPC response.");
        }

        private Task<HttpResponseMessage> SendRpcAsync(string method, string token, string tool = null, object arguments = null)
        {
            var parameters = new Dictionary<string, object>
            {
                ["_meta"] = new Dictionary<string, object>
                {
                    ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
                    ["io.modelcontextprotocol/clientInfo"] = new { name = "test", version = "1.0" },
                    ["io.modelcontextprotocol/clientCapabilities"] = new { }
                }
            };
            if (tool != null)
            {
                parameters["name"] = tool;
                parameters["arguments"] = arguments;
            }
            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters })
            };
            if (token != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            request.Headers.Accept.ParseAdd("application/json, text/event-stream");
            request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
            request.Headers.Add("Mcp-Method", method);
            if (tool != null)
            {
                request.Headers.Add("Mcp-Name", tool);
            }
            return _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            _client.Dispose();
            _host.Dispose();
            _connection.Dispose();
        }
    }
}
