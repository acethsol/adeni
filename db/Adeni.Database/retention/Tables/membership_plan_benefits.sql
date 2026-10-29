CREATE TABLE [retention].[membership_plan_benefits] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER NOT NULL,
    [MembershipPlanId]  UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceName]       NVARCHAR (200)   NOT NULL,
    [QuantityPerCycle]  INT              NOT NULL,
    [SortOrder]         INT              NOT NULL,
    CONSTRAINT [PK_membership_plan_benefits] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_membership_plan_benefits_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_membership_plan_benefits_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_membership_plan_benefits_membership_plans] FOREIGN KEY ([TenantId], [MembershipPlanId]) REFERENCES [retention].[membership_plans] ([TenantId], [Id]),
    CONSTRAINT [FK_membership_plan_benefits_service_offerings] FOREIGN KEY ([TenantId], [ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([TenantId], [Id]),
    CONSTRAINT [CK_membership_plan_benefits_Quantity] CHECK ([QuantityPerCycle] BETWEEN 1 AND 1000)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_membership_plan_benefits_TenantId_MembershipPlanId_ServiceOfferingId]
    ON [retention].[membership_plan_benefits] ([TenantId] ASC, [MembershipPlanId] ASC, [ServiceOfferingId] ASC);
GO
