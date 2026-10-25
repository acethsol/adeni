CREATE TABLE [intake].[form_versions] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [TemplateId] UNIQUEIDENTIFIER NOT NULL,
    [VersionNumber] INT NOT NULL,
    [Title] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(2000) NULL,
    [Instructions] NVARCHAR(3000) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [PublishedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_form_versions] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_versions_0] UNIQUE ([TenantId], [TemplateId], [Id]),
    CONSTRAINT [AK_form_versions_1] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [AK_form_versions_2] UNIQUE ([TenantId], [TemplateId], [VersionNumber]),
    CONSTRAINT [FK_form_versions_0] FOREIGN KEY ([TenantId], [TemplateId]) REFERENCES [intake].[form_templates] ([TenantId], [Id]),
    CONSTRAINT [CK_form_versions_Values] CHECK ([VersionNumber] > 0)
);
GO
CREATE UNIQUE INDEX [IX_form_versions_Draft] ON [intake].[form_versions] ([TenantId], [TemplateId]) WHERE [PublishedAt] IS NULL;
