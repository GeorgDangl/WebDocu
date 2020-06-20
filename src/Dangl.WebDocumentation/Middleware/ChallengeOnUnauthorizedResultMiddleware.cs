using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Middleware
{
    public class ChallengeOnUnauthorizedResultMiddleware
    {
        // Taken from https://stackoverflow.com/a/50022355/4190785
        private readonly RequestDelegate _next;

        private readonly IAuthenticationSchemeProvider _schemes;

        public ChallengeOnUnauthorizedResultMiddleware(RequestDelegate next, IAuthenticationSchemeProvider schemes)
        {
            _next = next;
            _schemes = schemes;
        }

        public async Task Invoke(HttpContext context)
        {
            context.Response.OnStarting(async () =>
            {
                if (context.Response.StatusCode == (int)HttpStatusCode.Unauthorized)
                {
                    var defaultChallenge = await _schemes.GetDefaultChallengeSchemeAsync();
                    if (defaultChallenge != null)
                    {
                        await context.ChallengeAsync(defaultChallenge.Name);
                    }
                }
                await Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
