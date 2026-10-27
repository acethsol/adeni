CREATE TABLE [calendar].[connections] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [StaffMemberId] UNIQUEIDENTIFIER NOT NULL,
    [Provider] NVARCHAR(16) NOT NULL,
    [AccountHash] NVARCHAR(64) NOT NULL,
    [ProtectedCredentials] NVARCHAR(MAX) NOT NULL,
    [TokenExpiresAt] DATETIMEOFFSET(7) NOT NULL,
    [WriteAccess] BIT NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [Generation] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [NextSyncAt] DATETIMEOFFSET(7) NOT NULL,
    [LeaseUntil] DATETIMEOFFSET(7) NULL,
    [LeaseId] UNIQUEIDENTIFIER NULL,
    [FailureCount] INT NOT NULL DEFAULT (0),
    [FailureCategory] NVARCHAR(32) NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_calendar_connections] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_connections_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_connections_StaffMemberId] FOREIGN KEY ([TenantId],[StaffMemberId]) REFERENCES [booking].[staff_members] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_calendar_connections_TenantId_StaffMemberId_Provider] ON [calendar].[connections] ([TenantId],[StaffMemberId],[Provider]);
GO
CREATE INDEX [IX_calendar_connections_NextSyncAt] ON [calendar].[connections] ([NextSyncAt]);
