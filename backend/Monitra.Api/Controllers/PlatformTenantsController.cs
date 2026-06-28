using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Enums;
using Monitra.Core.Security;
using Monitra.Infrastructure.Data;
using System.Security.Cryptography;
using System.Text;

namespace Monitra.Api.Controllers;

[ApiController]
[Route("api/platform/tenants")]
[Authorize(Roles = "SuperAdmin")]
public class PlatformTenantsController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;

    public PlatformTenantsController(MonitraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        var query = _dbContext.Tenants.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();

        return Ok(new { total, page, limit, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenant = await _dbContext.Tenants.FindAsync(id);
        if (tenant == null) return NotFound("Tenant not found.");
        return Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
        {
            return BadRequest("Name and Slug are required.");
        }

        var normalizedSlug = request.Slug.ToLowerInvariant().Trim();
        if (await _dbContext.Tenants.AnyAsync(t => t.Slug == normalizedSlug))
        {
            return BadRequest("Slug is already in use.");
        }

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name,
            Slug = normalizedSlug,
            Timezone = request.Timezone ?? "UTC",
            Status = TenantStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Tenants.Add(tenant);

        // If platform admin optionally provided owner admin details
        TenantUser? owner = null;
        if (!string.IsNullOrEmpty(request.AdminEmail) && !string.IsNullOrEmpty(request.AdminPassword))
        {
            owner = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                FullName = request.AdminFullName ?? "Tenant Owner",
                Email = request.AdminEmail,
                PasswordHash = PasswordHashHelper.HashPassword(request.AdminPassword),
                Role = TenantUserRole.Owner,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.TenantUsers.Add(owner);
        }

        await _dbContext.SaveChangesAsync();

        // Create initial Audit Log
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = null, // Global platform event
            ActorType = "PlatformAdmin",
            ActorId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "System",
            Action = "tenant_created",
            EntityType = "Tenant",
            EntityId = tenantId.ToString(),
            MetadataJson = $"{{\"TenantName\":\"{tenant.Name}\", \"Slug\":\"{tenant.Slug}\"}}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            Tenant = tenant,
            AdminCreated = owner != null,
            AdminEmail = owner?.Email
        });
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var tenant = await _dbContext.Tenants.FindAsync(id);
        if (tenant == null) return NotFound("Tenant not found.");

        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = DateTime.UtcNow;

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            ActorType = "PlatformAdmin",
            ActorId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "System",
            Action = "tenant_activated",
            EntityType = "Tenant",
            EntityId = id.ToString(),
            MetadataJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync();
        return Ok(new { tenant.Id, tenant.Status });
    }

    [HttpPost("{id}/suspend")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var tenant = await _dbContext.Tenants.FindAsync(id);
        if (tenant == null) return NotFound("Tenant not found.");

        tenant.Status = TenantStatus.Suspended;
        tenant.UpdatedAt = DateTime.UtcNow;

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            ActorType = "PlatformAdmin",
            ActorId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "System",
            Action = "tenant_suspended",
            EntityType = "Tenant",
            EntityId = id.ToString(),
            MetadataJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync();
        return Ok(new { tenant.Id, tenant.Status });
    }

    [HttpPost("{tenantId}/admins")]
    public async Task<IActionResult> AddAdmin(Guid tenantId, [FromBody] AddAdminRequest request)
    {
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound("Tenant not found.");

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and Password are required.");
        }

        // Verify uniqueness across all users
        if (await _dbContext.TenantUsers.IgnoreQueryFilters().AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest("Email is already registered.");
        }

        var newAdmin = new TenantUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = PasswordHashHelper.HashPassword(request.Password),
            Role = TenantUserRole.Admin,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.TenantUsers.Add(newAdmin);

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorType = "PlatformAdmin",
            ActorId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "System",
            Action = "tenant_admin_created",
            EntityType = "TenantUser",
            EntityId = newAdmin.Id.ToString(),
            MetadataJson = $"{{\"Email\":\"{newAdmin.Email}\"}}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync();
        return Ok(new { newAdmin.Id, newAdmin.Email, newAdmin.Role });
    }

    [HttpPost("{tenantId}/install-tokens")]
    public async Task<IActionResult> GenerateInstallToken(Guid tenantId, [FromBody] GenerateTokenRequest request)
    {
        var tenant = await _dbContext.Tenants.FindAsync(tenantId);
        if (tenant == null) return NotFound("Tenant not found.");

        // Generate raw unique token
        var rawToken = $"MONITRA-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}";
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var installToken = new AgentInstallToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TokenHash = tokenHash,
            Label = request.Label ?? "Standard Activation Token",
            Status = TokenStatus.Active,
            MaxUses = request.MaxUses > 0 ? request.MaxUses : 10,
            UsedCount = 0,
            ExpiresAt = request.ExpiresInDays.HasValue ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value) : DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.AgentInstallTokens.Add(installToken);

        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorType = "PlatformAdmin",
            ActorId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? "System",
            Action = "install_token_created",
            EntityType = "AgentInstallToken",
            EntityId = installToken.Id.ToString(),
            MetadataJson = $"{{\"Label\":\"{installToken.Label}\"}}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AuditLogs.Add(audit);

        await _dbContext.SaveChangesAsync();

        // Return the RAW token once. It is not saved in plain text.
        return Ok(new
        {
            TokenId = installToken.Id,
            RawActivationToken = rawToken,
            ExpiresAt = installToken.ExpiresAt,
            Label = installToken.Label
        });
    }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Timezone { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string? AdminFullName { get; set; }
}

public class AddAdminRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class GenerateTokenRequest
{
    public string? Label { get; set; }
    public int MaxUses { get; set; } = 10;
    public int? ExpiresInDays { get; set; }
}
