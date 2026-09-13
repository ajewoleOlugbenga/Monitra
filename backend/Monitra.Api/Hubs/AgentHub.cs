using Microsoft.AspNetCore.SignalR;

namespace Monitra.Api.Hubs;

/// <summary>
/// The agent's persistent connection for real-time pushes (currently: EmployeeAction
/// notifications). Auth is device-token based, handled entirely by
/// TenantIsolationMiddleware before the connection reaches here - there's no [Authorize]
/// attribute because this doesn't use the JWT/cookie scheme at all. Each connection joins a
/// group keyed by employee, so an action pushed to an employee reaches every device they're
/// signed in on.
/// </summary>
public class AgentHub : Hub
{
    public static string EmployeeGroup(Guid employeeId) => $"employee:{employeeId}";

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var employeeId = httpContext?.Items["EmployeeId"] as Guid?;

        if (employeeId.HasValue && employeeId.Value != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, EmployeeGroup(employeeId.Value));
        }
        // A device with no assigned employee still connects (so heartbeat/registration flows
        // aren't blocked on this), it just never receives a push - nothing to route it to.

        await base.OnConnectedAsync();
    }
}
