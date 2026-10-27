namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

public sealed class IntakeServiceBindingService(AdeniDbContext db, IIntakeManagementAccessQuery access, IIntakeServiceQuery services,
    IAuditLogWriter audit, ICorrelationContext correlation) : IIntakeServiceBinding
{
    public async Task<Result<IntakeServiceBinding>> GetAsync(Guid tenantId, string actor, Guid serviceId, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, actor, cancellationToken)) return Result.Failure<IntakeServiceBinding>(ErrorCodes.PermissionDeniedError());
        if (!await services.ExistsAsync(tenantId, serviceId, cancellationToken)) return Result.Failure<IntakeServiceBinding>(Error.NotFound("Service"));
        var binding = await db.Set<ServiceIntakeForm>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ServiceOfferingId == serviceId, cancellationToken);
        return Result.Success(new IntakeServiceBinding(binding?.TemplateId, binding is null ? "" : Convert.ToBase64String(binding.RowVersion)));
    }

    public async Task<Result<IntakeServiceBinding>> SetAsync(Guid tenantId, string actor, Guid serviceId, Guid? templateId, string revision, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(tenantId, actor, serviceId, cancellationToken);
        if (current.IsFailure) return current;
        if (revision != current.Value!.Revision) return Result.Failure<IntakeServiceBinding>(new Error("intake.conflict"));
        if (templateId is Guid id && !await db.Set<IntakeFormTemplate>().AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.IsActive
                && x.CurrentPublishedVersionId != null, cancellationToken))
            return Result.Failure<IntakeServiceBinding>(Error.Validation("An active published template is required."));
        if (templateId is Guid selected)
        {
            var template = await db.Set<IntakeFormTemplate>().SingleAsync(x => x.TenantId == tenantId && x.Id == selected, cancellationToken);
            if (!template.IsActive || template.CurrentPublishedVersionId is null) return Result.Failure<IntakeServiceBinding>(new Error("intake.conflict"));
            template.UpdatedAt = DateTimeOffset.UtcNow;
        }
        var row = await db.Set<ServiceIntakeForm>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.ServiceOfferingId == serviceId, cancellationToken);
        if (revision != (row is null ? "" : Convert.ToBase64String(row.RowVersion))) return Result.Failure<IntakeServiceBinding>(new Error("intake.conflict"));
        if (templateId is null) { if (row is not null) db.Remove(row); }
        else
        {
            if (row is null) { row = new() { TenantId = tenantId, ServiceOfferingId = serviceId }; db.Add(row); }
            row.TemplateId = templateId.Value; row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Result.Failure<IntakeServiceBinding>(new Error("intake.conflict")); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { db.ChangeTracker.Clear(); return Result.Failure<IntakeServiceBinding>(new Error("intake.conflict")); }
        await audit.WriteAsync(new AuditEntry(Guid.NewGuid(), actor, "intake.service_binding_changed", "service", serviceId.ToString(), correlation.CorrelationId, DateTimeOffset.UtcNow), cancellationToken);
        return await GetAsync(tenantId, actor, serviceId, cancellationToken);
    }
}
