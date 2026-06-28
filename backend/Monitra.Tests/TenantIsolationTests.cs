using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Interfaces;
using Monitra.Infrastructure.Data;
using Xunit;

namespace Monitra.Tests;

public class TenantIsolationTests
{
    private class TestTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; set; }
    }

    [Fact]
    public async Task QueryFilters_ShouldIsolateTenantData()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolation_QueryFilter")
            .Options;

        var tenantProvider = new TestTenantProvider();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed both tenants' data using a context with query filters bypassed/ignored or using platform provider context
        using (var seedContext = new MonitraDbContext(options, new TestTenantProvider { TenantId = null }))
        {
            seedContext.Employees.Add(new Employee { Id = Guid.NewGuid(), TenantId = tenantA, FullName = "Emp A", Email = "a@acme.com" });
            seedContext.Employees.Add(new Employee { Id = Guid.NewGuid(), TenantId = tenantB, FullName = "Emp B", Email = "b@acme.com" });
            await seedContext.SaveChangesAsync();
        }

        // Act & Assert: Query as Tenant A
        tenantProvider.TenantId = tenantA;
        using (var contextA = new MonitraDbContext(options, tenantProvider))
        {
            var employees = await contextA.Employees.ToListAsync();
            Assert.Single(employees);
            Assert.Equal("Emp A", employees[0].FullName);
        }

        // Act & Assert: Query as Tenant B
        tenantProvider.TenantId = tenantB;
        using (var contextB = new MonitraDbContext(options, tenantProvider))
        {
            var employees = await contextB.Employees.ToListAsync();
            Assert.Single(employees);
            Assert.Equal("Emp B", employees[0].FullName);
        }
    }

    [Fact]
    public async Task SaveChanges_ShouldAutoPopulateTenantId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolation_AutoPopulate")
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantProvider = new TestTenantProvider { TenantId = tenantA };

        // Act: Save new entity as Tenant A without setting TenantId manually
        using (var context = new MonitraDbContext(options, tenantProvider))
        {
            var employee = new Employee { Id = Guid.NewGuid(), FullName = "New Employee", Email = "new@acme.com" };
            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            // Assert: Tenant ID was auto-filled by interceptor
            Assert.Equal(tenantA, employee.TenantId);
        }
    }

    [Fact]
    public async Task SaveChanges_ShouldBlockCrossTenantModification()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolation_BlockModification")
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantProvider = new TestTenantProvider();

        var employeeId = Guid.NewGuid();

        // Seed employee as Tenant B
        using (var seedContext = new MonitraDbContext(options, new TestTenantProvider { TenantId = null }))
        {
            seedContext.Employees.Add(new Employee { Id = employeeId, TenantId = tenantB, FullName = "Emp B", Email = "b@acme.com" });
            await seedContext.SaveChangesAsync();
        }

        // Act & Assert: Attempt to modify Tenant B's employee while logged in as Tenant A
        tenantProvider.TenantId = tenantA;
        using (var context = new MonitraDbContext(options, tenantProvider))
        {
            // Fetch employee with IgnoreQueryFilters to get references, then try to modify it
            var employee = await context.Employees.IgnoreQueryFilters().FirstAsync(e => e.Id == employeeId);
            employee.FullName = "Modified by Attacker";

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
            Assert.Contains("Cross-tenant database", exception.Message);
        }
    }

    [Fact]
    public async Task SaveChanges_ShouldRejectMissingTenantContext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolation_MissingContext")
            .Options;

        var tenantProvider = new TestTenantProvider { TenantId = null }; // Missing context

        // Act & Assert
        using (var context = new MonitraDbContext(options, tenantProvider))
        {
            var employee = new Employee { Id = Guid.NewGuid(), FullName = "Orphan Employee", Email = "orphan@acme.com" };
            context.Employees.Add(employee);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
            Assert.Contains("Tenant ID context is missing", exception.Message);
        }
    }

    [Fact]
    public async Task SaveChanges_ShouldBlockTenantIdChanges()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseInMemoryDatabase(databaseName: "TenantIsolation_BlockTenantIdChange")
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantProvider = new TestTenantProvider { TenantId = tenantA };

        var employeeId = Guid.NewGuid();

        // Seed employee as Tenant A
        using (var seedContext = new MonitraDbContext(options, new TestTenantProvider { TenantId = null }))
        {
            seedContext.Employees.Add(new Employee { Id = employeeId, TenantId = tenantA, FullName = "Emp A", Email = "a@acme.com" });
            await seedContext.SaveChangesAsync();
        }

        // Act & Assert: Attempt to change TenantId from tenantA to tenantB
        using (var context = new MonitraDbContext(options, tenantProvider))
        {
            var employee = await context.Employees.FirstAsync(e => e.Id == employeeId);
            employee.TenantId = tenantB; // Try to change it

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
            Assert.Contains("Changing the TenantId of an existing entity is prohibited", exception.Message);
        }
    }
}
