CREATE TABLE [tenancy].[tenants] (
    [Id]               UNIQUEIDENTIFIER   NOT NULL,
    [Name]             NVARCHAR (200)     NOT NULL,
    [Status]           INT                NOT NULL,
    [SubscriptionTier] INT                NOT NULL,
    [CreatedAt]        DATETIMEOFFSET (7) NOT NULL,
    [VerifiedAt]       DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_tenants] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

