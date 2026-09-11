namespace Monitra.Agent;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public int HeartbeatIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Bootstrap install token used only for first-run registration. In a real deployment this
    /// comes from the MSI installer or the MONITRA_INSTALL_TOKEN environment variable, not from
    /// a committed config file. Once the agent registers, the issued device token (not this
    /// value) is what's used for every subsequent call.
    /// </summary>
    public string? InstallToken { get; set; }
}
