CREATE TABLE [messaging].[messages] (
    [Id]             UNIQUEIDENTIFIER   NOT NULL,
    [ThreadId]       UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]       UNIQUEIDENTIFIER   NOT NULL,
    [SenderType]     INT                NOT NULL,
    [SenderAuth0Sub] NVARCHAR (128)     NOT NULL,
    [Body]           NVARCHAR (4000)    NOT NULL,
    [CreatedAt]      DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_messages] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_messages_message_threads_ThreadId] FOREIGN KEY ([ThreadId]) REFERENCES [messaging].[message_threads] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_messages_ThreadId_CreatedAt]
    ON [messaging].[messages]([ThreadId] ASC, [CreatedAt] ASC);


GO

