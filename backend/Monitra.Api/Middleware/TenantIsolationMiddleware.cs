using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Enums;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Middleware;

public class TenantIsolationMiddleware
{
    private readonly RequestDelegate _next;

    public TenantIsolationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 1. Skip middleware check for public/auth paths
        if (path.StartsWith("/api/auth/login") ||
            path.StartsWith("/api/platform/auth/login") ||
            path.StartsWith("/api/agent/register"))
        {
            await _next(context);
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MonitraDbContext>();

        // 2. Handle Agent APIs and the agent's SignalR push channel - both use device-token
        // auth, not JWT. The hub connection is exempted from the "device suspended" block the
        // same way heartbeat is: it's a passive receive channel, not a data-ingestion endpoint.
        if (path.StartsWith("/api/agent/") || path.StartsWith("/hubs/agent"))
        {
            var suspensionExempt = path.StartsWith("/api/agent/heartbeat") || path.StartsWith("/hubs/agent");
            var authResult = await TryAuthenticateDeviceAsync(context, dbContext, suspensionExempt);
            if (!authResult)
            {
                return; // response already written by TryAuthenticateDeviceAsync
            }

            await _next(context);
            return;
        }

        // 3. Handle Tenant Admin APIs
        if (path.StartsWith("/api/platform/"))
        {
            // Platform admin endpoints (e.g. platform dashboard metrics)
            // JWT verification will be checked by standard bearer authentication
            await _next(context);
            return;
        }

        // Default: Tenant user dashboard endpoints
        var user = context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = user.FindFirst("tenant_id")?.Value;
            if (string.IsNullOrEmpty(tenantClaim) || !Guid.TryParse(tenantClaim, out var tenantId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Tenant ID missing in authentication claims.");
                return;
            }

            // Validate that the Tenant is active
            var tenant = await dbContext.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Tenant not found.");
                return;
            }

            if (tenant.Status == TenantStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Account Suspended. Please contact support.");
                return;
            }

            context.Items["TenantId"] = tenantId;
        }

        await _next(context);
    }

    /// <summary>
    /// Validates the X-Device-Token header, resolves TenantId/DeviceId/EmployeeId into
    /// HttpContext.Items, and updates LastUsedAt/LastSeenAt. Writes an error response and
    /// returns false on any failure; the caller must not call _next(context) in that case.
    /// </summary>
    private static async Task<bool> TryAuthenticateDeviceAsync(HttpContext context, MonitraDbContext dbContext, bool suspensionExempt)
    {
        if (!context.Request.Headers.TryGetValue("X-Device-Token", out var tokenValues) ||
            string.IsNullOrEmpty(tokenValues.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("X-Device-Token header is missing.");
            return false;
        }

        string rawToken = tokenValues.ToString();
        string tokenHash = ComputeSha256Hash(rawToken);

        // Fetch device token mapping (Ignore global filter because this runs before tenant context is set)
        var tokenMapping = await dbContext.DeviceTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(dt => dt.TokenHash == tokenHash && dt.Status == "Active");

        if (tokenMapping == null || tokenMapping.RevokedAt != null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or revoked device token.");
            return false;
        }

        // Check if device or tenant is suspended
        var device = await dbContext.Devices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == tokenMapping.DeviceId);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tokenMapping.TenantId);

        if (device == null || device.DeviceStatus == DeviceStatus.Revoked || tenant == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Device or Tenant not found.");
            return false;
        }

        if (tenant.Status == TenantStatus.Suspended)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant is suspended.");
            return false;
        }

        if (device.DeviceStatus == DeviceStatus.Suspended && !suspensionExempt)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Device is suspended.");
            return false;
        }

        // Cache credentials in HttpContext items for TenantProvider
        context.Items["TenantId"] = tokenMapping.TenantId;
        context.Items["DeviceId"] = tokenMapping.DeviceId;
        context.Items["EmployeeId"] = device.EmployeeId;

        // Track last active timestamp
        tokenMapping.LastUsedAt = DateTime.UtcNow;
        device.LastSeenAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        return true;
    }

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawData);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
