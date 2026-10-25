namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Common;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeRetentionService(AdeniDbContext db, IIntakeBookingQuery bookings, TimeProvider clock) : IIntakeRetentionService
{
    public async Task<IntakePurgeResult> PurgeTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var purged = 0; var removed = 0; var held = 0; var offset = 0;
        while (true)
        {
            var batch = await db.Set<BookingIntakeAssignment>().Where(x => x.TenantId == tenantId
                && !db.Set<IntakeFormSubmission>().Any(s => s.TenantId == tenantId && s.AssignmentId == x.Id && s.PurgedAt != null))
                .OrderBy(x => x.AssignedAt).ThenBy(x => x.Id).Skip(offset).Take(100).ToArrayAsync(cancellationToken);
            if (batch.Length == 0) break;
            var disappeared = 0;
            foreach (var assignment in batch)
            {
                var submission = await db.Set<IntakeFormSubmission>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.AssignmentId == assignment.Id, cancellationToken);
                var booking = await bookings.GetAsync(tenantId, assignment.BookingId, cancellationToken);
                // Missing source linkage is not evidence that a retention deadline has passed.
                if (booking is null) continue;
                if (submission is null)
                {
                    if (IntakeRetentionPolicy.PendingExpired(booking, now)) { db.Remove(assignment); removed++; disappeared++; }
                    continue;
                }
                if (submission.PrivacyDeletionRequestedAt is null && !IntakeRetentionPolicy.CompletedExpired(booking.StartAt, now)) continue;
                if (await HeldAsync(tenantId, submission.Id, cancellationToken)) { held++; continue; }
                await RemoveContentAsync(submission, now, cancellationToken);
                purged++; disappeared++;
            }
            // Submission rowversion serializes purge against hold placement/release. A race rolls back the entire SQL save.
            await db.SaveChangesAsync(cancellationToken);
            offset += batch.Length - disappeared;
        }
        return new(purged, removed, held);
    }

    public async Task StagePrivacyPurgeAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var ids = await bookings.ListForCustomerAsync(customerId, cancellationToken);
        var assignments = await db.Set<BookingIntakeAssignment>().Where(x => ids.Contains(x.BookingId)).ToArrayAsync(cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var assignment in assignments)
        {
            var submission = await db.Set<IntakeFormSubmission>().SingleOrDefaultAsync(x => x.TenantId == assignment.TenantId && x.AssignmentId == assignment.Id, cancellationToken);
            if (submission is null) { db.Remove(assignment); continue; }
            if (submission.PurgedAt is not null) continue;
            submission.PrivacyDeletionRequestedAt ??= now;
            submission.RetentionUpdatedAt = now;
            if (!await HeldAsync(assignment.TenantId, submission.Id, cancellationToken)) await RemoveContentAsync(submission, now, cancellationToken);
        }
        // The existing privacy owner saves identity redaction and staged Intake deletion together.
    }

    public async Task<Result> PlaceHoldAsync(Guid tenantId, Guid submissionId, string actor, string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128 || category is not ("legalProceeding" or "regulatory" or "otherLegalRequirement"))
            return Result.Failure(Error.Validation("Invalid hold actor or category."));
        var submission = await db.Set<IntakeFormSubmission>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == submissionId, cancellationToken);
        if (submission is null) return Result.Failure(Error.NotFound("Intake submission"));
        if (submission.PurgedAt is not null) return Result.Failure(Error.Conflict("Intake content has already been purged."));
        if (await HeldAsync(tenantId, submissionId, cancellationToken)) return Result.Success();
        var now = clock.GetUtcNow();
        submission.RetentionUpdatedAt = now;
        db.Entry(submission).Property(x => x.RetentionUpdatedAt).IsModified = true;
        db.Add(new IntakeSubmissionHold { Id = Guid.NewGuid(), TenantId = tenantId, SubmissionId = submissionId, PlacedAt = now, PlacedBy = actor, Category = category });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReleaseHoldAsync(Guid tenantId, Guid submissionId, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 128) return Result.Failure(Error.Validation("Invalid hold actor."));
        var submission = await db.Set<IntakeFormSubmission>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == submissionId, cancellationToken);
        if (submission is null) return Result.Failure(Error.NotFound("Intake submission"));
        var hold = await db.Set<IntakeSubmissionHold>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId && x.ReleasedAt == null, cancellationToken);
        if (hold is null) return Result.Success();
        var now = clock.GetUtcNow();
        hold.ReleasedAt = now; hold.ReleasedBy = actor; submission.RetentionUpdatedAt = now;
        db.Entry(submission).Property(x => x.RetentionUpdatedAt).IsModified = true;
        var assignment = await db.Set<BookingIntakeAssignment>().SingleAsync(x => x.TenantId == tenantId && x.Id == submission.AssignmentId, cancellationToken);
        var booking = await bookings.GetAsync(tenantId, assignment.BookingId, cancellationToken);
        if (submission.PrivacyDeletionRequestedAt is not null || (booking is not null && IntakeRetentionPolicy.CompletedExpired(booking.StartAt, now)))
            await RemoveContentAsync(submission, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<bool> HeldAsync(Guid tenantId, Guid submissionId, CancellationToken ct) => db.Set<IntakeSubmissionHold>()
        .AnyAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId && x.ReleasedAt == null, ct);

    private async Task RemoveContentAsync(IntakeFormSubmission submission, DateTimeOffset now, CancellationToken ct)
    {
        if (submission.PurgedAt is not null) return;
        var answers = await db.Set<IntakeFormAnswer>().Where(x => x.TenantId == submission.TenantId && x.SubmissionId == submission.Id).ToArrayAsync(ct);
        var ids = answers.Select(x => x.Id).ToArray();
        var choices = await db.Set<IntakeAnswerChoice>().Where(x => x.TenantId == submission.TenantId && ids.Contains(x.AnswerId)).ToArrayAsync(ct);
        db.RemoveRange(choices); db.RemoveRange(answers);
        submission.PurgedAt = now; submission.RetentionUpdatedAt = now; submission.IdempotencyKey = string.Empty;
        // Minimal receipt/assignment headers preserve completion state and prevent resurrection on replay.
    }
}
