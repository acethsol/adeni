namespace Adeni.Domain.Intake;

using Adeni.Domain.Tenancy;

public enum IntakeFieldType { ShortText, LongText, SingleChoice, MultipleChoice, YesNo, Date, Acknowledgement }

public sealed class IntakeFormTemplate : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid? CurrentPublishedVersionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class IntakeFormVersion : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

public sealed class IntakeFormSection : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VersionId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class IntakeFormField : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VersionId { get; set; }
    public int SortOrder { get; set; }
    public IntakeFieldType FieldType { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; }
    public Guid? SectionId { get; set; }
}

public sealed class IntakeFieldChoice : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid VersionId { get; set; }
    public Guid FieldId { get; set; }
    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class ServiceIntakeForm : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ServiceOfferingId { get; set; }
    public Guid TemplateId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class BookingIntakeAssignment : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BookingId { get; set; }
    public Guid TemplateId { get; set; }
    public Guid FormVersionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
}

public sealed class IntakeFormSubmission : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid FormVersionId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset? PurgedAt { get; set; }
    public DateTimeOffset? RetentionUpdatedAt { get; set; }
    public DateTimeOffset? PrivacyDeletionRequestedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class IntakeSubmissionHold : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public string PlacedBy { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleasedBy { get; set; }
}

public sealed class IntakeFormAnswer : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid VersionId { get; set; }
    public Guid FieldId { get; set; }
    public string? TextValue { get; set; }
    public bool? BooleanValue { get; set; }
    public DateOnly? DateValue { get; set; }
}

public sealed class IntakeAnswerChoice : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid AnswerId { get; set; }
    public Guid VersionId { get; set; }
    public Guid FieldId { get; set; }
    public Guid ChoiceId { get; set; }
}
