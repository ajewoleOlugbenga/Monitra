using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Core.Interfaces;
using Monitra.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace Monitra.Tests;

public class PostgresIsolationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("monitradb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private class TestTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; set; }
    }

    [Fact]
    public async Task QueryFilters_ShouldIsolateTenantData_OnRealPostgres()
    {
        // Arrange
        var connectionString = _postgresContainer!.GetConnectionString();
        var options = new DbContextOptionsBuilder<MonitraDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var tenantProvider = new TestTenantProvider();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Enforce DB schema creation on real Postgres database
        using (var setupContext = new MonitraDbContext(options, new TestTenantProvider { TenantId = null }))
        {
            await setupContext.Database.EnsureCreatedAsync();

            setupContext.Employees.Add(new Employee { Id = Guid.NewGuid(), TenantId = tenantA, FullName = "Postgres Emp A", Email = "a@acme.com" });
            setupContext.Employees.Add(new Employee { Id = Guid.NewGuid(), TenantId = tenantB, FullName = "Postgres Emp B", Email = "b@acme.com" });
            await setupContext.SaveChangesAsync();
        }

        // Act & Assert: Query as Tenant A
        tenantProvider.TenantId = tenantA;
        using (var contextA = new MonitraDbContext(options, tenantProvider))
        {
            var employees = await contextA.Employees.ToListAsync();
            Assert.Single(employees);
            Assert.Equal("Postgres Emp A", employees[0].FullName);
        }

        // Act & Assert: Query as Tenant B
        tenantProvider.TenantId = tenantB;
        using (var contextB = new MonitraDbContext(options, tenantProvider))
        {
            var employees = await contextB.Employees.ToListAsync();
            Assert.Single(employees);
            Assert.Equal("Postgres Emp B", employees[0].FullName);
        }
    }
}
