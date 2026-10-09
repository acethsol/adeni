CREATE TABLE [tenancy].[verification_documents] (
    [Id]              UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]        UNIQUEIDENTIFIER   NOT NULL,
    [DocumentType]    INT                NOT NULL,
    [ReferenceNumber] NVARCHAR (128)     NOT NULL,
    [SubmittedAt]     DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_verification_documents] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_verification_documents_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_verification_documents_TenantId]
    ON [tenancy].[verification_documents]([TenantId] ASC);


GO

