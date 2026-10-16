-- Idempotent unified floor board columns (queue source + walk-in policy).

IF COL_LENGTH('booking.queue_tickets', 'Source') IS NULL
BEGIN
    ALTER TABLE [booking].[queue_tickets]
        ADD [Source] INT NOT NULL CONSTRAINT [DF_queue_tickets_Source] DEFAULT ((0));
END
GO

IF COL_LENGTH('tenancy.business_profiles', 'WalkInsEnabled') IS NULL
BEGIN
    ALTER TABLE [tenancy].[business_profiles]
        ADD [WalkInsEnabled] BIT NOT NULL CONSTRAINT [DF_business_profiles_WalkInsEnabled] DEFAULT ((1));
END
GO

IF COL_LENGTH('tenancy.business_profiles', 'WalkInAcceptingUntil') IS NULL
BEGIN
    ALTER TABLE [tenancy].[business_profiles]
        ADD [WalkInAcceptingUntil] DATETIMEOFFSET (7) NULL;
END
GO

IF COL_LENGTH('tenancy.business_profiles', 'MaxWalkInWaiting') IS NULL
BEGIN
    ALTER TABLE [tenancy].[business_profiles]
        ADD [MaxWalkInWaiting] INT NULL;
END
GO

IF COL_LENGTH('tenancy.business_profiles', 'WalkInCalendarPolicy') IS NULL
BEGIN
    ALTER TABLE [tenancy].[business_profiles]
        ADD [WalkInCalendarPolicy] INT NOT NULL CONSTRAINT [DF_business_profiles_WalkInCalendarPolicy] DEFAULT ((0));
END
GO
