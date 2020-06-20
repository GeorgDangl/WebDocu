using Dangl.Identity.Client.Mvc.Services;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public class DocuUserInfoService : IDocuUserInfoService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserInfoService _userInfoService;

        public DocuUserInfoService(IHttpContextAccessor httpContextAccessor,
            IUserInfoService userInfoService)
        {
            _httpContextAccessor = httpContextAccessor;
            _userInfoService = userInfoService;
        }

        public async Task<Guid?> GetCurrentUserIdOrNullAsync()
        {
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false)
            {
                return await _userInfoService.GetCurrentUserIdAsync();
            }

            return null;
        }
    }
}
