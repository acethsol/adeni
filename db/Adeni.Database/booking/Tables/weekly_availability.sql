CREATE TABLE [booking].[weekly_availability] (
    [Id]        UNIQUEIDENTIFIER NOT NULL,
    [TenantId]  UNIQUEIDENTIFIER NOT NULL,
    [DayOfWeek] INT              NOT NULL,
    [OpenTime]  TIME (7)         NOT NULL,
    [CloseTime] TIME (7)         NOT NULL,
    CONSTRAINT [PK_weekly_availability] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_weekly_availability_TenantId_DayOfWeek]
    ON [booking].[weekly_availability]([TenantId] ASC, [DayOfWeek] ASC);


GO

