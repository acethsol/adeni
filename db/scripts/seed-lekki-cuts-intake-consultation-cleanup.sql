/*
  DEV ONLY — removes intake rows created by seed-lekki-cuts-intake-consultation.sql (marker seed-lekki-intake-v1).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @Marker NVARCHAR(64) = N'seed-lekki-intake-v1';
DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = N'lekki-cuts');
IF @TenantId IS NULL THROW 50001, 'Tenant lekki-cuts not found.', 1;

DECLARE @TemplateId UNIQUEIDENTIFIER =
    (SELECT TOP (1) v.TemplateId FROM intake.form_versions v WHERE v.TenantId = @TenantId AND v.Description = @Marker);
IF @TemplateId IS NULL
BEGIN
    PRINT N'No Lekki intake consultation seed to remove.';
    RETURN;
END;

BEGIN TRY
BEGIN TRANSACTION;

DECLARE @VersionIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @VersionIds SELECT Id FROM intake.form_versions WHERE TenantId = @TenantId AND TemplateId = @TemplateId;

DECLARE @AssignmentIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @AssignmentIds
SELECT a.Id FROM intake.booking_assignments a
INNER JOIN @VersionIds v ON v.Id = a.FormVersionId
WHERE a.TenantId = @TenantId;

DECLARE @SubmissionIds TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);
INSERT @SubmissionIds
SELECT s.Id FROM intake.form_submissions s
INNER JOIN @AssignmentIds a ON a.Id = s.AssignmentId
WHERE s.TenantId = @TenantId;

DELETE ac FROM intake.answer_choices ac
INNER JOIN intake.form_answers fa ON fa.Id = ac.AnswerId
WHERE fa.SubmissionId IN (SELECT Id FROM @SubmissionIds);

DELETE FROM intake.form_answers WHERE SubmissionId IN (SELECT Id FROM @SubmissionIds);
DELETE FROM intake.submission_holds WHERE SubmissionId IN (SELECT Id FROM @SubmissionIds);
DELETE FROM intake.form_submissions WHERE Id IN (SELECT Id FROM @SubmissionIds);
DELETE FROM intake.booking_assignments WHERE Id IN (SELECT Id FROM @AssignmentIds);

DELETE FROM intake.service_form_links WHERE TenantId = @TenantId AND TemplateId = @TemplateId;

DELETE FROM intake.field_choices WHERE TenantId = @TenantId AND VersionId IN (SELECT Id FROM @VersionIds);
DELETE FROM intake.form_fields WHERE TenantId = @TenantId AND VersionId IN (SELECT Id FROM @VersionIds);
DELETE FROM intake.form_sections WHERE TenantId = @TenantId AND VersionId IN (SELECT Id FROM @VersionIds);
DELETE FROM intake.form_versions WHERE TenantId = @TenantId AND TemplateId = @TemplateId;
DELETE FROM intake.form_templates WHERE TenantId = @TenantId AND Id = @TemplateId;

COMMIT TRANSACTION;
PRINT N'Lekki intake consultation seed removed.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
