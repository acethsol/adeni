CREATE TABLE [calendar].[oauth_attempts] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [StaffMemberId] UNIQUEIDENTIFIER NOT NULL,
    [Provider] NVARCHAR(16) NOT NULL,
    [Actor] NVARCHAR(128) NOT NULL,
    [StateHash] NVARCHAR(64) NOT NULL,
    [CorrelationHash] NVARCHAR(64) NOT NULL,
    [ProtectedVerifier] NVARCHAR(2048) NOT NULL,
    [WriteAccess] BIT NOT NULL,
    [ExpiresAt] DATETIMEOFFSET(7) NOT NULL,
    [LaunchedAt] DATETIMEOFFSET(7) NULL,
    [ConsumedAt] DATETIMEOFFSET(7) NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_calendar_oauth_attempts] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_calendar_oauth_attempts_TenantId_Id] UNIQUE ([TenantId],[Id]),
    CONSTRAINT [FK_calendar_oauth_attempts_StaffMemberId] FOREIGN KEY ([TenantId],[StaffMemberId]) REFERENCES [booking].[staff_members] ([TenantId],[Id])
);
GO
CREATE UNIQUE INDEX [IX_calendar_oauth_attempts_StateHash] ON [calendar].[oauth_attempts] ([StateHash]);
