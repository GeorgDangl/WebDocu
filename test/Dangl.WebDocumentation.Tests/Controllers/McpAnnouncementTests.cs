using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dangl.WebDocumentation.Controllers;
using Dangl.WebDocumentation.Mcp;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Controllers
{
    public class McpAnnouncementTests : IDisposable
    {
        private readonly IHost _host;
        private readonly HttpClient _client;

        public McpAnnouncementTests()
        {
            _host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(new McpSettings { Enabled = true });
                    services.AddControllersWithViews().AddApplicationPart(typeof(Startup).Assembly).AddControllersAsServices();
                    services.AddTransient(_ => new HomeController(null, null, null, null, null, null));
                    services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.Use((context, next) =>
                    {
                        if (context.Request.Headers["X-Test-Authenticated"] == "true")
                        {
                            context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                            {
                                new Claim(ClaimTypes.NameIdentifier, "announcement-test-user")
                            }, "Test"));
                        }
                        return next();
                    });
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapDefaultControllerRoute();
                        endpoints.MapGet("/announcement", context =>
                            new PartialViewResult { ViewName = "_McpAnnouncementPartial" }.ExecuteResultAsync(
                                new ActionContext(context, new RouteData(), new ActionDescriptor())));
                    });
                })).Start();
            _client = _host.GetTestClient();
            _client.BaseAddress = new Uri("https://docs.example.com");
            _client.DefaultRequestHeaders.Add("X-Test-Authenticated", "true");
        }

        [Fact]
        public async Task SignedInUsersSeeConfigurationLinkAndBothDismissalOptions()
        {
            using var response = await _client.GetAsync("/announcement?filter=example", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Contains("MCP server announcement", html);
            Assert.Contains("href=\"/Home/ManageAccount\"", html);
            Assert.Contains("action=\"/Home/DismissMcpAnnouncement\"", html);
            Assert.Contains("Hide once", html);
            Assert.Contains("Hide forever", html);
            Assert.Contains("value=\"/announcement?filter=example\"", html);
            Assert.Contains("__RequestVerificationToken", html);
        }

        [Theory]
        [InlineData(false, true, null, false)]
        [InlineData(true, false, null, false)]
        [InlineData(true, true, "true", false)]
        [InlineData(true, true, "unexpected", true)]
        public async Task BannerRespectsAuthenticationFeatureFlagAndDismissalCookie(
            bool authenticated, bool enabled, string cookie, bool visible)
        {
            _host.Services.GetRequiredService<McpSettings>().Enabled = enabled;
            if (!authenticated)
            {
                _client.DefaultRequestHeaders.Remove("X-Test-Authenticated");
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, "/announcement");
            if (cookie != null)
            {
                request.Headers.Add("Cookie", $"{AppConstants.MCP_ANNOUNCEMENT_DISMISSED_COOKIE}={cookie}");
            }
            using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(visible, html.Contains("MCP server announcement", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DismissalSetsRequestedCookieLifetimeAndHidesBannerOnFollowingPages(bool hideForever)
        {
            using var response = await DismissAsync(hideForever);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/announcement?filter=example", response.Headers.Location.OriginalString);
            var cookie = Assert.Single(SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList()),
                c => c.Name == AppConstants.MCP_ANNOUNCEMENT_DISMISSED_COOKIE);
            Assert.Equal("true", cookie.Value.ToString());
            Assert.Equal("/", cookie.Path.ToString());
            Assert.True(cookie.HttpOnly);
            Assert.True(cookie.Secure);
            Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
            Assert.Equal(hideForever ? TimeSpan.FromDays(400) : (TimeSpan?)null, cookie.MaxAge);
            Assert.Null(cookie.Expires);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/announcement");
            request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
            using var nextPage = await _client.SendAsync(request, TestContext.Current.CancellationToken);
            nextPage.EnsureSuccessStatusCode();
            Assert.DoesNotContain("MCP server announcement",
                await nextPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DismissalRequiresAuthentication()
        {
            _client.DefaultRequestHeaders.Remove("X-Test-Authenticated");
            using var response = await _client.PostAsync("/Home/DismissMcpAnnouncement",
                new FormUrlEncodedContent(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("https://docs.example.com/Account/Login", response.Headers.Location.AbsoluteUri);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task DismissalRequiresAntiforgeryToken()
        {
            using var response = await DismissAsync(false, includeToken: false);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        [Theory]
        [InlineData("https://untrusted.example.com")]
        [InlineData("//untrusted.example.com")]
        [InlineData("")]
        public async Task InvalidReturnUrlsDoNotSetDismissalCookie(string returnUrl)
        {
            using var response = await DismissAsync(false, returnUrl);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("A local return URL is required.",
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        private async Task<HttpResponseMessage> DismissAsync(bool hideForever,
            string returnUrl = "/announcement?filter=example", bool includeToken = true)
        {
            using var page = await _client.GetAsync("/announcement", TestContext.Current.CancellationToken);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            var values = new Dictionary<string, string>
            {
                ["hideForever"] = hideForever.ToString(),
                ["returnUrl"] = returnUrl
            };
            if (includeToken)
            {
                values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Home/DismissMcpAnnouncement")
            {
                Content = new FormUrlEncodedContent(values)
            };
            request.Headers.Add("Cookie", string.Join("; ", SetCookieHeaderValue.ParseList(
                page.Headers.GetValues("Set-Cookie").ToList()).Select(c => $"{c.Name}={c.Value}")));
            return await _client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            _client.Dispose();
            _host.Dispose();
        }
    }
}