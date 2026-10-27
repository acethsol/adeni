CREATE TABLE [intake].[form_answers] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [SubmissionId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [FieldId] UNIQUEIDENTIFIER NOT NULL,
    [TextValue] NVARCHAR(4000) NULL,
    [BooleanValue] BIT NULL,
    [DateValue] DATE NULL,
    CONSTRAINT [PK_form_answers] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_answers_0] UNIQUE ([TenantId], [Id], [VersionId], [FieldId]),
    CONSTRAINT [AK_form_answers_1] UNIQUE ([TenantId], [SubmissionId], [FieldId]),
    CONSTRAINT [FK_form_answers_0] FOREIGN KEY ([TenantId], [SubmissionId], [VersionId]) REFERENCES [intake].[form_submissions] ([TenantId], [Id], [FormVersionId]),
    CONSTRAINT [FK_form_answers_1] FOREIGN KEY ([TenantId], [VersionId], [FieldId]) REFERENCES [intake].[form_fields] ([TenantId], [VersionId], [Id]),
    CONSTRAINT [CK_form_answers_Values] CHECK ((CASE WHEN [TextValue] IS NULL THEN 0 ELSE 1 END + CASE WHEN [BooleanValue] IS NULL THEN 0 ELSE 1 END + CASE WHEN [DateValue] IS NULL THEN 0 ELSE 1 END) <= 1)
);
