using System;
using System.Linq;
using System.Security.Claims;
using Dangl.WebDocumentation.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Dangl.WebDocumentation
{
    public class RefreshUserClaimsFilterAttribute : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext context)
        {
            var User = context.HttpContext.User;
            if (!User.Identity.IsAuthenticated)
            {
                return;
            }
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var stampFromClaims = User.Claims.FirstOrDefault(Claim => Claim.Type == "ClaimsStamp")?.Value;
            var stampFromDb = dbContext.UserClaims.FirstOrDefault(UserClaim => UserClaim.UserId == User.GetUserId() && UserClaim.ClaimType == "ClaimsStamp")?.ClaimValue;
            if (string.IsNullOrWhiteSpace(stampFromClaims) || string.IsNullOrWhiteSpace(stampFromDb) || stampFromClaims != stampFromDb)
            {
                var dbUser = dbContext.Users.FirstOrDefault(UserInDb => UserInDb.Id == User.GetUserId());
                // Need to recreate
                if (string.IsNullOrWhiteSpace(stampFromDb))
                {
                    // No stamp at all
                    var userManager = context.HttpContext.ApplicationServices.GetRequiredService<UserManager<ApplicationUser>>();
                    userManager.AddClaimAsync(dbUser, new Claim("ClaimsStamp", Guid.NewGuid().ToString())).Wait();
                }
                var signInManager = context.HttpContext.ApplicationServices.GetRequiredService<SignInManager<ApplicationUser>>();
                signInManager.RefreshSignInAsync(dbUser).Wait();
            }
        }
    }
}