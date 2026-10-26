-- Idempotent schema for verification UX gap closeout (Phase 1 + Phase 3 file keys).
IF COL_LENGTH('tenancy.tenants', 'VerificationRejectionReason') IS NULL
BEGIN
    ALTER TABLE [tenancy].[tenants] ADD [VerificationRejectionReason] NVARCHAR (2000) NULL;
END
GO

IF COL_LENGTH('tenancy.verification_documents', 'StorageFileKey') IS NULL
BEGIN
    ALTER TABLE [tenancy].[verification_documents] ADD [StorageFileKey] NVARCHAR (512) NULL;
END
GO
