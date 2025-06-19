using dartford_api.Interfaces;
using dartford_api.Models;
using dartford_api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace dartford_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;

        public AuthController(IUserService userService, IAuthService authService)
        {
            _userService = userService;
            _authService = authService;
        }

        [HttpGet("getUser/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userService.GetUserById(id);
            if (user == null)
                return NotFound("User not found");

            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel login)
        {
            var user = await _userService.ValidateUserAsync(login.Email, login.Password);
            if (user == null)
                return Unauthorized("Invalid email or password");

            var token = _authService.GenerateJWTToken(new LoginModel
            {
                Id = user.Id,
                Email = user.Email
            }, user.UserType);

            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.UserName
                }
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            var existing = await _userService.GetByEmailAsync(user.Email);
            if (existing != null)
                return BadRequest("Email already registered.");

            var result = await _authService.RegisterAsync(user);
            return Ok(result);
        }
    }
}
