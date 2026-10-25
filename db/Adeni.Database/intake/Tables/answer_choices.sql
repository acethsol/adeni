CREATE TABLE [intake].[answer_choices] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [AnswerId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [FieldId] UNIQUEIDENTIFIER NOT NULL,
    [ChoiceId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_answer_choices] PRIMARY KEY ([TenantId], [AnswerId], [ChoiceId]),
    CONSTRAINT [FK_answer_choices_0] FOREIGN KEY ([TenantId], [AnswerId], [VersionId], [FieldId]) REFERENCES [intake].[form_answers] ([TenantId], [Id], [VersionId], [FieldId]),
    CONSTRAINT [FK_answer_choices_1] FOREIGN KEY ([TenantId], [VersionId], [FieldId], [ChoiceId]) REFERENCES [intake].[field_choices] ([TenantId], [VersionId], [FieldId], [Id])
);
