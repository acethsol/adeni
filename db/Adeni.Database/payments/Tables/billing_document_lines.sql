CREATE TABLE [payments].[billing_document_lines] (
    [Id]          UNIQUEIDENTIFIER NOT NULL,
    [DocumentId]  UNIQUEIDENTIFIER NOT NULL,
    [TenantId]    UNIQUEIDENTIFIER NOT NULL,
    [SortOrder]   INT              NOT NULL,
    [Description] NVARCHAR (300)   NOT NULL,
    [Quantity]    DECIMAL (12, 2)  NOT NULL,
    [UnitAmount]  DECIMAL (12, 2)  NOT NULL,
    [LineTotal]   DECIMAL (12, 2)  NOT NULL,
    CONSTRAINT [PK_billing_document_lines] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_billing_document_lines_billing_documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [payments].[billing_documents] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_billing_document_lines_DocumentId_SortOrder]
    ON [payments].[billing_document_lines]([DocumentId] ASC, [SortOrder] ASC);


GO
