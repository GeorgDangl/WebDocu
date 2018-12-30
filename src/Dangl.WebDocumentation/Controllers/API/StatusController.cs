using Dangl.WebDocumentation.Services;
using Dangl.WebDocumentation.ViewModels.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dangl.WebDocumentation.Controllers.API
{
    /// <summary>
    /// This controller reports the health status of the Dangl.Docu API
    /// </summary>
    [Route("api/status")]
    [AllowAnonymous]
    [ApiController]
    public class StatusController : ControllerBase
    {
        /// <summary>
        /// Reports the health status of the Dangl.Docu API
        /// </summary>
        /// <returns></returns>
        [HttpGet("")]
        [ProducesResponseType(typeof(StatusGet), 200)]
        public IActionResult GetStatus()
        {
            var status = new StatusGet
            {
                IsHealthy = true,
                Version = VersionsService.Version
            };
            return Ok(status);
        }
    }
}
