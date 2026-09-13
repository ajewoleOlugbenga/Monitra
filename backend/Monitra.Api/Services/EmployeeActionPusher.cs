using Microsoft.AspNetCore.SignalR;
using Monitra.Api.Hubs;
using Monitra.Core.Entities;

namespace Monitra.Api.Services;

/// <summary>
/// Wraps the SignalR push for a newly-created EmployeeAction, keeping controllers free of
/// hub-specific code. This is fire-and-forget from SendAsync's perspective - there's no
/// built-in "was anyone actually connected" signal from a group send, which is why
/// EmployeeAction.Status only moves to Delivered when the agent calls back to confirm it
/// showed the notification (see AgentController.AcknowledgeAction), not from this call alone.
/// </summary>
public class EmployeeActionPusher
{
    private readonly IHubContext<AgentHub> _hubContext;

    public EmployeeActionPusher(IHubContext<AgentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PushAsync(EmployeeAction action)
    {
        var payload = new
        {
            action.Id,
            ActionType = action.ActionType.ToString(),
            Severity = action.Severity.ToString(),
            action.Message,
            action.CreatedAt
        };

        return _hubContext.Clients
            .Group(AgentHub.EmployeeGroup(action.EmployeeId))
            .SendAsync("ReceiveAction", payload);
    }
}
