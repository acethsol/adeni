CREATE TABLE [retention].[membership_payments] (
    [Id]                   UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]             UNIQUEIDENTIFIER   NOT NULL,
    [CustomerMembershipId] UNIQUEIDENTIFIER   NOT NULL,
    [CycleNumber]          INT                NOT NULL,
    [Kind]                 INT                NOT NULL,
    [PaymentIntentId]      UNIQUEIDENTIFIER   NOT NULL,
    [Amount]               DECIMAL (12, 2)    NOT NULL,
    [Currency]             NVARCHAR (3)       NOT NULL,
    [Status]               INT                NOT NULL,
    [ProviderReference]    NVARCHAR (128)     NULL,
    [FailureReason]        NVARCHAR (200)     NULL,
    [CreatedAt]            DATETIMEOFFSET (7) NOT NULL,
    [ResolvedAt]           DATETIMEOFFSET (7) NULL,
    [RowVersion]           ROWVERSION         NOT NULL,
    CONSTRAINT [PK_membership_payments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_membership_payments_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_membership_payments_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_membership_payments_customer_memberships] FOREIGN KEY ([TenantId], [CustomerMembershipId]) REFERENCES [retention].[customer_memberships] ([TenantId], [Id]),
    CONSTRAINT [CK_membership_payments_Kind] CHECK ([Kind] IN (0,1)),
    CONSTRAINT [CK_membership_payments_Status] CHECK ([Status] IN (0,1,2,3)),
    CONSTRAINT [CK_membership_payments_CycleNumber] CHECK ([CycleNumber] >= 1)
);
GO
-- One provider payment intent maps to exactly one cycle charge (replay-safe PaymentCompleted handling).
CREATE UNIQUE NONCLUSTERED INDEX [IX_membership_payments_PaymentIntentId]
    ON [retention].[membership_payments] ([PaymentIntentId] ASC);
GO
-- At most one open (Pending) or paid (Succeeded) charge per membership cycle; failed / unapplied attempts may be superseded.
CREATE UNIQUE NONCLUSTERED INDEX [IX_membership_payments_TenantId_CustomerMembershipId_CycleNumber]
    ON [retention].[membership_payments] ([TenantId] ASC, [CustomerMembershipId] ASC, [CycleNumber] ASC)
    WHERE [Status] IN (0,1);
GO
CREATE NONCLUSTERED INDEX [IX_membership_payments_TenantId_Status_CreatedAt]
    ON [retention].[membership_payments] ([TenantId] ASC, [Status] ASC, [CreatedAt] ASC);
GO