using System.Threading.Tasks;
using Dangl.WebDocumentation.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Middleware
{
    public class ChallengeOnUnauthorizedResultMiddlewareTests
    {
        [Fact]
        public async Task RedirectsAnonymousDocumentRequestToLocalLogin()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/Projects/Dangl.AVA.Converter/2.28.15/howto/toc.html";
            context.Request.QueryString = new QueryString("?section=overview");
            var middleware = new ChallengeOnUnauthorizedResultMiddleware(httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            });

            await middleware.Invoke(context);

            Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
            Assert.Equal("/Account/Login?returnUrl=%2FProjects%2FDangl.AVA.Converter%2F2.28.15%2Fhowto%2Ftoc.html%3Fsection%3Doverview", context.Response.Headers.Location);
        }

        [Fact]
        public async Task LeavesApiUnauthorizedResponseUnchanged()
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Get;
            context.Request.Path = "/API/Projects";
            var middleware = new ChallengeOnUnauthorizedResultMiddleware(httpContext =>
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            });

            await middleware.Invoke(context);

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.False(context.Response.Headers.ContainsKey("Location"));
        }
    }
}
