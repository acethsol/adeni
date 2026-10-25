CREATE TABLE [intake].[form_submissions] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [AssignmentId] UNIQUEIDENTIFIER NOT NULL,
    [FormVersionId] UNIQUEIDENTIFIER NOT NULL,
    [SubmittedAt] DATETIMEOFFSET(7) NOT NULL,
    [IdempotencyKey] NVARCHAR(128) NOT NULL,
    [PurgedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_form_submissions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_submissions_0] UNIQUE ([TenantId], [Id], [FormVersionId]),
    CONSTRAINT [AK_form_submissions_1] UNIQUE ([TenantId], [AssignmentId]),
    CONSTRAINT [FK_form_submissions_0] FOREIGN KEY ([TenantId], [AssignmentId], [FormVersionId]) REFERENCES [intake].[booking_assignments] ([TenantId], [Id], [FormVersionId])
);
