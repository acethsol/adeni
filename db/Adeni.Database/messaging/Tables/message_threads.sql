CREATE TABLE [messaging].[message_threads] (
    [Id]                  UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]            UNIQUEIDENTIFIER   NOT NULL,
    [CustomerId]          UNIQUEIDENTIFIER   NOT NULL,
    [BookingId]           UNIQUEIDENTIFIER   NULL,
    [Status]              INT                NOT NULL,
    [Subject]             NVARCHAR (200)     NULL,
    [LastMessageAt]       DATETIMEOFFSET (7) NOT NULL,
    [BusinessUnreadCount] INT                NOT NULL,
    [CustomerUnreadCount] INT                NOT NULL,
    [CreatedAt]           DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_message_threads] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_message_threads_TenantId_CustomerId_BookingId]
    ON [messaging].[message_threads]([TenantId] ASC, [CustomerId] ASC, [BookingId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_message_threads_TenantId_LastMessageAt]
    ON [messaging].[message_threads]([TenantId] ASC, [LastMessageAt] ASC);


GO

