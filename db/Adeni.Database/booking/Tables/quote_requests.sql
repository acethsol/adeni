CREATE TABLE [booking].[quote_requests] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [BusinessLocationId] UNIQUEIDENTIFIER  NULL,
    [CustomerId]        UNIQUEIDENTIFIER   NOT NULL,
    [Description]       NVARCHAR (2000)    NOT NULL,
    [ServiceAddress]    NVARCHAR (500)     NULL,
    [PhotoKeysJson]     NVARCHAR (2000)    NULL,
    [Status]            INT                NOT NULL,
    [QuotedAmount]      DECIMAL (12, 2)    NULL,
    [QuotedCurrency]    NVARCHAR (3)       NULL,
    [QuoteNotes]        NVARCHAR (2000)    NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER   NULL,
    [ProposedStartAt]   DATETIMEOFFSET (7) NULL,
    [ProposedEndAt]     DATETIMEOFFSET (7) NULL,
    [QuotedAt]          DATETIMEOFFSET (7) NULL,
    [ExpiresAt]         DATETIMEOFFSET (7) NULL,
    [BookingId]         UNIQUEIDENTIFIER   NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_quote_requests] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_quote_requests_business_locations_BusinessLocationId] FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_quote_requests_CustomerId_CreatedAt]
    ON [booking].[quote_requests]([CustomerId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_quote_requests_TenantId_CreatedAt]
    ON [booking].[quote_requests]([TenantId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_quote_requests_TenantId_BusinessLocationId_CreatedAt]
    ON [booking].[quote_requests]([TenantId] ASC, [BusinessLocationId] ASC, [CreatedAt] ASC);


GO

