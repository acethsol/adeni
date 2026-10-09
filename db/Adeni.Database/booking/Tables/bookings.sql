CREATE TABLE [booking].[bookings] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER   NOT NULL,
    [CustomerId]        UNIQUEIDENTIFIER   NOT NULL,
    [StaffMemberId]     UNIQUEIDENTIFIER   NULL,
    [GuestCount]        INT                DEFAULT ((1)) NOT NULL,
    [StartAt]           DATETIMEOFFSET (7) NOT NULL,
    [EndAt]             DATETIMEOFFSET (7) NOT NULL,
    [Status]            INT                NOT NULL,
    [CustomerNotes]     NVARCHAR (1000)    NULL,
    [BusinessNotes]     NVARCHAR (1000)    NULL,
    [IdempotencyKey]    NVARCHAR (128)     NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]         DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_bookings] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_bookings_service_offerings_ServiceOfferingId] FOREIGN KEY ([ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([Id]),
    CONSTRAINT [FK_bookings_staff_members_StaffMemberId] FOREIGN KEY ([StaffMemberId]) REFERENCES [booking].[staff_members] ([Id]) ON DELETE SET NULL
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_bookings_IdempotencyKey]
    ON [booking].[bookings]([IdempotencyKey] ASC) WHERE ([IdempotencyKey] IS NOT NULL);


GO

CREATE NONCLUSTERED INDEX [IX_bookings_ServiceOfferingId]
    ON [booking].[bookings]([ServiceOfferingId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_bookings_TenantId_Status_StartAt]
    ON [booking].[bookings]([TenantId] ASC, [Status] ASC, [StartAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_bookings_TenantId_StaffMemberId_StartAt]
    ON [booking].[bookings]([TenantId] ASC, [StaffMemberId] ASC, [StartAt] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_bookings_StaffMemberId]
    ON [booking].[bookings]([StaffMemberId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_bookings_TenantId_StartAt]
    ON [booking].[bookings]([TenantId] ASC, [StartAt] ASC);


GO

