namespace Adeni.Application.Intake;

using Adeni.Domain.Common;

public sealed record IntakeTemplateDetail(Guid Id, bool IsActive, string Revision, Guid? PublishedVersionId,
    Guid? DraftVersionId, IntakeDefinition? Draft, IntakeDefinition? Published);

/// <summary>Identity-owned adapter: explicit persisted Owner/Manager membership, never legacy-role normalization.</summary>
public interface IIntakeManagementAccessQuery
{
    Task<bool> CanManageAsync(Guid tenantId, string auth0Sub, CancellationToken cancellationToken);
}

public sealed record IntakeTemplateSummary(Guid Id, string Name, bool IsActive, Guid? PublishedVersionId, string Revision);
public interface IIntakeTemplateService
{
    Task<Result<IReadOnlyList<IntakeTemplateSummary>>> ListAsync(Guid tenantId, string actor, int page = 1, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> UpdateAsync(Guid tenantId, string actor, Guid templateId, string name, bool active, string revision, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> CreateAsync(Guid tenantId, string auth0Sub, string name, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> GetAsync(Guid tenantId, string auth0Sub, Guid templateId, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> SaveDraftAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision,
        IntakeDefinition definition, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> PublishAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision, CancellationToken cancellationToken = default);
    Task<Result<IntakeTemplateDetail>> CreateDraftAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision, CancellationToken cancellationToken = default);
}
