using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPMS.Core.DTOs;
using NPMS.Core.Services;

namespace NPMS.WebAPI.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IProductService _service;

        public AuthController(IProductService service)
        {
            _service = service;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var res = _service.Authenticate(request.Username, request.Password);
            if (!res.Success)
            {
                return Unauthorized(res);
            }
            return Ok(res);
        }
    }
}
