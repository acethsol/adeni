CREATE TABLE [notifications].[tenant_notification_preferences] (
    [TenantId]                   UNIQUEIDENTIFIER   NOT NULL,
    [EmailEnabled]               BIT                NOT NULL,
    [PushEnabled]                BIT                NOT NULL,
    [SmsRemindersEnabled]        BIT                NOT NULL,
    [DefaultNotificationLocale]  NVARCHAR (8)       NOT NULL CONSTRAINT [DF_tenant_notification_preferences_DefaultNotificationLocale] DEFAULT ('en'),
    [NotificationLanguageMode]   NVARCHAR (16)      NOT NULL CONSTRAINT [DF_tenant_notification_preferences_NotificationLanguageMode] DEFAULT ('single'),
    [UpdatedAt]                  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_tenant_notification_preferences] PRIMARY KEY CLUSTERED ([TenantId] ASC)
);


GO

