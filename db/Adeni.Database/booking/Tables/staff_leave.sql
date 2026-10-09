CREATE TABLE [booking].[staff_leave] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]      UNIQUEIDENTIFIER   NOT NULL,
    [StaffMemberId] UNIQUEIDENTIFIER   NOT NULL,
    [StartAt]       DATETIMEOFFSET (7) NOT NULL,
    [EndAt]         DATETIMEOFFSET (7) NOT NULL,
    [Reason]        NVARCHAR (200)     NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_staff_leave] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_staff_leave_staff_members_StaffMemberId] FOREIGN KEY ([StaffMemberId]) REFERENCES [booking].[staff_members] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_leave_StaffMemberId]
    ON [booking].[staff_leave]([StaffMemberId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_leave_TenantId_StaffMemberId_StartAt]
    ON [booking].[staff_leave]([TenantId] ASC, [StaffMemberId] ASC, [StartAt] ASC);


GO

