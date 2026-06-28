using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Enums;
using Monitra.Core.Security;
using System.Security.Cryptography;
using System.Text;

namespace Monitra.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(MonitraDbContext context, bool isProduction)
    {
        // Enforce safety: Never seed test credentials in production environments
        if (isProduction)
        {
            return;
        }

        // Apply any pending migrations or ensure database is created
        await context.Database.EnsureCreatedAsync();

        // 1. Seed Platform Admins
        if (!await context.PlatformUsers.AnyAsync())
        {
            var superAdmin = new PlatformUser
            {
                Id = Guid.NewGuid(),
                FullName = "Platform Super Admin",
                Email = "admin@monitra.local",
                PasswordHash = PasswordHashHelper.HashPassword("AdminPass123!"),
                Role = "SuperAdmin",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.PlatformUsers.Add(superAdmin);
            await context.SaveChangesAsync();
        }

        // 2. Seed default Tenant (Acme Corp)
        Guid acmeTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var acmeTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == acmeTenantId);
        if (acmeTenant == null)
        {
            acmeTenant = new Tenant
            {
                Id = acmeTenantId,
                Name = "Acme Corporation",
                Slug = "acme",
                Status = TenantStatus.Active,
                Timezone = "UTC",
                DefaultWorkStartTime = TimeSpan.FromHours(9), // 9:00 AM
                DefaultWorkEndTime = TimeSpan.FromHours(17),  // 5:00 PM
                WorkDays = "Monday,Tuesday,Wednesday,Thursday,Friday",
                TrackWeekends = false,
                IdleThresholdMinutes = 5,
                UrlTrackingMode = "DomainOnly",
                KeystrokeTrackingMode = "CountsOnly",
                DataRetentionDays = 90,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Tenants.Add(acmeTenant);
            // Save immediately because global filters depend on context values
            await context.SaveChangesAsync();
        }

        // 3. Seed Tenant Owner/Admin for Acme Corp
        // Note: For tenant-scoped entities, we must temporarily bypass or provide tenant context.
        // Let's add them directly to DB sets.
        if (!await context.TenantUsers.IgnoreQueryFilters().AnyAsync(u => u.TenantId == acmeTenantId))
        {
            var tenantOwner = new TenantUser
            {
                Id = Guid.NewGuid(),
                TenantId = acmeTenantId,
                FullName = "Acme Owner",
                Email = "admin@acme.com",
                PasswordHash = PasswordHashHelper.HashPassword("AcmePass123!"),
                Role = TenantUserRole.Owner,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.TenantUsers.Add(tenantOwner);
        }

        // 4. Seed Employee for Acme Corp
        if (!await context.Employees.IgnoreQueryFilters().AnyAsync(e => e.TenantId == acmeTenantId))
        {
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                TenantId = acmeTenantId,
                FullName = "John Doe",
                Email = "john@acme.com",
                EmployeeCode = "EMP001",
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Employees.Add(employee);
        }

        // 5. Seed default installation token for Acme Corp
        string installTokenValue = "ACME-INSTALL-2026";
        byte[] tokenBytes = Encoding.UTF8.GetBytes(installTokenValue);
        string tokenHash = Convert.ToHexString(SHA256.HashData(tokenBytes));

        if (!await context.AgentInstallTokens.IgnoreQueryFilters().AnyAsync(t => t.TenantId == acmeTenantId))
        {
            var token = new AgentInstallToken
            {
                Id = Guid.NewGuid(),
                TenantId = acmeTenantId,
                TokenHash = tokenHash,
                Label = "Initial Acme Onboarding",
                Status = TokenStatus.Active,
                MaxUses = 100,
                UsedCount = 0,
                ExpiresAt = DateTime.UtcNow.AddYears(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.AgentInstallTokens.Add(token);
        }

        await context.SaveChangesAsync();
    }
}
