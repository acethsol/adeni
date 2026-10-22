CREATE TABLE [notifications].[tenant_notification_template_overrides] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [TemplateId]       NVARCHAR (64)      NOT NULL,
    [Locale]           NVARCHAR (8)       NOT NULL,
    [SubjectOverride]  NVARCHAR (200)     NULL,
    [BodyOverride]     NVARCHAR (2000)    NULL,
    [UpdatedAt]        DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_tenant_notification_template_overrides] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_tenant_notification_template_overrides_tenant] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id])
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_notification_template_overrides_tenant_template_locale]
    ON [notifications].[tenant_notification_template_overrides]([TenantId] ASC, [TemplateId] ASC, [Locale] ASC);


GO
