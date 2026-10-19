CREATE TABLE [messaging].[message_attachments] (
    [Id]          UNIQUEIDENTIFIER   NOT NULL,
    [MessageId]   UNIQUEIDENTIFIER   NOT NULL,
    [BlobKey]     NVARCHAR (512)     NOT NULL,
    [ContentType] NVARCHAR (128)     NOT NULL,
    [FileName]    NVARCHAR (256)     NOT NULL,
    [FileSize]    BIGINT             NOT NULL,
    [CreatedAt]   DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_message_attachments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_message_attachments_conversation_messages_MessageId]
        FOREIGN KEY ([MessageId]) REFERENCES [messaging].[conversation_messages] ([Id]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_message_attachments_MessageId]
    ON [messaging].[message_attachments]([MessageId] ASC);


GO
