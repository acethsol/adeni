namespace Adeni.Infrastructure.Intake;

using System.Globalization;
using System.Text.Json;
using Adeni.Application.Abstractions;
using Adeni.Application.Auth;
using Adeni.Application.Common;
using Adeni.Application.Intake;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeSubmissionService(AdeniDbContext db, IIntakeBookingQuery bookings, IPortalPermissionService permissions,
    IIntakeManagementAccessQuery management, IAuditLogWriter audit, ICorrelationContext correlation, TimeProvider clock) : IIntakeSubmissionService
{
    private bool Open(IntakeBookingFacts b) => b.Status is 0 or 1 && clock.GetUtcNow() < b.StartAt;
    private async Task AuditAsync(string actor, string action, Guid id, CancellationToken ct) =>
        await audit.WriteAsync(new AuditEntry(Guid.NewGuid(), actor, action, "intake", id.ToString(), correlation.CorrelationId, clock.GetUtcNow()), ct);
    private Task<BookingIntakeAssignment?> AssignmentAsync(IntakeBookingFacts b, Guid assignmentId, CancellationToken ct) =>
        db.Set<BookingIntakeAssignment>().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == b.TenantId && x.BookingId == b.Id && x.Id == assignmentId, ct);
    private Task<IntakeFormSubmission?> SubmissionAsync(BookingIntakeAssignment a, CancellationToken ct) => db.Set<IntakeFormSubmission>().AsNoTracking()
        .SingleOrDefaultAsync(x => x.TenantId == a.TenantId && x.AssignmentId == a.Id, ct);
    private async Task<IntakeAssignmentStatus> StatusAsync(IntakeBookingFacts b, BookingIntakeAssignment a, CancellationToken ct)
    {
        var s = await SubmissionAsync(a, ct);
        return new(a.Id, a.FormVersionId, s?.PurgedAt is not null ? "purged" : s is not null ? "completed" : Open(b) ? "pending" : "closedUncompleted",
            s?.SubmittedAt, s?.PurgedAt, s is null && Open(b));
    }
    private async Task<IReadOnlyList<IntakeAssignmentStatus>> StatusesAsync(IntakeBookingFacts b, CancellationToken ct)
    {
        var rows = await db.Set<BookingIntakeAssignment>().AsNoTracking().Where(x => x.TenantId == b.TenantId && x.BookingId == b.Id).OrderBy(x => x.AssignedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        var result = new List<IntakeAssignmentStatus>();
        foreach (var row in rows) result.Add(await StatusAsync(b, row, ct));
        return result;
    }
    public async Task<Result<IReadOnlyList<IntakeAssignmentStatus>>> CustomerStatusesAsync(string customerSub, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetOwnedAsync(customerSub, bookingId, cancellationToken);
        return booking is null ? Result.Failure<IReadOnlyList<IntakeAssignmentStatus>>(Error.NotFound("Booking"))
            : Result.Success(await StatusesAsync(booking, cancellationToken));
    }
    public async Task<Result<IntakeSubmissionView>> CustomerReadAsync(string customerSub, Guid bookingId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetOwnedAsync(customerSub, bookingId, cancellationToken);
        if (booking is null) return Missing();
        var assignment = await AssignmentAsync(booking, assignmentId, cancellationToken);
        if (assignment is null) return Missing();
        var result = await ViewAsync(booking, assignment, cancellationToken);
        await AuditAsync(customerSub, "intake.customer_read", assignmentId, cancellationToken);
        return Result.Success(result);
    }
    private async Task<IReadOnlyList<IntakeAnswerInput>> AnswersAsync(IntakeFormSubmission s, CancellationToken ct)
    {
        var answers = await db.Set<IntakeFormAnswer>().AsNoTracking().Where(x => x.TenantId == s.TenantId && x.SubmissionId == s.Id).ToArrayAsync(ct);
        var ids = answers.Select(x => x.Id).ToArray();
        var choices = await db.Set<IntakeAnswerChoice>().AsNoTracking().Where(x => x.TenantId == s.TenantId && ids.Contains(x.AnswerId)).ToArrayAsync(ct);
        var fields = await db.Set<IntakeFormField>().AsNoTracking().Where(x => x.TenantId == s.TenantId && x.VersionId == s.FormVersionId).ToDictionaryAsync(x => x.Id, ct);
        return answers.Select(x => new IntakeAnswerInput(x.FieldId, x.TextValue, x.BooleanValue, x.DateValue?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            fields[x.FieldId].FieldType is IntakeFieldType.SingleChoice or IntakeFieldType.MultipleChoice
                ? choices.Where(c => c.AnswerId == x.Id).Select(c => c.ChoiceId).ToArray() : null)).ToArray();
    }
    private async Task<IntakeSubmissionView> ViewAsync(IntakeBookingFacts b, BookingIntakeAssignment a, CancellationToken ct)
    {
        var submission = await SubmissionAsync(a, ct);
        var held = submission is not null && await db.Set<IntakeSubmissionHold>().AnyAsync(x => x.TenantId == a.TenantId && x.SubmissionId == submission.Id && x.ReleasedAt == null, ct);
        var readable = submission is not null && submission.PurgedAt is null
            && (held || (submission.PrivacyDeletionRequestedAt is null && !IntakeRetentionPolicy.CompletedExpired(b.StartAt, clock.GetUtcNow())));
        return new(await StatusAsync(b, a, ct), await IntakeDefinitions.ReadAsync(db, a.TenantId, a.FormVersionId, ct),
            readable ? await AnswersAsync(submission!, ct) : null);
    }
    private static string Canonical(IReadOnlyList<IntakeAnswerInput> answers) => JsonSerializer.Serialize(answers.OrderBy(x => x.FieldId)
        .Select(x => x with { Choices = x.Choices?.OrderBy(c => c).ToArray() }));
    public async Task<Result<IntakeAssignmentStatus>> SubmitAsync(string customerSub, Guid bookingId, Guid assignmentId, string key, SubmitIntakeRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = IdempotencyKeyNormalizer.Normalize(key);
        if (normalized is null || request is null || request.Answers is null || JsonSerializer.SerializeToUtf8Bytes(request.Answers).Length > 65536)
            return Result.Failure<IntakeAssignmentStatus>(new Error("intake.validation"));
        var booking = await bookings.GetOwnedAsync(customerSub, bookingId, cancellationToken);
        if (booking is null) return Result.Failure<IntakeAssignmentStatus>(Error.NotFound("Booking"));
        var assignment = await AssignmentAsync(booking, assignmentId, cancellationToken);
        if (assignment is null) return Result.Failure<IntakeAssignmentStatus>(Error.NotFound("Intake form"));
        if (request.VersionId != assignment.FormVersionId) return Result.Failure<IntakeAssignmentStatus>(new Error("intake.validation"));
        var definition = await IntakeDefinitions.ReadAsync(db, assignment.TenantId, assignment.FormVersionId, cancellationToken);
        if (IntakeValidation.Answers(definition, request.Answers).IsFailure) return Result.Failure<IntakeAssignmentStatus>(new Error("intake.validation"));
        var existing = await SubmissionAsync(assignment, cancellationToken);
        if (existing is not null) return await ReplayAsync(existing, booking, assignment, normalized, request.Answers, cancellationToken);
        if (!Open(booking)) return Result.Failure<IntakeAssignmentStatus>(new Error("intake.submission_closed"));
        var submission = new IntakeFormSubmission { Id = Guid.NewGuid(), TenantId = assignment.TenantId, AssignmentId = assignment.Id,
            FormVersionId = assignment.FormVersionId, SubmittedAt = clock.GetUtcNow(), IdempotencyKey = normalized };
        db.Add(submission);
        foreach (var answer in request.Answers)
        {
            var row = new IntakeFormAnswer { Id = Guid.NewGuid(), TenantId = assignment.TenantId, SubmissionId = submission.Id,
                VersionId = assignment.FormVersionId, FieldId = answer.FieldId, TextValue = answer.Text, BooleanValue = answer.Boolean,
                DateValue = answer.Date is null ? null : DateOnly.ParseExact(answer.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture) };
            db.Add(row);
            foreach (var choice in answer.Choices ?? []) db.Add(new IntakeAnswerChoice { TenantId = assignment.TenantId, AnswerId = row.Id,
                VersionId = row.VersionId, FieldId = row.FieldId, ChoiceId = choice });
        }
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            existing = await SubmissionAsync(assignment, cancellationToken);
            return existing is null ? Result.Failure<IntakeAssignmentStatus>(new Error("intake.conflict"))
                : await ReplayAsync(existing, booking, assignment, normalized, request.Answers, cancellationToken);
        }
        await AuditAsync(customerSub, "intake.submitted", submission.Id, cancellationToken);
        return Result.Success(await StatusAsync(booking, assignment, cancellationToken));
    }
    private async Task<Result<IntakeAssignmentStatus>> ReplayAsync(IntakeFormSubmission existing, IntakeBookingFacts b, BookingIntakeAssignment a, string key, IReadOnlyList<IntakeAnswerInput> answers, CancellationToken ct)
    {
        if (existing.PurgedAt is not null || existing.IdempotencyKey != key || Canonical(await AnswersAsync(existing, ct)) != Canonical(answers))
            return Result.Failure<IntakeAssignmentStatus>(new Error("intake.conflict"));
        return Result.Success(await StatusAsync(b, a, ct));
    }
    private async Task<bool> CanReadStatusAsync(Guid tenantId, string actor, IntakeBookingFacts b, CancellationToken ct)
    {
        var resolved = await permissions.ResolveAsync(actor, ct);
        if (resolved.IsFailure || resolved.Value!.TenantId != tenantId) return false;
        var access = resolved.Value;
        return access.Permissions.Contains(PortalPermissions.Bookings)
            || (access.Permissions.Contains(PortalPermissions.BookingsSelf) && access.StaffMemberId is not null && access.StaffMemberId == b.StaffMemberId);
    }
    public async Task<Result<IReadOnlyList<IntakeAssignmentStatus>>> StaffStatusesAsync(Guid tenantId, string actor, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var b = await bookings.GetAsync(tenantId, bookingId, cancellationToken);
        if (b is null) return Result.Failure<IReadOnlyList<IntakeAssignmentStatus>>(Error.NotFound("Booking"));
        if (!await CanReadStatusAsync(tenantId, actor, b, cancellationToken)) return Result.Failure<IReadOnlyList<IntakeAssignmentStatus>>(ErrorCodes.PermissionDeniedError());
        return Result.Success(await StatusesAsync(b, cancellationToken));
    }
    public async Task<Result<IntakeSubmissionView>> ReviewAsync(Guid tenantId, string actor, Guid bookingId, Guid assignmentId, CancellationToken cancellationToken = default)
    {
        if (!await management.CanManageAsync(tenantId, actor, cancellationToken)) return Result.Failure<IntakeSubmissionView>(ErrorCodes.PermissionDeniedError());
        var b = await bookings.GetAsync(tenantId, bookingId, cancellationToken);
        if (b is null) return Missing();
        var resolved = await permissions.ResolveAsync(actor, cancellationToken);
        if (resolved.IsFailure || resolved.Value!.TenantId != tenantId || !resolved.Value.Permissions.Contains(PortalPermissions.Bookings))
            return Result.Failure<IntakeSubmissionView>(ErrorCodes.PermissionDeniedError());
        var a = await AssignmentAsync(b, assignmentId, cancellationToken);
        if (a is null) return Missing();
        var view = await ViewAsync(b, a, cancellationToken);
        await AuditAsync(actor, "intake.reviewed", assignmentId, cancellationToken);
        return Result.Success(view);
    }
    public async Task<IReadOnlyList<IntakeSubmissionView>> PrivacyExportAsync(Guid customerId, string adminActor, CancellationToken cancellationToken = default)
    {
        var ids = await bookings.ListForCustomerAsync(customerId, cancellationToken);
        var assignments = await db.Set<BookingIntakeAssignment>().AsNoTracking().Where(x => ids.Contains(x.BookingId)).ToArrayAsync(cancellationToken);
        var result = new List<IntakeSubmissionView>();
        foreach (var assignment in assignments)
        {
            var b = await bookings.GetAsync(assignment.TenantId, assignment.BookingId, cancellationToken);
            if (b is not null) result.Add(await ViewAsync(b, assignment, cancellationToken));
        }
        await AuditAsync(adminActor, "intake.privacy_exported", customerId, cancellationToken);
        return result;
    }
    private static Result<IntakeSubmissionView> Missing() => Result.Failure<IntakeSubmissionView>(Error.NotFound("Intake form"));
}
