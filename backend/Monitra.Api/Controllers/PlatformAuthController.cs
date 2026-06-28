using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Api.Services;
using Monitra.Core.Security;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

[ApiController]
[Route("api/platform/auth")]
public class PlatformAuthController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;

    public PlatformAuthController(MonitraDbContext dbContext, JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PlatformLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and Password are required.");
        }

        var user = await _dbContext.PlatformUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !PasswordHashHelper.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid platform credentials.");
        }

        if (user.Status != "Active")
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Platform admin profile is suspended.");
        }

        // Generate token (no tenant ID for Platform Admin)
        var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Role);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // local debugging accepts secure over HTTP localhost
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(1), // Shorter session for platform admin
            Path = "/"
        };

        // Separate cookie name for platform context
        Response.Cookies.Append("MonitraPlatformSession", token, cookieOptions);

        return Ok(new PlatformLoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("MonitraPlatformSession", new CookieOptions
        {
            Path = "/"
        });
        return Ok("Logged out successfully.");
    }
}

public class PlatformLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PlatformLoginResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
