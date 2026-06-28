using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Interfaces;

namespace Monitra.Infrastructure.Data;

public class MonitraDbContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public MonitraDbContext(DbContextOptions<MonitraDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PlatformUser> PlatformUsers => Set<PlatformUser>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AgentInstallToken> AgentInstallTokens => Set<AgentInstallToken>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // This property will be evaluated dynamically by EF Core's query compilation for HasQueryFilter
    public Guid CurrentTenantId => _tenantProvider.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure indexes and schemas
        modelBuilder.Entity<Tenant>()
            .HasIndex(t => t.Slug)
            .IsUnique();

        modelBuilder.Entity<PlatformUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<TenantUser>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Employee>()
            .HasIndex(e => new { e.TenantId, e.Email })
            .IsUnique();

        modelBuilder.Entity<DeviceToken>()
            .HasIndex(dt => dt.TokenHash)
            .IsUnique();

        modelBuilder.Entity<AgentInstallToken>()
            .HasIndex(it => it.TokenHash)
            .IsUnique();

        // Apply Global Query Filters for all ITenantScoped entities
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

                var dbContextExpression = Expression.Constant(this);
                var tenantIdProperty = typeof(MonitraDbContext).GetProperty(nameof(CurrentTenantId))!;
                var currentTenantIdExpression = Expression.Property(dbContextExpression, tenantIdProperty);

                var filterExpression = Expression.Lambda(
                    Expression.Equal(property, currentTenantIdExpression),
                    parameter
                );

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filterExpression);
            }
        }
    }

    public override int SaveChanges()
    {
        EnforceTenantIsolation();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceTenantIsolation();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceTenantIsolation()
    {
        var tenantId = _tenantProvider.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added)
            {
                if (tenantId == null || tenantId == Guid.Empty)
                {
                    // Allow manual TenantId assignment ONLY if this is registered from Platform context
                    if (entry.Entity.TenantId == Guid.Empty)
                    {
                        throw new InvalidOperationException("Tenant ID context is missing for creating tenant-scoped entity.");
                    }
                }
                else
                {
                    // Force the tenant ID resolved from the credentials
                    entry.Entity.TenantId = tenantId.Value;
                }
            }
            else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                // Verify tenant boundary on modification/deletion
                if (tenantId == null || tenantId == Guid.Empty)
                {
                    throw new InvalidOperationException("Tenant ID context is missing for modifying tenant-scoped entity.");
                }

                var tenantIdProp = entry.Property(nameof(ITenantScoped.TenantId));
                var originalTenantId = (Guid)tenantIdProp.OriginalValue;
                var currentTenantId = (Guid)tenantIdProp.CurrentValue;

                if (originalTenantId != tenantId.Value)
                {
                    throw new InvalidOperationException("Cross-tenant database modification detected and rejected.");
                }

                if (currentTenantId != originalTenantId)
                {
                    throw new InvalidOperationException("Changing the TenantId of an existing entity is prohibited.");
                }
            }
        }
    }
}
