CREATE TABLE [booking].[reviews] (
    [Id]           UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]     UNIQUEIDENTIFIER   NOT NULL,
    [BookingId]    UNIQUEIDENTIFIER   NOT NULL,
    [CustomerId]   UNIQUEIDENTIFIER   NOT NULL,
    [Rating]       TINYINT            NOT NULL,
    [Comment]      NVARCHAR (1000)    NOT NULL,
    [OwnerReply]   NVARCHAR (1000)    NULL,
    [OwnerReplyAt] DATETIMEOFFSET (7) NULL,
    [IsHidden]     BIT                NOT NULL,
    [CreatedAt]    DATETIMEOFFSET (7) NOT NULL,
    [HiddenAt]     DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_reviews] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_reviews_BookingId]
    ON [booking].[reviews]([BookingId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_reviews_TenantId_IsHidden_CreatedAt]
    ON [booking].[reviews]([TenantId] ASC, [IsHidden] ASC, [CreatedAt] ASC);


GO

