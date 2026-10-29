CREATE TABLE [retention].[customer_memberships] (
    [Id]                          UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                    UNIQUEIDENTIFIER   NOT NULL,
    [TenantCustomerId]            UNIQUEIDENTIFIER   NOT NULL,
    [MembershipPlanId]            UNIQUEIDENTIFIER   NOT NULL,
    [EnrolledAtLocationId]        UNIQUEIDENTIFIER   NULL,
    [EnrolChannel]                INT                NOT NULL,
    [Status]                      INT                NOT NULL,
    [NameSnapshot]                NVARCHAR (200)     NOT NULL,
    [PriceSnapshot]               DECIMAL (12, 2)    NOT NULL,
    [CurrencySnapshot]            NVARCHAR (3)       NOT NULL,
    [BillingIntervalSnapshot]     INT                NOT NULL,
    [RolloverPolicySnapshot]      INT                NOT NULL,
    [CancellationPolicySnapshot]  INT                NOT NULL,
    [AllowFreezeSnapshot]         BIT                NOT NULL,
    [PastDueGraceDaysSnapshot]    INT                NOT NULL,
    [Note]                        NVARCHAR (500)     NULL,
    [EnrolledBy]                  NVARCHAR (128)     NOT NULL,
    [CreatedAt]                   DATETIMEOFFSET (7) NOT NULL,
    [ActivatedAt]                 DATETIMEOFFSET (7) NULL,
    [CurrentCycleNumber]          INT                NOT NULL,
    [CurrentCycleStart]           DATETIMEOFFSET (7) NULL,
    [CurrentCycleEnd]             DATETIMEOFFSET (7) NULL,
    [FrozenAt]                    DATETIMEOFFSET (7) NULL,
    [CancelRequestedAt]           DATETIMEOFFSET (7) NULL,
    [CancelledAt]                 DATETIMEOFFSET (7) NULL,
    [CancelledBy]                 NVARCHAR (128)     NULL,
    [CancelReason]                NVARCHAR (500)     NULL,
    [ProviderName]                NVARCHAR (32)      NULL,
    [ProviderMandateRef]          NVARCHAR (128)     NULL,
    [ProviderSubscriptionRef]     NVARCHAR (128)     NULL,
    [PastDueSince]                DATETIMEOFFSET (7) NULL,
    [FailedPaymentCount]          INT                NOT NULL CONSTRAINT [DF_customer_memberships_FailedPaymentCount] DEFAULT (0),
    [ExpiredAt]                   DATETIMEOFFSET (7) NULL,
    [RowVersion]                  ROWVERSION         NOT NULL,
    CONSTRAINT [PK_customer_memberships] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_customer_memberships_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_customer_memberships_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_customer_memberships_tenant_customers] FOREIGN KEY ([TenantId], [TenantCustomerId]) REFERENCES [tenancy].[tenant_customers] ([TenantId], [Id]),
    CONSTRAINT [FK_customer_memberships_membership_plans] FOREIGN KEY ([TenantId], [MembershipPlanId]) REFERENCES [retention].[membership_plans] ([TenantId], [Id]),
    CONSTRAINT [FK_customer_memberships_business_locations] FOREIGN KEY ([TenantId], [EnrolledAtLocationId]) REFERENCES [tenancy].[business_locations] ([TenantId], [Id]),
    CONSTRAINT [CK_customer_memberships_Status] CHECK ([Status] IN (0,1,2,3,4,5,6)),
    CONSTRAINT [CK_customer_memberships_EnrolChannel] CHECK ([EnrolChannel] IN (0,1,2)),
    CONSTRAINT [CK_customer_memberships_BillingInterval] CHECK ([BillingIntervalSnapshot] IN (0,1,2)),
    CONSTRAINT [CK_customer_memberships_RolloverPolicy] CHECK ([RolloverPolicySnapshot] IN (0,1)),
    CONSTRAINT [CK_customer_memberships_CancellationPolicy] CHECK ([CancellationPolicySnapshot] IN (0,1))
);
GO
CREATE NONCLUSTERED INDEX [IX_customer_memberships_TenantId_TenantCustomerId_Status]
    ON [retention].[customer_memberships] ([TenantId] ASC, [TenantCustomerId] ASC, [Status] ASC);
GO
CREATE NONCLUSTERED INDEX [IX_customer_memberships_TenantId_Status_CurrentCycleEnd]
    ON [retention].[customer_memberships] ([TenantId] ASC, [Status] ASC, [CurrentCycleEnd] ASC);
GO
-- One live (non-terminal) membership per customer and plan: Pending, Active, PastDue, Frozen, CancelPending.
CREATE UNIQUE NONCLUSTERED INDEX [IX_customer_memberships_TenantId_TenantCustomerId_MembershipPlanId]
    ON [retention].[customer_memberships] ([TenantId] ASC, [TenantCustomerId] ASC, [MembershipPlanId] ASC)
    WHERE [Status] IN (0,1,2,3,4);
GO
