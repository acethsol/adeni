/*
  DEV ONLY — published intake template matching the Body Sculpt wellness consultation PDF.
  Idempotent marker: fixed template Id a1000001-0001-4001-8001-000000000001

  Run: scripts/apply-wellness-consultation-form-seed.ps1
       sqlcmd -I -C -v TenantSlug="ikoyi-massage-studio" -i db/scripts/seed-wellness-consultation-form.sql

  Requires DevelopmentDataSeeder (API once in Development). Binds to active massage services on the tenant.
  JSON source of truth for Portal/API import: db/scripts/wellness-consultation-form.definition.json
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @TenantSlug NVARCHAR(64) = N'$(TenantSlug)';
DECLARE @TemplateName NVARCHAR(200) = N'Body Sculpt wellness consultation';
DECLARE @Now DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();

DECLARE @TenantId UNIQUEIDENTIFIER =
    (SELECT TOP (1) TenantId FROM tenancy.business_locations WHERE Slug = @TenantSlug);
IF @TenantId IS NULL
    THROW 50001, 'Tenant slug not found. Start the API in Development once, or pass -TenantSlug.', 1;

DECLARE @TemplateId UNIQUEIDENTIFIER = 'a1000001-0001-4001-8001-000000000001';

IF EXISTS (SELECT 1 FROM intake.form_templates WHERE TenantId = @TenantId AND Id = @TemplateId)
BEGIN
    PRINT 'Wellness consultation intake seed already applied for ' + @TenantSlug + '. Skipping.';
    RETURN;
END;
DECLARE @VersionId  UNIQUEIDENTIFIER = 'a1000001-0001-4001-8001-000000000002';

BEGIN TRAN;

INSERT intake.form_templates (Id, TenantId, Name, IsActive, CurrentPublishedVersionId, CreatedAt, UpdatedAt)
VALUES (@TemplateId, @TenantId, @TemplateName, 1, NULL, @Now, @Now);

INSERT intake.form_versions (Id, TenantId, TemplateId, VersionNumber, Title, Description, Instructions, CreatedAt, PublishedAt)
VALUES (
    @VersionId,
    @TenantId,
    @TemplateId,
    1,
    N'Body Sculpt wellness consultation',
    N'Non-clinical intake for massage and spa appointments. Do not enter diagnoses or prescriptions.',
    N'Your comfort and well-being are our priority. Please complete this form before your appointment so we can personalize your experience. All information is confidential.',
    @Now,
    @Now);

INSERT intake.form_sections (Id, TenantId, VersionId, SortOrder, Title, Description) VALUES
('a1000b01-0001-4001-8001-000000000001', @TenantId, @VersionId, 0, N'Personal information', N'Contact and emergency details for your visit.'),
('a1000b01-0001-4001-8001-000000000002', @TenantId, @VersionId, 1, N'Health history', N'Wellness screening only — not a medical record.'),
('a1000b01-0001-4001-8001-000000000003', @TenantId, @VersionId, 2, N'Treatment preferences', N'Help us tailor pressure, focus areas, and products.'),
('a1000b01-0001-4001-8001-000000000004', @TenantId, @VersionId, 3, N'Consent', NULL);

INSERT intake.form_fields (Id, TenantId, VersionId, SortOrder, FieldType, Label, HelpText, IsRequired, SectionId) VALUES
('a1000101-0001-4001-8001-000000000001', @TenantId, @VersionId, 0, 0, N'Full name', N'As you would like to be addressed during your visit.', 1, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000002', @TenantId, @VersionId, 1, 0, N'Phone number', NULL, 1, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000003', @TenantId, @VersionId, 2, 0, N'Email', NULL, 1, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000004', @TenantId, @VersionId, 3, 5, N'Date of birth', N'Optional. Used only to tailor treatment safely.', 0, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000005', @TenantId, @VersionId, 4, 0, N'Occupation', NULL, 0, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000006', @TenantId, @VersionId, 5, 0, N'Emergency contact name and phone', N'Someone we can reach if needed during your visit.', 1, 'a1000b01-0001-4001-8001-000000000001'),
('a1000101-0001-4001-8001-000000000007', @TenantId, @VersionId, 6, 3, N'Health history — tick any conditions that apply', N'Leave blank if none apply. Wellness screening only, not a medical record.', 0, 'a1000b01-0001-4001-8001-000000000002'),
('a1000101-0001-4001-8001-000000000008', @TenantId, @VersionId, 7, 0, N'Other health conditions not listed', NULL, 0, 'a1000b01-0001-4001-8001-000000000002'),
('a1000101-0001-4001-8001-000000000009', @TenantId, @VersionId, 8, 1, N'Current injuries, physical limitations, or areas of concern', NULL, 0, 'a1000b01-0001-4001-8001-000000000002'),
('a1000101-0001-4001-8001-00000000000a', @TenantId, @VersionId, 9, 2, N'What type of massage or treatment are you booking today?', NULL, 1, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-00000000000b', @TenantId, @VersionId, 10, 0, N'If other treatment, please specify', NULL, 0, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-00000000000c', @TenantId, @VersionId, 11, 3, N'Main goal for this treatment (select all that apply)', NULL, 1, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-00000000000d', @TenantId, @VersionId, 12, 0, N'Other treatment goal', NULL, 0, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-00000000000e', @TenantId, @VersionId, 13, 2, N'Preferred pressure', NULL, 1, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-00000000000f', @TenantId, @VersionId, 14, 1, N'Areas you would like us to focus on', NULL, 0, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-000000000010', @TenantId, @VersionId, 15, 1, N'Areas we should avoid', NULL, 0, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-000000000011', @TenantId, @VersionId, 16, 1, N'Allergies (oils, lotions, fragrances, foods, etc.)', N'Enter "None" if you have no known allergies.', 1, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-000000000012', @TenantId, @VersionId, 17, 1, N'Anything else we should know to make your experience better?', NULL, 0, 'a1000b01-0001-4001-8001-000000000003'),
('a1000101-0001-4001-8001-000000000013', @TenantId, @VersionId, 18, 6,
 N'I confirm that the information provided is accurate to the best of my knowledge. I understand that this information will help my therapist provide a safe and effective treatment.',
 N'This is an acknowledgement, not a legal e-signature. Submit once before your appointment.', 1, 'a1000b01-0001-4001-8001-000000000004');

INSERT intake.field_choices (Id, TenantId, VersionId, FieldId, SortOrder, Label) VALUES
('a1000201-0001-4001-8001-000000000001', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 0, N'High blood pressure'),
('a1000201-0001-4001-8001-000000000002', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 1, N'Heart condition'),
('a1000201-0001-4001-8001-000000000003', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 2, N'Diabetes'),
('a1000201-0001-4001-8001-000000000004', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 3, N'Asthma / respiratory issues'),
('a1000201-0001-4001-8001-000000000005', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 4, N'Epilepsy / seizures'),
('a1000201-0001-4001-8001-000000000006', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 5, N'Back / neck pain'),
('a1000201-0001-4001-8001-000000000007', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 6, N'Arthritis'),
('a1000201-0001-4001-8001-000000000008', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 7, N'Pregnancy'),
('a1000201-0001-4001-8001-000000000009', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 8, N'Recent surgery'),
('a1000201-0001-4001-8001-00000000000a', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 9, N'Varicose veins'),
('a1000201-0001-4001-8001-00000000000b', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 10, N'Skin conditions'),
('a1000201-0001-4001-8001-00000000000c', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 11, N'Blood clots / DVT'),
('a1000201-0001-4001-8001-00000000000d', @TenantId, @VersionId, 'a1000101-0001-4001-8001-000000000007', 12, N'Cancer (current or past)'),
('a1000301-0001-4001-8001-000000000001', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000a', 0, N'Relaxation massage'),
('a1000301-0001-4001-8001-000000000002', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000a', 1, N'Deep tissue massage'),
('a1000301-0001-4001-8001-000000000003', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000a', 2, N'Aromatherapy massage'),
('a1000301-0001-4001-8001-000000000004', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000a', 3, N'Other (describe below)'),
('a1000401-0001-4001-8001-000000000001', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000c', 0, N'Relaxation / stress relief'),
('a1000401-0001-4001-8001-000000000002', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000c', 1, N'Pain relief'),
('a1000401-0001-4001-8001-000000000003', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000c', 2, N'Muscle recovery'),
('a1000401-0001-4001-8001-000000000004', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000c', 3, N'Improved circulation'),
('a1000501-0001-4001-8001-000000000001', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000e', 0, N'Light'),
('a1000501-0001-4001-8001-000000000002', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000e', 1, N'Medium'),
('a1000501-0001-4001-8001-000000000003', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000e', 2, N'Firm'),
('a1000501-0001-4001-8001-000000000004', @TenantId, @VersionId, 'a1000101-0001-4001-8001-00000000000e', 3, N'Very firm');

UPDATE intake.form_templates
SET CurrentPublishedVersionId = @VersionId, UpdatedAt = @Now
WHERE TenantId = @TenantId AND Id = @TemplateId;

INSERT intake.service_form_links (TenantId, ServiceOfferingId, TemplateId, UpdatedAt)
SELECT so.TenantId, so.Id, @TemplateId, @Now
FROM booking.service_offerings so
WHERE so.TenantId = @TenantId
  AND so.IsActive = 1
  AND so.IsAddOn = 0
  AND so.Name LIKE N'%Massage%'
  AND NOT EXISTS (
      SELECT 1 FROM intake.service_form_links l
      WHERE l.TenantId = so.TenantId AND l.ServiceOfferingId = so.Id);

COMMIT TRAN;

PRINT 'Wellness consultation intake seeded for tenant slug ' + @TenantSlug + ' (template ' + CAST(@TemplateId AS NVARCHAR(36)) + ').';
