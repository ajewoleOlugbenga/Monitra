using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Api.Services;
using Monitra.Core.Security;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(MonitraDbContext dbContext, JwtTokenService jwtTokenService)
    {
        _dbContext = dbContext;
        _jwtTokenService = jwtTokenService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and Password are required.");
        }

        // Query tenant user (Ignore filters to check credentials before setting scope)
        var user = await _dbContext.TenantUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !PasswordHashHelper.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid email or password.");
        }

        // Verify tenant is active
        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId);

        if (tenant == null || tenant.Status == Core.Enums.TenantStatus.Deleted)
        {
            return Unauthorized("Organization not found.");
        }

        if (tenant.Status == Core.Enums.TenantStatus.Suspended)
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Organization is suspended.");
        }

        // Generate token and write to HttpOnly Cookie
        var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Role.ToString(), user.TenantId);
        
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Enforce in prod, but browsers accept locally over localhost
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7),
            Path = "/"
        };

        Response.Cookies.Append("MonitraSession", token, cookieOptions);

        return Ok(new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role.ToString(),
            TenantId = user.TenantId,
            TenantSlug = tenant.Slug
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("MonitraSession", new CookieOptions
        {
            Path = "/"
        });
        return Ok("Logged out successfully.");
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        // Automatic tenant query filtering will restrict lookup to user's tenant
        var user = await _dbContext.TenantUsers.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return NotFound("User profile not found.");
        }

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            Role = user.Role.ToString(),
            user.TenantId
        });
    }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
}
