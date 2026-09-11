using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Monitra.Agent.Services;

public record DeviceCredentials(Guid DeviceId, Guid TenantId, string RawDeviceToken);

/// <summary>
/// Persists the device token issued at registration so the agent survives restarts without
/// re-registering. The token is encrypted at rest with Windows DPAPI, scoped to the current
/// user (this now runs as a per-user app, not a service - see the v0.2 architecture note in
/// Monitra.Agent.csproj) rather than stored as plain text.
/// </summary>
[SupportedOSPlatform("windows")]
public class DeviceCredentialStore
{
    private readonly string _filePath;

    public DeviceCredentialStore()
    {
        // LocalApplicationData, not CommonApplicationData: this runs unelevated as the logged-in
        // user, which may not have write access to %ProgramData% without install-time ACL setup.
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Monitra");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "device.dat");
    }

    public bool Exists() => File.Exists(_filePath);

    public async Task SaveAsync(DeviceCredentials credentials, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(credentials);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_filePath, protectedBytes, cancellationToken);
    }

    public async Task<DeviceCredentials?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!Exists())
        {
            return null;
        }

        var protectedBytes = await File.ReadAllBytesAsync(_filePath, cancellationToken);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var json = Encoding.UTF8.GetString(plainBytes);
        return JsonSerializer.Deserialize<DeviceCredentials>(json);
    }
}
