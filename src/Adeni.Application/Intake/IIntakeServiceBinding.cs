namespace Adeni.Application.Intake;

using Adeni.Domain.Common;

public sealed record IntakeServiceBinding(Guid? TemplateId, string Revision);
public interface IIntakeServiceQuery
{
    Task<bool> ExistsAsync(Guid tenantId, Guid serviceId, CancellationToken cancellationToken);
}
public interface IIntakeServiceBinding
{
    Task<Result<IntakeServiceBinding>> GetAsync(Guid tenantId, string actor, Guid serviceId, CancellationToken cancellationToken = default);
    Task<Result<IntakeServiceBinding>> SetAsync(Guid tenantId, string actor, Guid serviceId, Guid? templateId, string revision, CancellationToken cancellationToken = default);
}
