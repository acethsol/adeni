CREATE TABLE [calendar].[staff_state] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [AdmissionStamp] UNIQUEIDENTIFIER NOT NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_calendar_staff_state] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_staff_state_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_staff_state_Id] FOREIGN KEY ([TenantId],[Id]) REFERENCES [booking].[staff_members] ([TenantId],[Id])
);
