/*
  DEV ONLY — published wellness consultation intake for Lekki Cuts (tenant slug lekki-cuts).
  Mapped from scanned "Body Sculpt Wellness Consultation Form" (demo / local).

  Idempotent marker: intake.form_versions.Description = N'seed-lekki-intake-v1'
  Undo: db/scripts/seed-lekki-cuts-intake-consultation-cleanup.sql
  Run:  scripts/apply-lekki-cuts-demo-seed.ps1  (includes this script after main demo seed)

  Requires DevelopmentDataSeeder (tenant, services: Barbering, Braids, Locs, …).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @TenantSlug NVARCHAR(64) = N'lekki-cuts';
DECLARE @Marker     NVARCHAR(64) = N'seed-lekki-intake-v1';
DECLARE @Now        DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = @TenantSlug);
IF @TenantId IS NULL
    THROW 50001, 'Tenant for slug lekki-cuts not found. Start the API in Development once.', 1;

IF EXISTS (SELECT 1 FROM intake.form_versions WHERE TenantId = @TenantId AND Description = @Marker)
BEGIN
    PRINT N'Lekki intake consultation seed already applied (' + @Marker + N').';
    RETURN;
END;

-- Fixed ids so re-run and cleanup stay predictable (dev-only).
DECLARE @TemplateId UNIQUEIDENTIFIER = '6f4e8c2a-9d1b-4f7e-a6c5-000000000101';
DECLARE @VersionId  UNIQUEIDENTIFIER = '6f4e8c2a-9d1b-4f7e-a6c5-000000000102';

BEGIN TRY
BEGIN TRANSACTION;

INSERT INTO intake.form_templates (Id, TenantId, Name, IsActive, CurrentPublishedVersionId, CreatedAt, UpdatedAt)
VALUES (@TemplateId, @TenantId, N'Wellness consultation', 1, NULL, @Now, @Now);

INSERT INTO intake.form_versions (Id, TenantId, TemplateId, VersionNumber, Title, Description, Instructions, CreatedAt, PublishedAt)
VALUES (
    @VersionId,
    @TenantId,
    @TemplateId,
    1,
    N'Body Sculpt Wellness Consultation',
    @Marker,
    N'Your comfort and well-being are our priority. Kindly take a few minutes to complete this form so we can personalize your experience. All information is confidential.',
    @Now,
    @Now
);

UPDATE intake.form_templates
SET CurrentPublishedVersionId = @VersionId, UpdatedAt = @Now
WHERE Id = @TemplateId AND TenantId = @TenantId;

DECLARE @Fields TABLE (
    SortOrder INT NOT NULL,
    FieldId UNIQUEIDENTIFIER NOT NULL,
    FieldType INT NOT NULL,
    Label NVARCHAR(500) NOT NULL,
    HelpText NVARCHAR(1000) NULL,
    IsRequired BIT NOT NULL
);

INSERT INTO @Fields (SortOrder, FieldId, FieldType, Label, HelpText, IsRequired) VALUES
(0,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000201', 0, N'Full name', N'Section 1 — Personal information', 1),
(1,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000202', 5, N'Date (today)', NULL, 1),
(2,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000203', 0, N'Phone number', NULL, 1),
(3,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000204', 0, N'Email', NULL, 1),
(4,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000205', 5, N'Date of birth', NULL, 0),
(5,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000206', 0, N'Occupation', NULL, 0),
(6,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000207', 0, N'Emergency contact & phone', NULL, 0),
(7,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 3, N'Health conditions', N'Section 2 — Please tick any conditions that apply to you.', 0),
(8,  '6f4e8c2a-9d1b-4f7e-a6c5-000000000209', 0, N'Other health condition (specify)', NULL, 0),
(9,  '6f4e8c2a-9d1b-4f7e-a6c5-00000000020a', 1, N'Current injuries, physical limitations or areas of concern', NULL, 0),
(10, '6f4e8c2a-9d1b-4f7e-a6c5-00000000020b', 3, N'Treatment you are booking today', N'Section 3 — Wellness & massage preferences', 0),
(11, '6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 3, N'Main goal for this treatment', NULL, 0),
(12, '6f4e8c2a-9d1b-4f7e-a6c5-00000000020d', 2, N'Preferred pressure', NULL, 0),
(13, '6f4e8c2a-9d1b-4f7e-a6c5-00000000020e', 1, N'Areas you would like us to focus on', NULL, 0),
(14, '6f4e8c2a-9d1b-4f7e-a6c5-00000000020f', 1, N'Areas we should avoid', NULL, 0),
(15, '6f4e8c2a-9d1b-4f7e-a6c5-000000000210', 1, N'Allergies (oils, lotions, fragrances, foods, etc.)', N'Section 4 — Additional information', 0),
(16, '6f4e8c2a-9d1b-4f7e-a6c5-000000000211', 1, N'Anything else we should know', NULL, 0),
(17, '6f4e8c2a-9d1b-4f7e-a6c5-000000000212', 6,
    N'I confirm that the information provided is accurate to the best of my knowledge. I understand that this information will help my therapist provide a safe and effective treatment.',
    N'Consent — signature on paper form is not collected in Adeni intake v1.', 1);

INSERT INTO intake.form_fields (Id, TenantId, VersionId, SortOrder, FieldType, Label, HelpText, IsRequired)
SELECT FieldId, @TenantId, @VersionId, SortOrder, FieldType, Label, HelpText, IsRequired FROM @Fields;

DECLARE @Choices TABLE (FieldId UNIQUEIDENTIFIER NOT NULL, SortOrder INT NOT NULL, ChoiceId UNIQUEIDENTIFIER NOT NULL, Label NVARCHAR(500) NOT NULL);

INSERT INTO @Choices (FieldId, SortOrder, ChoiceId, Label) VALUES
-- Health conditions (field …208)
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 0, '6f4e8c2a-9d1b-4f7e-a6c5-000000000301', N'High blood pressure'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 1, '6f4e8c2a-9d1b-4f7e-a6c5-000000000302', N'Heart condition'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 2, '6f4e8c2a-9d1b-4f7e-a6c5-000000000303', N'Diabetes'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 3, '6f4e8c2a-9d1b-4f7e-a6c5-000000000304', N'Asthma / respiratory issues'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 4, '6f4e8c2a-9d1b-4f7e-a6c5-000000000305', N'Epilepsy / seizures'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 5, '6f4e8c2a-9d1b-4f7e-a6c5-000000000306', N'Back / neck pain'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 6, '6f4e8c2a-9d1b-4f7e-a6c5-000000000307', N'Arthritis'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 7, '6f4e8c2a-9d1b-4f7e-a6c5-000000000308', N'Pregnancy'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 8, '6f4e8c2a-9d1b-4f7e-a6c5-000000000309', N'Recent surgery'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 9, '6f4e8c2a-9d1b-4f7e-a6c5-00000000030a', N'Varicose veins'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 10, '6f4e8c2a-9d1b-4f7e-a6c5-00000000030b', N'Skin conditions'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 11, '6f4e8c2a-9d1b-4f7e-a6c5-00000000030c', N'Blood clots / DVT'),
('6f4e8c2a-9d1b-4f7e-a6c5-000000000208', 12, '6f4e8c2a-9d1b-4f7e-a6c5-00000000030d', N'Cancer'),
-- Treatment booking (field …20b)
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020b', 0, '6f4e8c2a-9d1b-4f7e-a6c5-000000000401', N'Relaxation massage'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020b', 1, '6f4e8c2a-9d1b-4f7e-a6c5-000000000402', N'Deep tissue massage'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020b', 2, '6f4e8c2a-9d1b-4f7e-a6c5-000000000403', N'Aromatherapy massage'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020b', 3, '6f4e8c2a-9d1b-4f7e-a6c5-000000000404', N'Other treatment'),
-- Main goal (field …20c)
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 0, '6f4e8c2a-9d1b-4f7e-a6c5-000000000501', N'Relaxation / stress relief'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 1, '6f4e8c2a-9d1b-4f7e-a6c5-000000000502', N'Pain relief'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 2, '6f4e8c2a-9d1b-4f7e-a6c5-000000000503', N'Muscle recovery'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 3, '6f4e8c2a-9d1b-4f7e-a6c5-000000000504', N'Improved circulation'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020c', 4, '6f4e8c2a-9d1b-4f7e-a6c5-000000000505', N'Other goal'),
-- Preferred pressure (field …20d, single choice)
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020d', 0, '6f4e8c2a-9d1b-4f7e-a6c5-000000000601', N'Light'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020d', 1, '6f4e8c2a-9d1b-4f7e-a6c5-000000000602', N'Medium'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020d', 2, '6f4e8c2a-9d1b-4f7e-a6c5-000000000603', N'Firm'),
('6f4e8c2a-9d1b-4f7e-a6c5-00000000020d', 3, '6f4e8c2a-9d1b-4f7e-a6c5-000000000604', N'Very firm');

INSERT INTO intake.field_choices (Id, TenantId, VersionId, FieldId, SortOrder, Label)
SELECT ChoiceId, @TenantId, @VersionId, FieldId, SortOrder, Label FROM @Choices;

-- Bind to key hair services (by name; idempotent MERGE).
DECLARE @Services TABLE (ServiceOfferingId UNIQUEIDENTIFIER PRIMARY KEY, ServiceName NVARCHAR(200) NOT NULL);
INSERT INTO @Services (ServiceOfferingId, ServiceName)
SELECT s.Id, s.Name
FROM booking.service_offerings s
WHERE s.TenantId = @TenantId
  AND s.IsActive = 1
  AND s.Name IN (N'Barbering', N'Braids', N'Locs');

IF NOT EXISTS (SELECT 1 FROM @Services)
    THROW 50002, 'No Barbering/Braids/Locs services for Lekki Cuts. Run the API in Development once.', 1;

MERGE intake.service_form_links AS target
USING (
    SELECT @TenantId AS TenantId, ServiceOfferingId, @TemplateId AS TemplateId, @Now AS UpdatedAt
    FROM @Services
) AS source
ON target.TenantId = source.TenantId AND target.ServiceOfferingId = source.ServiceOfferingId
WHEN MATCHED THEN
    UPDATE SET TemplateId = source.TemplateId, UpdatedAt = source.UpdatedAt
WHEN NOT MATCHED THEN
    INSERT (TenantId, ServiceOfferingId, TemplateId, UpdatedAt)
    VALUES (source.TenantId, source.ServiceOfferingId, source.TemplateId, source.UpdatedAt);

COMMIT TRANSACTION;

DECLARE @LinkCount INT = (SELECT COUNT(*) FROM intake.service_form_links WHERE TenantId = @TenantId AND TemplateId = @TemplateId);
PRINT CONCAT(N'Lekki intake consultation seeded. Template=', CONVERT(NVARCHAR(36), @TemplateId), N', service links=', @LinkCount);
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
