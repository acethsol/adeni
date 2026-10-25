CREATE TABLE [intake].[form_fields] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [SortOrder] INT NOT NULL,
    [FieldType] INT NOT NULL,
    [Label] NVARCHAR(500) NOT NULL,
    [HelpText] NVARCHAR(1000) NULL,
    [IsRequired] BIT NOT NULL,
    CONSTRAINT [PK_form_fields] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_fields_0] UNIQUE ([TenantId], [VersionId], [Id]),
    CONSTRAINT [FK_form_fields_0] FOREIGN KEY ([TenantId], [VersionId]) REFERENCES [intake].[form_versions] ([TenantId], [Id]),
    CONSTRAINT [CK_form_fields_Values] CHECK ([FieldType] BETWEEN 0 AND 6 AND [SortOrder] >= 0)
);
