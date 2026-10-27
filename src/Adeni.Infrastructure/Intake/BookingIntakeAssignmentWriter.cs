namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingIntakeAssignmentWriter(AdeniDbContext db) : IBookingIntakeAssignmentWriter
{
    public async Task<Result<IReadOnlyList<IntakeRequirement>>> PrepareAsync(Guid tenantId, IReadOnlyList<Guid> serviceIds, CancellationToken cancellationToken)
    {
        var templateIds = await db.Set<ServiceIntakeForm>().AsNoTracking().Where(x => x.TenantId == tenantId && serviceIds.Contains(x.ServiceOfferingId))
            .Select(x => x.TemplateId).Distinct().ToArrayAsync(cancellationToken);
        var templates = await db.Set<IntakeFormTemplate>().AsNoTracking().Where(x => x.TenantId == tenantId && templateIds.Contains(x.Id)).ToArrayAsync(cancellationToken);
        if (templates.Length != templateIds.Length || templates.Any(x => !x.IsActive || x.CurrentPublishedVersionId is null))
            return Result.Failure<IReadOnlyList<IntakeRequirement>>(Error.Conflict("An intake requirement is unavailable."));
        var versions = templates.Select(x => x.CurrentPublishedVersionId!.Value).ToArray();
        var valid = await db.Set<IntakeFormVersion>().AsNoTracking().CountAsync(x => x.TenantId == tenantId && versions.Contains(x.Id) && x.PublishedAt != null, cancellationToken);
        if (valid != versions.Length) return Result.Failure<IReadOnlyList<IntakeRequirement>>(Error.Conflict("An intake requirement is unpublished."));
        return Result.Success<IReadOnlyList<IntakeRequirement>>(templates.Select(x => new IntakeRequirement(x.Id, x.CurrentPublishedVersionId!.Value)).ToArray());
    }

    public void Stage(Guid tenantId, Guid bookingId, IReadOnlyList<IntakeRequirement> requirements, DateTimeOffset assignedAt)
    {
        foreach (var requirement in requirements)
            db.Add(new BookingIntakeAssignment { Id = Guid.NewGuid(), TenantId = tenantId, BookingId = bookingId,
                TemplateId = requirement.TemplateId, FormVersionId = requirement.VersionId, AssignedAt = assignedAt });
    }
}
