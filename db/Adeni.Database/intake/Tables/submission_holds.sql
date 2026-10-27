CREATE TABLE [intake].[submission_holds] (
 [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
 [TenantId] UNIQUEIDENTIFIER NOT NULL,
 [SubmissionId] UNIQUEIDENTIFIER NOT NULL,
 [PlacedAt] DATETIMEOFFSET(7) NOT NULL,
 [PlacedBy] NVARCHAR(128) NOT NULL,
 [Category] NVARCHAR(40) NOT NULL,
 [ReleasedAt] DATETIMEOFFSET(7) NULL,
 [ReleasedBy] NVARCHAR(128) NULL,
 CONSTRAINT [FK_intake_hold_submission] FOREIGN KEY ([TenantId], [SubmissionId]) REFERENCES [intake].[form_submissions] ([TenantId], [Id]),
 CONSTRAINT [CK_intake_hold_category] CHECK ([Category] IN ('legalProceeding', 'regulatory', 'otherLegalRequirement')),
 CONSTRAINT [CK_intake_hold_release] CHECK (([ReleasedAt] IS NULL AND [ReleasedBy] IS NULL) OR ([ReleasedAt] >= [PlacedAt] AND [ReleasedBy] IS NOT NULL))
);
GO
CREATE UNIQUE INDEX [IX_intake_hold_active] ON [intake].[submission_holds] ([TenantId], [SubmissionId]) WHERE [ReleasedAt] IS NULL;
