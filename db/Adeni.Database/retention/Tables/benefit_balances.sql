CREATE TABLE [retention].[benefit_balances] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [CustomerPackageId] UNIQUEIDENTIFIER   NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER   NOT NULL,
    [ServiceName]       NVARCHAR (200)     NOT NULL,
    [Granted]           INT                NOT NULL,
    [Used]              INT                NOT NULL,
    [Reserved]          INT                NOT NULL,
    [Forfeited]         INT                NOT NULL,
    [Sequence]          INT                NOT NULL,
    [ExpiresAt]         DATETIMEOFFSET (7) NULL,
    [RowVersion]        ROWVERSION         NOT NULL,
    CONSTRAINT [PK_benefit_balances] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_benefit_balances_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_benefit_balances_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_benefit_balances_customer_packages] FOREIGN KEY ([TenantId], [CustomerPackageId]) REFERENCES [retention].[customer_packages] ([TenantId], [Id]),
    CONSTRAINT [CK_benefit_balances_Invariant] CHECK (
        [Granted] > 0 AND [Used] >= 0 AND [Reserved] >= 0 AND [Forfeited] >= 0
        AND ([Used] + [Reserved] + [Forfeited]) <= [Granted])
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_benefit_balances_TenantId_CustomerPackageId_ServiceOfferingId]
    ON [retention].[benefit_balances] ([TenantId] ASC, [CustomerPackageId] ASC, [ServiceOfferingId] ASC);
GO
