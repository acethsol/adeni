/*
  Sprint 38A — rename SMS preference column (idempotent).
  Run manually on existing databases before/after dacpac publish if column still uses legacy name.
*/
IF COL_LENGTH(N'notifications.tenant_notification_preferences', N'SmsWhatsAppReminderEnabled') IS NOT NULL
   AND COL_LENGTH(N'notifications.tenant_notification_preferences', N'SmsRemindersEnabled') IS NULL
BEGIN
    EXEC sp_rename N'notifications.tenant_notification_preferences.SmsWhatsAppReminderEnabled', N'SmsRemindersEnabled', N'COLUMN';
END
GO
