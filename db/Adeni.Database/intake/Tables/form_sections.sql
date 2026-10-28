CREATE TABLE [intake].[form_sections] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [VersionId] UNIQUEIDENTIFIER NOT NULL,
    [SortOrder] INT NOT NULL,
    [Title] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(1000) NULL,
    CONSTRAINT [PK_form_sections] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_form_sections_0] UNIQUE ([TenantId], [VersionId], [Id]),
    CONSTRAINT [FK_form_sections_0] FOREIGN KEY ([TenantId], [VersionId]) REFERENCES [intake].[form_versions] ([TenantId], [Id]),
    CONSTRAINT [CK_form_sections_Values] CHECK ([SortOrder] >= 0 AND [SortOrder] < 10)
);
