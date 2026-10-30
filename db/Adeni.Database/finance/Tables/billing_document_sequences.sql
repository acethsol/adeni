CREATE TABLE [finance].[billing_document_sequences] (
    [TenantId]  UNIQUEIDENTIFIER NOT NULL,
    [Kind]      INT              NOT NULL,
    [NextValue] INT              NOT NULL,
    CONSTRAINT [PK_billing_document_sequences] PRIMARY KEY CLUSTERED ([TenantId] ASC, [Kind] ASC)
);


GO
