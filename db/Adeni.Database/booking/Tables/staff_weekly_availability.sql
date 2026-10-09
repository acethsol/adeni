CREATE TABLE [booking].[staff_weekly_availability] (
    [Id]            UNIQUEIDENTIFIER NOT NULL,
    [TenantId]      UNIQUEIDENTIFIER NOT NULL,
    [StaffMemberId] UNIQUEIDENTIFIER NOT NULL,
    [DayOfWeek]     INT              NOT NULL,
    [OpenTime]      TIME (7)         NOT NULL,
    [CloseTime]     TIME (7)         NOT NULL,
    CONSTRAINT [PK_staff_weekly_availability] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_staff_weekly_availability_staff_members_StaffMemberId] FOREIGN KEY ([StaffMemberId]) REFERENCES [booking].[staff_members] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_weekly_availability_StaffMemberId]
    ON [booking].[staff_weekly_availability]([StaffMemberId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_weekly_availability_TenantId_StaffMemberId_DayOfWeek]
    ON [booking].[staff_weekly_availability]([TenantId] ASC, [StaffMemberId] ASC, [DayOfWeek] ASC);


GO

