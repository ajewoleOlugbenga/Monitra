namespace Monitra.Core.Interfaces;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
