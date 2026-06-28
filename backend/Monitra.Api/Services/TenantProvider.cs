using Monitra.Core.Interfaces;

namespace Monitra.Api.Services;

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return null;
            }

            // 1. Check if resolved by middleware (e.g. from X-Device-Token or custom route resolving)
            if (httpContext.Items.TryGetValue("TenantId", out var cachedTenantId) && cachedTenantId is Guid guid)
            {
                return guid;
            }

            // 2. Check if present in user claims (JWT authentication context)
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var tenantClaim = user.FindFirst("tenant_id")?.Value;
                if (Guid.TryParse(tenantClaim, out var parsedGuid))
                {
                    // Cache it in HttpContext.Items for speed in downstream calls
                    httpContext.Items["TenantId"] = parsedGuid;
                    return parsedGuid;
                }
            }

            return null;
        }
    }
}
