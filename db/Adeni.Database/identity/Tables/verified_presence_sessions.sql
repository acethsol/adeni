CREATE TABLE [identity].[verified_presence_sessions] (
    [Id]                           UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                     UNIQUEIDENTIFIER   NOT NULL,
    [BusinessUserId]               UNIQUEIDENTIFIER   NOT NULL,
    [StaffMemberId]                UNIQUEIDENTIFIER   NULL,
    [BusinessLocationId]           UNIQUEIDENTIFIER   NOT NULL,
    [VerifiedAt]                   DATETIMEOFFSET (7) NOT NULL,
    [ExpiresAt]                    DATETIMEOFFSET (7) NOT NULL,
    [LastExtendedAt]               DATETIMEOFFSET (7) NULL,
    [VerificationMethod]           NVARCHAR (32)      NOT NULL,
    [DistanceFromLocationMeters]   INT                NULL,
    [ReportedAccuracyMeters]       INT                NULL,
    [InvalidatedAt]                DATETIMEOFFSET (7) NULL,
    [InvalidationReason]           NVARCHAR (32)      NULL,
    [SourceAttendanceEventId]      UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [PK_verified_presence_sessions] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_verified_presence_sessions_business_users] FOREIGN KEY ([BusinessUserId]) REFERENCES [identity].[business_users] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_verified_presence_sessions_TenantId_BusinessUserId_Active]
    ON [identity].[verified_presence_sessions]([TenantId] ASC, [BusinessUserId] ASC, [InvalidatedAt] ASC, [ExpiresAt] ASC);


GO
