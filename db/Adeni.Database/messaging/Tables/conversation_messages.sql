CREATE TABLE [messaging].[conversation_messages] (
    [Id]                   UNIQUEIDENTIFIER   NOT NULL,
    [ConversationId]       UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]             UNIQUEIDENTIFIER   NOT NULL,
    [SenderParticipantId]  UNIQUEIDENTIFIER   NOT NULL,
    [Type]                 INT                NOT NULL,
    [Body]                 NVARCHAR (4000)    NULL,
    [ClientMessageId]      NVARCHAR (64)      NULL,
    [CreatedAt]            DATETIMEOFFSET (7) NOT NULL,
    [DeliveredAt]          DATETIMEOFFSET (7) NULL,
    [DeletedAt]            DATETIMEOFFSET (7) NULL,
    [ModerationStatus]     INT                NOT NULL CONSTRAINT [DF_conversation_messages_ModerationStatus] DEFAULT ((0)),
    CONSTRAINT [PK_conversation_messages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_conversation_messages_conversations_ConversationId]
        FOREIGN KEY ([ConversationId]) REFERENCES [messaging].[conversations] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_conversation_messages_participants_SenderParticipantId]
        FOREIGN KEY ([SenderParticipantId]) REFERENCES [messaging].[conversation_participants] ([Id]) ON DELETE NO ACTION
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_conversation_messages_Conversation_ClientMessageId]
    ON [messaging].[conversation_messages]([ConversationId] ASC, [ClientMessageId] ASC)
    WHERE [ClientMessageId] IS NOT NULL;


GO
CREATE NONCLUSTERED INDEX [IX_conversation_messages_ConversationId_CreatedAt_Id]
    ON [messaging].[conversation_messages]([ConversationId] ASC, [CreatedAt] ASC, [Id] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_conversation_messages_TenantId_CreatedAt]
    ON [messaging].[conversation_messages]([TenantId] ASC, [CreatedAt] ASC);


GO
