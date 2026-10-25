namespace Adeni.Application.Intake;

using Adeni.Domain.Common;

public sealed record IntakeBookingFacts(Guid Id, Guid TenantId, Guid CustomerId, Guid? StaffMemberId,
    Guid? LocationId, DateTimeOffset StartAt, DateTimeOffset EndAt, int Status, DateTimeOffset UpdatedAt);

public interface IIntakeBookingQuery
{
    Task<IntakeBookingFacts?> GetAsync(Guid tenantId, Guid bookingId, CancellationToken cancellationToken);
    Task<IntakeBookingFacts?> GetOwnedAsync(string customerSub, Guid bookingId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);
}

public static class IntakeRetentionPolicy
{
    public static bool CompletedExpired(DateTimeOffset appointmentAt, DateTimeOffset now) => appointmentAt.AddMonths(24) <= now;
    public static DateTimeOffset? ClosedAt(IntakeBookingFacts booking, DateTimeOffset now)
    {
        if (booking.StartAt <= now) return booking.StartAt;
        if (booking.Status is 2 or 3 or 4 or 5 or 6)
            return booking.UpdatedAt == default || booking.UpdatedAt > now ? now : booking.UpdatedAt;
        return null;
    }
    public static bool PendingExpired(IntakeBookingFacts booking, DateTimeOffset now) =>
        ClosedAt(booking, now) is { } closed && closed.AddDays(90) <= now;
}

public sealed record IntakePurgeResult(int SubmissionsPurged, int AssignmentsRemoved, int Held);
public interface IIntakeRetentionService
{
    Task<IntakePurgeResult> PurgeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> PlaceHoldAsync(Guid tenantId, Guid submissionId, string actor, string category, CancellationToken cancellationToken = default);
    Task<Result> ReleaseHoldAsync(Guid tenantId, Guid submissionId, string actor, CancellationToken cancellationToken = default);
    Task StagePrivacyPurgeAsync(Guid customerId, CancellationToken cancellationToken = default);
}
