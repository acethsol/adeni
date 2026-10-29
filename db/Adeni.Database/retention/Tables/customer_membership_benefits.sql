CREATE TABLE [retention].[customer_membership_benefits] (
    [Id]                   UNIQUEIDENTIFIER NOT NULL,
    [TenantId]             UNIQUEIDENTIFIER NOT NULL,
    [CustomerMembershipId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId]    UNIQUEIDENTIFIER NOT NULL,
    [ServiceName]          NVARCHAR (200)   NOT NULL,
    [QuantityPerCycle]     INT              NOT NULL,
    [SortOrder]            INT              NOT NULL,
    CONSTRAINT [PK_customer_membership_benefits] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_customer_membership_benefits_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_customer_membership_benefits_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_customer_membership_benefits_customer_memberships] FOREIGN KEY ([TenantId], [CustomerMembershipId]) REFERENCES [retention].[customer_memberships] ([TenantId], [Id]),
    CONSTRAINT [FK_customer_membership_benefits_service_offerings] FOREIGN KEY ([TenantId], [ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([TenantId], [Id]),
    CONSTRAINT [CK_customer_membership_benefits_Quantity] CHECK ([QuantityPerCycle] BETWEEN 1 AND 1000)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_customer_membership_benefits_TenantId_CustomerMembershipId_ServiceOfferingId]
    ON [retention].[customer_membership_benefits] ([TenantId] ASC, [CustomerMembershipId] ASC, [ServiceOfferingId] ASC);
GO
