CREATE TABLE [booking].[staff_attendance_events] (
    [Id]                              UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]                        UNIQUEIDENTIFIER   NOT NULL,
    [BusinessLocationId]              UNIQUEIDENTIFIER   NOT NULL,
    [StaffMemberId]                   UNIQUEIDENTIFIER   NOT NULL,
    [BusinessUserId]                  UNIQUEIDENTIFIER   NOT NULL,
    [EventType]                       NVARCHAR (32)      NOT NULL,
    [OccurredAt]                      DATETIMEOFFSET (7) NOT NULL,
    [RecordedAt]                      DATETIMEOFFSET (7) NOT NULL,
    [LocationVerificationStatus]      NVARCHAR (32)      NOT NULL,
    [DistanceFromLocationMeters]      INT                NULL,
    [ReportedAccuracyMeters]          INT                NULL,
    [VerificationMethod]              NVARCHAR (32)      NOT NULL,
    [ManagerOverrideByBusinessUserId] UNIQUEIDENTIFIER   NULL,
    [ManagerOverrideReason]           NVARCHAR (500)     NULL,
    [RelatedEventId]                  UNIQUEIDENTIFIER   NULL,
    [CorrectionReason]                NVARCHAR (500)     NULL,
    [ClientLocationCapturedAt]        DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_staff_attendance_events] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_staff_attendance_events_staff_members] FOREIGN KEY ([StaffMemberId]) REFERENCES [booking].[staff_members] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_attendance_events_TenantId_StaffMemberId_OccurredAt]
    ON [booking].[staff_attendance_events]([TenantId] ASC, [StaffMemberId] ASC, [OccurredAt] DESC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_attendance_events_TenantId_BusinessLocationId_OccurredAt]
    ON [booking].[staff_attendance_events]([TenantId] ASC, [BusinessLocationId] ASC, [OccurredAt] ASC);


GO
