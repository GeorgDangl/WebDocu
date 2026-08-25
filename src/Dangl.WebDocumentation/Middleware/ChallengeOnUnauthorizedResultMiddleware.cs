using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Middleware
{
    public class ChallengeOnUnauthorizedResultMiddleware
    {
        // Taken from https://stackoverflow.com/a/50022355/4190785
        private readonly RequestDelegate _next;

        public ChallengeOnUnauthorizedResultMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            var isAnonymousDocumentRequest = context.User.Identity?.IsAuthenticated != true
                && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && !context.Request.Path.StartsWithSegments("/API");
            if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized
                && isAnonymousDocumentRequest
                && !context.Response.HasStarted)
            {
                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                context.Response.Redirect($"/Account/Login?returnUrl={WebUtility.UrlEncode(returnUrl)}");
            }
        }
    }
}
