namespace Monitra.Core.Interfaces;

public interface ITenantProvider
{
    Guid? TenantId { get; }
}
