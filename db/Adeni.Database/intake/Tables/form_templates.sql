CREATE TABLE [intake].[form_templates] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [IsActive] BIT NOT NULL,
    [CurrentPublishedVersionId] UNIQUEIDENTIFIER NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_form_templates] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_templates_0] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_form_templates_0] FOREIGN KEY ([TenantId], [Id], [CurrentPublishedVersionId]) REFERENCES [intake].[form_versions] ([TenantId], [TemplateId], [Id])
);
