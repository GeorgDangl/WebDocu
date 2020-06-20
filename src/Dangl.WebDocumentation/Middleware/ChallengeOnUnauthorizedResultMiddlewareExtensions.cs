using Microsoft.AspNetCore.Builder;

namespace Dangl.WebDocumentation.Middleware
{
    public static class ChallengeOnUnauthorizedResultMiddlewareExtensions
    {
        public static IApplicationBuilder UseChallengeOnUnauthorizedResponse(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ChallengeOnUnauthorizedResultMiddleware>();
        }
    }
}
