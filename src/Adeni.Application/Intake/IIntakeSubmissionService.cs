namespace Adeni.Application.Intake;

using Adeni.Domain.Common;
using System.Text.Json.Serialization;

public sealed record IntakeAssignmentStatus(Guid Id, Guid VersionId, string State, DateTimeOffset? SubmittedAt, DateTimeOffset? PurgedAt, bool CanSubmit);
public sealed record IntakeSubmissionView(IntakeAssignmentStatus Assignment, IntakeDefinition Definition, IReadOnlyList<IntakeAnswerInput>? Answers)
{
    public override string ToString() => $"IntakeSubmissionView {{ AssignmentId = {Assignment.Id} }}";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitIntakeRequest(Guid VersionId, IReadOnlyList<IntakeAnswerInput> Answers)
{
    public override string ToString() => $"SubmitIntakeRequest {{ VersionId = {VersionId} }}";
}

public interface IIntakeSubmissionService
{
    Task<Result<IReadOnlyList<IntakeAssignmentStatus>>> CustomerStatusesAsync(string customerSub, Guid bookingId, CancellationToken cancellationToken = default);
    Task<Result<IntakeSubmissionView>> CustomerReadAsync(string customerSub, Guid bookingId, Guid assignmentId, CancellationToken cancellationToken = default);
    Task<Result<IntakeAssignmentStatus>> SubmitAsync(string customerSub, Guid bookingId, Guid assignmentId, string key, SubmitIntakeRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<IntakeAssignmentStatus>>> StaffStatusesAsync(Guid tenantId, string actor, Guid bookingId, CancellationToken cancellationToken = default);
    Task<Result<IntakeSubmissionView>> ReviewAsync(Guid tenantId, string actor, Guid bookingId, Guid assignmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IntakeSubmissionView>> PrivacyExportAsync(Guid customerId, string adminActor, CancellationToken cancellationToken = default);
}
