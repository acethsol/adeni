namespace Adeni.Infrastructure.Persistence;
using Adeni.Domain.Intake;
using Microsoft.EntityFrameworkCore;
public sealed partial class AdeniDbContext
{
    private void ConfigureIntake(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntakeFormTemplate>(e =>
        {
            e.ToTable("form_templates", "intake");
            e.HasOne<Adeni.Domain.Tenancy.Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasOne<IntakeFormVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.Id, x.CurrentPublishedVersionId }).HasPrincipalKey(x => new { x.TenantId, x.TemplateId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFormVersion>(e =>
        {
            e.ToTable("form_versions", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Instructions).HasMaxLength(3000);
            e.HasAlternateKey(x => new { x.TenantId, x.TemplateId, x.Id });
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasAlternateKey(x => new { x.TenantId, x.TemplateId, x.VersionNumber });
            e.HasIndex(x => new { x.TenantId, x.TemplateId }).IsUnique().HasFilter("[PublishedAt] IS NULL");
            e.HasOne<IntakeFormTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.TemplateId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFormSection>(e =>
        {
            e.ToTable("form_sections", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasAlternateKey(x => new { x.TenantId, x.VersionId, x.Id });
            e.HasOne<IntakeFormVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFormField>(e =>
        {
            e.ToTable("form_fields", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.Label).HasMaxLength(500);
            e.Property(x => x.HelpText).HasMaxLength(1000);
            e.HasAlternateKey(x => new { x.TenantId, x.VersionId, x.Id });
            e.HasOne<IntakeFormVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IntakeFormSection>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId, x.SectionId }).HasPrincipalKey(x => new { x.TenantId, x.VersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFieldChoice>(e =>
        {
            e.ToTable("field_choices", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.Label).HasMaxLength(500);
            e.HasAlternateKey(x => new { x.TenantId, x.VersionId, x.FieldId, x.Id });
            e.HasOne<IntakeFormField>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId, x.FieldId }).HasPrincipalKey(x => new { x.TenantId, x.VersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ServiceIntakeForm>(e =>
        {
            e.ToTable("service_form_links", "intake");
            e.HasKey(x => new { x.TenantId, x.ServiceOfferingId });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne<IntakeFormTemplate>().WithMany().HasForeignKey(x => new { x.TenantId, x.TemplateId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Adeni.Domain.Booking.ServiceOffering>().WithMany().HasForeignKey(x => new { x.TenantId, x.ServiceOfferingId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BookingIntakeAssignment>(e =>
        {
            e.ToTable("booking_assignments", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasAlternateKey(x => new { x.TenantId, x.Id, x.FormVersionId });
            e.HasAlternateKey(x => new { x.TenantId, x.BookingId, x.TemplateId });
            e.HasOne<IntakeFormVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.TemplateId, x.FormVersionId }).HasPrincipalKey(x => new { x.TenantId, x.TemplateId, x.Id }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Adeni.Domain.Booking.BookingRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.BookingId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFormSubmission>(e =>
        {
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable("form_submissions", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128);
            e.HasAlternateKey(x => new { x.TenantId, x.Id, x.FormVersionId });
            e.HasAlternateKey(x => new { x.TenantId, x.AssignmentId });
            e.HasOne<BookingIntakeAssignment>().WithMany().HasForeignKey(x => new { x.TenantId, x.AssignmentId, x.FormVersionId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.FormVersionId }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeFormAnswer>(e =>
        {
            e.ToTable("form_answers", "intake");
            e.HasKey(x => new { x.Id });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.TextValue).HasMaxLength(4000);
            e.HasAlternateKey(x => new { x.TenantId, x.Id, x.VersionId, x.FieldId });
            e.HasAlternateKey(x => new { x.TenantId, x.SubmissionId, x.FieldId });
            e.HasOne<IntakeFormSubmission>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubmissionId, x.VersionId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.FormVersionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IntakeFormField>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId, x.FieldId }).HasPrincipalKey(x => new { x.TenantId, x.VersionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeAnswerChoice>(e =>
        {
            e.ToTable("answer_choices", "intake");
            e.HasKey(x => new { x.TenantId, x.AnswerId, x.ChoiceId });
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.HasOne<IntakeFormAnswer>().WithMany().HasForeignKey(x => new { x.TenantId, x.AnswerId, x.VersionId, x.FieldId }).HasPrincipalKey(x => new { x.TenantId, x.Id, x.VersionId, x.FieldId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<IntakeFieldChoice>().WithMany().HasForeignKey(x => new { x.TenantId, x.VersionId, x.FieldId, x.ChoiceId }).HasPrincipalKey(x => new { x.TenantId, x.VersionId, x.FieldId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IntakeSubmissionHold>(e =>
        {
            e.ToTable("submission_holds", "intake");
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => ActiveTenantFilterId == null || x.TenantId == ActiveTenantFilterId);
            e.Property(x => x.PlacedBy).HasMaxLength(128);
            e.Property(x => x.ReleasedBy).HasMaxLength(128);
            e.Property(x => x.Category).HasMaxLength(40);
            e.HasIndex(x => new { x.TenantId, x.SubmissionId }).IsUnique().HasFilter("[ReleasedAt] IS NULL");
            e.HasOne<IntakeFormSubmission>().WithMany().HasForeignKey(x => new { x.TenantId, x.SubmissionId }).HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
