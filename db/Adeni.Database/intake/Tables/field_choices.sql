CREATE TABLE [intake].[field_choices] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [FieldId] UNIQUEIDENTIFIER NOT NULL,
    [SortOrder] INT NOT NULL,
    [Label] NVARCHAR(500) NOT NULL,
    CONSTRAINT [PK_field_choices] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_field_choices_0] UNIQUE ([TenantId], [VersionId], [FieldId], [Id]),
    CONSTRAINT [FK_field_choices_0] FOREIGN KEY ([TenantId], [VersionId], [FieldId]) REFERENCES [intake].[form_fields] ([TenantId], [VersionId], [Id]),
    CONSTRAINT [CK_field_choices_Values] CHECK ([SortOrder] >= 0)
);
