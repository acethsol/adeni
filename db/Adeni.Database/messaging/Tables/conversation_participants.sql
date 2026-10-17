CREATE TABLE [messaging].[conversation_participants] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [ConversationId]     UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [ParticipantType]    INT                NOT NULL,
    [CustomerId]         UNIQUEIDENTIFIER   NULL,
    [BusinessUserId]     UNIQUEIDENTIFIER   NULL,
    [BotKey]             NVARCHAR (64)      NULL,
    [Auth0Sub]           NVARCHAR (128)     NULL,
    [LastReadMessageId]  UNIQUEIDENTIFIER   NULL,
    [LastReadAt]         DATETIMEOFFSET (7) NULL,
    [IsMuted]            BIT                NOT NULL CONSTRAINT [DF_conversation_participants_IsMuted] DEFAULT ((0)),
    [JoinedAt]           DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_conversation_participants] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_conversation_participants_conversations_ConversationId]
        FOREIGN KEY ([ConversationId]) REFERENCES [messaging].[conversations] ([Id]) ON DELETE CASCADE
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_conversation_participants_Conversation_Customer]
    ON [messaging].[conversation_participants]([ConversationId] ASC, [CustomerId] ASC)
    WHERE [CustomerId] IS NOT NULL;


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_conversation_participants_Conversation_BusinessUser]
    ON [messaging].[conversation_participants]([ConversationId] ASC, [BusinessUserId] ASC)
    WHERE [BusinessUserId] IS NOT NULL;


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_conversation_participants_Conversation_BotKey]
    ON [messaging].[conversation_participants]([ConversationId] ASC, [BotKey] ASC)
    WHERE [BotKey] IS NOT NULL;


GO
CREATE NONCLUSTERED INDEX [IX_conversation_participants_ConversationId]
    ON [messaging].[conversation_participants]([ConversationId] ASC);


GO
