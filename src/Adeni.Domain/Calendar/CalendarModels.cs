namespace Adeni.Domain.Calendar;

using Adeni.Domain.Tenancy;

public sealed class CalendarConnection : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public string Provider { get; set; } = "";
    public string AccountHash { get; set; } = "";
    public string ProtectedCredentials { get; set; } = "";
    public DateTimeOffset TokenExpiresAt { get; set; }
    public bool WriteAccess { get; set; }
    public string Status { get; set; } = "connected";
    public Guid Generation { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextSyncAt { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public string? FailureCategory { get; set; }
    public int FailureCount { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CalendarSelection : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public string ExternalId { get; set; } = "";
    public string ExternalIdHash { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";
    public bool IsWriteTarget { get; set; }
}

/// <summary>One generation shared by publication and booking admission, including multi-provider sources.</summary>
public sealed class CalendarStaffState : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AdmissionStamp { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CalendarSnapshot : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}

public sealed class ExternalBusyBlock : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SnapshotId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}

public sealed class CalendarOAuthAttempt : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public string Provider { get; set; } = "";
    public string Actor { get; set; } = "";
    public string StateHash { get; set; } = "";
    public string CorrelationHash { get; set; } = "";
    public string ProtectedVerifier { get; set; } = "";
    public bool WriteAccess { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LaunchedAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CalendarConflict : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public Guid BookingId { get; set; }
    public string Category { get; set; } = "assigned_overlap";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public sealed class ExternalBookingEvent : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid BookingId { get; set; }
    public string TargetId { get; set; } = "";
    public string? ExternalId { get; set; }
    public string? AppliedFingerprint { get; set; }
    public string State { get; set; } = "pending";
    public byte[] RowVersion { get; set; } = [];
}
