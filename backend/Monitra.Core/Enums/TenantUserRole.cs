namespace Monitra.Core.Enums;

public enum TenantUserRole
{
    Owner,
    Admin,
    Viewer,
    // Device fleet health and logs only - deliberately excluded from behavioral/productivity
    // data (inactivity, breaks, reports). See Monitra Architecture Reference §14.
    ITSupport
}
