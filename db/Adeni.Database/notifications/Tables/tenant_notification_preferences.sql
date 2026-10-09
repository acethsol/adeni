CREATE TABLE [notifications].[tenant_notification_preferences] (
    [TenantId]                   UNIQUEIDENTIFIER   NOT NULL,
    [EmailEnabled]               BIT                NOT NULL,
    [PushEnabled]                BIT                NOT NULL,
    [SmsWhatsAppReminderEnabled] BIT                NOT NULL,
    [UpdatedAt]                  DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_tenant_notification_preferences] PRIMARY KEY CLUSTERED ([TenantId] ASC)
);


GO

