namespace Adeni.Domain.Identity;

using Adeni.Domain.Tenancy;

public sealed class VerifiedPresenceSession : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid BusinessUserId { get; set; }

    public Guid? StaffMemberId { get; set; }

    public Guid BusinessLocationId { get; set; }

    public DateTimeOffset VerifiedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? LastExtendedAt { get; set; }

    public string VerificationMethod { get; set; } = VerifiedPresenceVerificationMethods.ManualVerify;

    public int? DistanceFromLocationMeters { get; set; }

    public int? ReportedAccuracyMeters { get; set; }

    public DateTimeOffset? InvalidatedAt { get; set; }

    public string? InvalidationReason { get; set; }

    public Guid? SourceAttendanceEventId { get; set; }

    public BusinessUser? BusinessUser { get; set; }
}
