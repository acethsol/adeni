namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record IntakeRequirement(Guid TemplateId, Guid VersionId);
public interface IBookingIntakeAssignmentWriter
{
    Task<Result<IReadOnlyList<IntakeRequirement>>> PrepareAsync(Guid tenantId, IReadOnlyList<Guid> serviceIds, CancellationToken cancellationToken);
    void Stage(Guid tenantId, Guid bookingId, IReadOnlyList<IntakeRequirement> requirements, DateTimeOffset assignedAt);
}
