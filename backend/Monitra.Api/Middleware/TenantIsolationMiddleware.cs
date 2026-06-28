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

        // 2. Handle Agent APIs
        if (path.StartsWith("/api/agent/"))
        {
            if (!context.Request.Headers.TryGetValue("X-Device-Token", out var tokenValues) || 
                string.IsNullOrEmpty(tokenValues.ToString()))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("X-Device-Token header is missing.");
                return;
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
                return;
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
                return;
            }

            if (tenant.Status == TenantStatus.Suspended)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Tenant is suspended.");
                return;
            }

            if (device.DeviceStatus == DeviceStatus.Suspended)
            {
                // In suspended status, heartbeat can continue (read-only check), but other ingestion endpoints fail.
                if (!path.StartsWith("/api/agent/heartbeat"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Device is suspended.");
                    return;
                }
            }

            // Cache credentials in HttpContext items for TenantProvider
            context.Items["TenantId"] = tokenMapping.TenantId;
            context.Items["DeviceId"] = tokenMapping.DeviceId;
            context.Items["EmployeeId"] = device.EmployeeId;

            // Track last active timestamp
            tokenMapping.LastUsedAt = DateTime.UtcNow;
            device.LastSeenAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();

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

    private static string ComputeSha256Hash(string rawData)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(rawData);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
