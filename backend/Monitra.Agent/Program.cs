using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monitra.Agent;
using Monitra.Agent.Services;
using Monitra.Agent.Tray;

internal static class Program
{
    // WinForms requires a single-threaded apartment for the UI thread - this is that thread.
    // The generic host (registration, heartbeat, health, policy, logs) runs on the thread pool
    // via host.RunAsync() below and never touches WinForms controls directly; it only calls
    // TrayContext's thread-marshaling methods.
    [STAThread]
    private static void Main(string[] args)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));

        builder.Services.AddHttpClient<ApiClient>();
        builder.Services.AddSingleton<DeviceCredentialStore>();
        builder.Services.AddSingleton<PolicyCache>();
        builder.Services.AddSingleton<DeviceHealthService>();
        builder.Services.AddSingleton<DeviceLogBuffer>();
        builder.Services.AddSingleton<TrayContext>();
        builder.Services.AddHostedService<AgentBackgroundService>();

        var host = builder.Build();

        // TrayContext must exist (and its NotifyIcon/hidden form created) before the
        // background service starts, since it wires TrayContext.BreakRequested in its
        // constructor. Resolving it here, before host.RunAsync(), guarantees that order.
        var tray = host.Services.GetRequiredService<TrayContext>();

        _ = host.RunAsync(); // starts AgentBackgroundService on the thread pool; doesn't block

        System.Windows.Forms.Application.Run(tray); // blocks this thread until tray.ExitThread()

        host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
}
