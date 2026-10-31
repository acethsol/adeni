namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class StaffAttendanceEvent : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid BusinessLocationId { get; set; }

    public Guid StaffMemberId { get; set; }

    public Guid BusinessUserId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public string LocationVerificationStatus { get; set; } = StaffLocationVerificationStatuses.NotRequired;

    public int? DistanceFromLocationMeters { get; set; }

    public int? ReportedAccuracyMeters { get; set; }

    public string VerificationMethod { get; set; } = StaffAttendanceVerificationMethods.None;

    public Guid? ManagerOverrideByBusinessUserId { get; set; }

    public string? ManagerOverrideReason { get; set; }

    public Guid? RelatedEventId { get; set; }

    public string? CorrectionReason { get; set; }

    public DateTimeOffset? ClientLocationCapturedAt { get; set; }

    public StaffMember? StaffMember { get; set; }

    public BusinessLocation? BusinessLocation { get; set; }
}
