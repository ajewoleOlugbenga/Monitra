using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Monitra.Core.Entities;
using Monitra.Infrastructure.Data;

namespace Monitra.Api.Controllers;

// The employee directory itself - who exists, their device assignment. Separate from
// EmployeeMonitoringController (inactivity/breaks/actions) and DevicesController (health/logs),
// which both reference employees by id but don't own the directory.
[ApiController]
[Route("api/employees")]
[Authorize(Policy = "BehavioralViewPolicy")]
public class EmployeesController : ControllerBase
{
    private readonly MonitraDbContext _dbContext;

    public EmployeesController(MonitraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var employees = await _dbContext.Employees.AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var devices = await _dbContext.Devices.AsNoTracking()
            .Where(d => d.EmployeeId != null)
            .ToListAsync();
        var deviceByEmployee = devices
            .Where(d => d.EmployeeId.HasValue)
            .GroupBy(d => d.EmployeeId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.LastSeenAt).First());

        var result = employees.Select(e => new
        {
            e.Id,
            e.FullName,
            e.Email,
            e.EmployeeCode,
            e.Status,
            Device = deviceByEmployee.TryGetValue(e.Id, out var d)
                ? new { d.Id, d.DeviceName, d.DeviceStatus, d.LastSeenAt }
                : null
        });

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var employee = await _dbContext.Employees.FirstOrDefaultAsync(e => e.Id == id);
        if (employee == null) return NotFound("Employee not found.");
        return Ok(employee);
    }

    [HttpPost]
    [Authorize(Policy = "TenantManagerPolicy")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("FullName and Email are required.");
        }

        if (await _dbContext.Employees.AnyAsync(e => e.Email == request.Email))
        {
            return BadRequest("An employee with this email already exists.");
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            EmployeeCode = request.EmployeeCode ?? string.Empty,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        return Ok(employee);
    }
}

public class CreateEmployeeRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
}
