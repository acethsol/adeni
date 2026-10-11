CREATE TABLE [booking].[queue_tickets] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [ServiceOfferingId]  UNIQUEIDENTIFIER   NULL,
    [StaffMemberId]      UNIQUEIDENTIFIER   NULL,
    [CustomerId]         UNIQUEIDENTIFIER   NULL,
    [GuestName]          NVARCHAR (200)     NOT NULL,
    [GuestPhone]         NVARCHAR (40)      NOT NULL,
    [Status]             INT                NOT NULL,
    [PartySize]          INT                DEFAULT ((1)) NOT NULL,
    [Notes]              NVARCHAR (500)     NULL,
    [PublicToken]        NVARCHAR (64)      NOT NULL,
    [BookingId]          UNIQUEIDENTIFIER   NULL,
    [JoinedAt]           DATETIMEOFFSET (7) NOT NULL,
    [CalledAt]           DATETIMEOFFSET (7) NULL,
    [StartedAt]          DATETIMEOFFSET (7) NULL,
    [CompletedAt]        DATETIMEOFFSET (7) NULL,
    [NoShowAt]           DATETIMEOFFSET (7) NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]          DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_queue_tickets] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_queue_tickets_PublicToken]
    ON [booking].[queue_tickets]([PublicToken] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_queue_tickets_TenantId_Status_JoinedAt]
    ON [booking].[queue_tickets]([TenantId] ASC, [Status] ASC, [JoinedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_queue_tickets_TenantId_StaffMemberId_Status]
    ON [booking].[queue_tickets]([TenantId] ASC, [StaffMemberId] ASC, [Status] ASC);


GO
