using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace TripWise.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        [HttpGet("status")]
        public IActionResult GetAuthStatus()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var isAuthenticated = userId.HasValue;
                var userName = HttpContext.Session.GetString("UserName");

                return Ok(new
                {
                    isAuthenticated,
                    userId,
                    userName
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    isAuthenticated = false,
                    userId = (int?)null,
                    userName = (string?)null
                });
            }
        }

        [HttpGet("session-info")]
        public IActionResult GetSessionInfo()
        {
            var sessionInfo = new
            {
                SessionId = HttpContext.Session.Id,
                UserId = HttpContext.Session.GetInt32("UserId"),
                UserName = HttpContext.Session.GetString("UserName"),
                IsAuthenticated = HttpContext.Session.GetInt32("UserId") != null,
                AllKeys = HttpContext.Session.Keys.ToList()
            };

            return Ok(sessionInfo);
        }
    }
}