namespace Monitra.Core.Enums;

public enum EmployeeActionStatus
{
    Sent,          // written to the DB; not yet confirmed delivered to a connected agent
    Delivered,     // pushed live over the agent's SignalR connection
    Acknowledged   // the employee saw it and the agent confirmed back
}
