using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Monitra.Api.Middleware;
using Monitra.Api.Services;
using Monitra.Core.Interfaces;
using Monitra.Infrastructure.Data;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. Set ConnectionStrings__DefaultConnection " +
        "as an environment variable, or via dotnet user-secrets in development.");
}

builder.Services.AddDbContext<MonitraDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. HTTP and Scoped Context Resolvers
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<JwtTokenService>();

// 3. Rate Limiting Setup
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("LoginLimiter", opt =>
    {
        opt.PermitLimit = 5; // Allow 5 login attempts
        opt.Window = TimeSpan.FromMinutes(1); // Per minute
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("RegistrationLimiter", opt =>
    {
        opt.PermitLimit = 3; // Allow 3 registrations
        opt.Window = TimeSpan.FromMinutes(5); // Per 5 minutes
        opt.QueueLimit = 0;
    });
});

// 4. JWT Authentication
var keyString = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(keyString) || keyString.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is not configured or is too short. Set Jwt__Key (at least 32 characters) as an " +
        "environment variable, or via dotnet user-secrets in development.");
}
var key = Encoding.UTF8.GetBytes(keyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MonitraApi",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MonitraDashboard",
        IssuerSigningKey = new SymmetricSecurityKey(key),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Extract from standard Cookie header if Bearer header is missing
            if (context.Request.Cookies.TryGetValue("MonitraSession", out var tenantCookieToken))
            {
                context.Token = tenantCookieToken;
            }
            else if (context.Request.Cookies.TryGetValue("MonitraPlatformSession", out var platformCookieToken))
            {
                context.Token = platformCookieToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminPolicy", policy => policy.RequireRole("SuperAdmin"));
    // Any signed-in tenant user, regardless of role - for endpoints like "view my own profile".
    options.AddPolicy("TenantUserPolicy", policy => policy.RequireRole("Owner", "Admin", "Viewer", "ITSupport"));
    // Behavioral/productivity data - inactivity incidents, breaks, reports. Deliberately
    // excludes ITSupport (see Monitra Architecture Reference §14: IT gets device health/logs
    // only, never why an employee was idle).
    options.AddPolicy("BehavioralViewPolicy", policy => policy.RequireRole("Owner", "Admin", "Viewer"));
    // Issuing actions, reviewing inactivity, managing tenant settings - never Viewer or ITSupport.
    options.AddPolicy("TenantManagerPolicy", policy => policy.RequireRole("Owner", "Admin"));
    // Device fleet health/logs only. Deliberately excludes Admin/Viewer - IT should not need
    // (and should not default into) visibility over behavioral/productivity data.
    options.AddPolicy("ITPolicy", policy => policy.RequireRole("Owner", "ITSupport"));
});

// 5. CORS Configurations
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Dashboard address
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Essential for HttpOnly secure cookies
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// 6. DB Migration & Seeding Sequence
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MonitraDbContext>();
        var isProduction = app.Environment.IsProduction();
        // Invoke database creation & development seeding
        await DbSeeder.SeedAsync(context, isProduction);
        Console.WriteLine("Database migration and seeding completed successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred migrating or seeding the DB: {ex.Message}");
    }
}

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();

// Injects the multi-tenant isolation borders
app.UseMiddleware<TenantIsolationMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
