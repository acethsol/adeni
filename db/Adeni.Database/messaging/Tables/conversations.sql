CREATE TABLE [messaging].[conversations] (
    [Id]                     UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]               UNIQUEIDENTIFIER   NOT NULL,
    [CustomerId]             UNIQUEIDENTIFIER   NOT NULL,
    [ContextType]            INT                NOT NULL,
    [ContextId]              UNIQUEIDENTIFIER   NULL,
    [Status]                 INT                NOT NULL,
    [CreatedAt]              DATETIMEOFFSET (7) NOT NULL,
    [LastMessageAt]          DATETIMEOFFSET (7) NOT NULL,
    [CreatedByParticipantId] UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [PK_conversations] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_conversations_TenantId_LastMessageAt]
    ON [messaging].[conversations]([TenantId] ASC, [LastMessageAt] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_conversations_TenantId_CustomerId_Context]
    ON [messaging].[conversations]([TenantId] ASC, [CustomerId] ASC, [ContextType] ASC, [ContextId] ASC);


GO
