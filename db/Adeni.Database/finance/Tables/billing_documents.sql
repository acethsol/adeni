CREATE TABLE [finance].[billing_documents] (
    [Id]                     UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]               UNIQUEIDENTIFIER   NOT NULL,
    [Kind]                   INT                NOT NULL,
    [Status]                 INT                NOT NULL,
    [DocumentNumber]         NVARCHAR (64)      NOT NULL,
    [BookingId]              UNIQUEIDENTIFIER   NULL,
    [PaymentIntentId]        UNIQUEIDENTIFIER   NULL,
    [BusinessNameSnapshot]   NVARCHAR (200)     NOT NULL,
    [CustomerNameSnapshot]   NVARCHAR (200)     NULL,
    [CustomerEmailSnapshot]  NVARCHAR (320)     NULL,
    [Currency]               NVARCHAR (3)       NOT NULL,
    [Subtotal]               DECIMAL (12, 2)    NOT NULL,
    [TaxTotal]               DECIMAL (12, 2)    NULL,
    [Total]                  DECIMAL (12, 2)    NOT NULL,
    [MethodLabel]            NVARCHAR (100)     NULL,
    [PublicToken]            NVARCHAR (64)      NOT NULL,
    [VoidReason]             NVARCHAR (500)     NULL,
    [IssuedAt]               DATETIMEOFFSET (7) NULL,
    [CreatedAt]              DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]              DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_billing_documents] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_billing_documents_PublicToken]
    ON [finance].[billing_documents]([PublicToken] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_billing_documents_TenantId_DocumentNumber]
    ON [finance].[billing_documents]([TenantId] ASC, [DocumentNumber] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_billing_documents_PaymentIntentId]
    ON [finance].[billing_documents]([PaymentIntentId] ASC) WHERE ([PaymentIntentId] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_billing_documents_TenantId_Kind_IssuedAt]
    ON [finance].[billing_documents]([TenantId] ASC, [Kind] ASC, [IssuedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_billing_documents_TenantId_BookingId]
    ON [finance].[billing_documents]([TenantId] ASC, [BookingId] ASC);


GO
