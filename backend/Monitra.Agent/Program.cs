using Monitra.Agent;
using Monitra.Agent.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

builder.Services.AddHttpClient<ApiClient>();
builder.Services.AddSingleton<DeviceCredentialStore>();
builder.Services.AddHostedService<Worker>();

// Runs as a normal console app when launched directly (for local dev/debugging) and as a
// Windows Service when installed via `sc create` / the MSI installer.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Monitra Agent";
});

var host = builder.Build();
host.Run();
