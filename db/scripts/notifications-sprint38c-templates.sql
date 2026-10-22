-- Sprint 38c: notification locale + template overrides

IF COL_LENGTH('notifications.tenant_notification_preferences', 'DefaultNotificationLocale') IS NULL
BEGIN
    ALTER TABLE [notifications].[tenant_notification_preferences]
        ADD [DefaultNotificationLocale] NVARCHAR(8) NOT NULL
            CONSTRAINT [DF_tenant_notification_preferences_DefaultNotificationLocale_38c] DEFAULT ('en');
END
GO

IF COL_LENGTH('notifications.tenant_notification_preferences', 'NotificationLanguageMode') IS NULL
BEGIN
    ALTER TABLE [notifications].[tenant_notification_preferences]
        ADD [NotificationLanguageMode] NVARCHAR(16) NOT NULL
            CONSTRAINT [DF_tenant_notification_preferences_NotificationLanguageMode_38c] DEFAULT ('single');
END
GO

IF COL_LENGTH('notifications.tenant_notification_preferences', 'SecondaryNotificationLocale') IS NULL
BEGIN
    ALTER TABLE [notifications].[tenant_notification_preferences]
        ADD [SecondaryNotificationLocale] NVARCHAR(8) NULL;
END
GO

IF COL_LENGTH('identity.customers', 'PreferredLocale') IS NULL
BEGIN
    ALTER TABLE [identity].[customers]
        ADD [PreferredLocale] NVARCHAR(8) NULL;
END
GO

IF OBJECT_ID(N'[notifications].[tenant_notification_template_overrides]', N'U') IS NULL
BEGIN
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

    CREATE UNIQUE NONCLUSTERED INDEX [IX_tenant_notification_template_overrides_tenant_template_locale]
        ON [notifications].[tenant_notification_template_overrides]([TenantId] ASC, [TemplateId] ASC, [Locale] ASC);
END
GO
