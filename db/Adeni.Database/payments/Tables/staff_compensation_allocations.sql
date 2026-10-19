CREATE TABLE [payments].[staff_compensation_allocations] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]         UNIQUEIDENTIFIER   NOT NULL,
    [StaffMemberId]    UNIQUEIDENTIFIER   NOT NULL,
    [BookingId]        UNIQUEIDENTIFIER   NULL,
    [PaymentIntentId]  UNIQUEIDENTIFIER   NULL,
    [Type]             INT                NOT NULL,
    [Amount]           DECIMAL (12, 2)    NOT NULL,
    [Currency]         NVARCHAR (3)       NOT NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_staff_compensation_allocations] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_compensation_allocations_TenantId_CreatedAt]
    ON [payments].[staff_compensation_allocations]([TenantId] ASC, [CreatedAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_compensation_allocations_TenantId_StaffMemberId_CreatedAt]
    ON [payments].[staff_compensation_allocations]([TenantId] ASC, [StaffMemberId] ASC, [CreatedAt] ASC);


GO
