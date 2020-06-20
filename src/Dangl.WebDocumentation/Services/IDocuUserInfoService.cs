using System;
using System.Threading.Tasks;

namespace Dangl.WebDocumentation.Services
{
    public interface IDocuUserInfoService
    {
        Task<Guid?> GetCurrentUserIdOrNullAsync();
    }
}
