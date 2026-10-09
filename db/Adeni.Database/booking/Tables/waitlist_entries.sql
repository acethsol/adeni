CREATE TABLE [booking].[waitlist_entries] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER   NOT NULL,
    [CustomerId]        UNIQUEIDENTIFIER   NOT NULL,
    [PreferredFrom]     DATETIMEOFFSET (7) NULL,
    [PreferredTo]       DATETIMEOFFSET (7) NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [NotifiedAt]        DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_waitlist_entries] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_waitlist_entries_TenantId_ServiceOfferingId_NotifiedAt]
    ON [booking].[waitlist_entries]([TenantId] ASC, [ServiceOfferingId] ASC, [NotifiedAt] ASC);


GO

