CREATE TABLE [retention].[benefit_transactions] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER   NOT NULL,
    [BenefitBalanceId]   UNIQUEIDENTIFIER   NOT NULL,
    [Sequence]           INT                NOT NULL,
    [Kind]               INT                NOT NULL,
    [Quantity]           INT                NOT NULL,
    [Delta]              INT                NOT NULL,
    [ReservedAfter]      INT                NOT NULL,
    [UsedAfter]          INT                NOT NULL,
    [RemainingAfter]     INT                NOT NULL,
    [BookingId]          UNIQUEIDENTIFIER   NULL,
    [BusinessLocationId] UNIQUEIDENTIFIER   NULL,
    [Actor]              NVARCHAR (128)     NOT NULL,
    [Reason]             NVARCHAR (500)     NULL,
    [OccurredAt]         DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_benefit_transactions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [AK_benefit_transactions_TenantId_Id] UNIQUE ([TenantId], [Id]),
    CONSTRAINT [FK_benefit_transactions_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]),
    CONSTRAINT [FK_benefit_transactions_benefit_balances] FOREIGN KEY ([TenantId], [BenefitBalanceId]) REFERENCES [retention].[benefit_balances] ([TenantId], [Id]),
    CONSTRAINT [CK_benefit_transactions_Kind] CHECK ([Kind] IN (0,1,2,3,4,5,6)),
    CONSTRAINT [CK_benefit_transactions_Quantity] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_benefit_transactions_After] CHECK ([ReservedAfter] >= 0 AND [UsedAfter] >= 0 AND [RemainingAfter] >= 0)
);
GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_benefit_transactions_TenantId_BenefitBalanceId_Sequence]
    ON [retention].[benefit_transactions] ([TenantId] ASC, [BenefitBalanceId] ASC, [Sequence] ASC);
GO
-- One reserve / release / redeem per balance per booking: replays cannot double-apply.
CREATE UNIQUE NONCLUSTERED INDEX [IX_benefit_transactions_Booking_Kind]
    ON [retention].[benefit_transactions] ([TenantId] ASC, [BenefitBalanceId] ASC, [BookingId] ASC, [Kind] ASC)
    WHERE [BookingId] IS NOT NULL;
GO
