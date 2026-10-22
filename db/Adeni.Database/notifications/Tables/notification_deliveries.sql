CREATE TABLE [notifications].[notification_deliveries] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [DedupeKey]     NVARCHAR (512)     NOT NULL,
    [TenantId]      UNIQUEIDENTIFIER   NULL,
    [Channel]       NVARCHAR (32)      NOT NULL,
    [RecipientKey]  NVARCHAR (512)     NOT NULL,
    [Status]        INT                NOT NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    [CompletedAt]   DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_notification_deliveries] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [UX_notification_deliveries_DedupeKey]
    ON [notifications].[notification_deliveries]([DedupeKey] ASC);


GO

