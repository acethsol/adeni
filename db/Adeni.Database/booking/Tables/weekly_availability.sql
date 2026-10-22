CREATE TABLE [booking].[weekly_availability] (
    [Id]                 UNIQUEIDENTIFIER NOT NULL,
    [TenantId]           UNIQUEIDENTIFIER NOT NULL,
    [BusinessLocationId] UNIQUEIDENTIFIER NOT NULL,
    [DayOfWeek]          INT              NOT NULL,
    [OpenTime]           TIME (7)         NOT NULL,
    [CloseTime]          TIME (7)         NOT NULL,
    CONSTRAINT [PK_weekly_availability] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_weekly_availability_business_locations_BusinessLocationId] FOREIGN KEY ([BusinessLocationId]) REFERENCES [tenancy].[business_locations] ([Id])
);


GO

CREATE NONCLUSTERED INDEX [IX_weekly_availability_TenantId_BusinessLocationId_DayOfWeek]
    ON [booking].[weekly_availability]([TenantId] ASC, [BusinessLocationId] ASC, [DayOfWeek] ASC);


GO

