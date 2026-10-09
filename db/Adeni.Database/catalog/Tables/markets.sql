CREATE TABLE [catalog].[markets] (
    [Id]            NVARCHAR (32)      NOT NULL,
    [Name]          NVARCHAR (120)     NOT NULL,
    [CountryCode]   NVARCHAR (2)       NOT NULL,
    [Currency]      NVARCHAR (3)       NOT NULL,
    [TimeZoneId]    NVARCHAR (64)      NOT NULL,
    [DefaultLat]    FLOAT (53)         NOT NULL,
    [DefaultLng]    FLOAT (53)         NOT NULL,
    [LanguagesJson] NVARCHAR (64)      NOT NULL,
    [IsLive]        BIT                NOT NULL,
    [LaunchNote]    NVARCHAR (500)     NULL,
    [CreatedAt]     DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]     DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_markets] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE NONCLUSTERED INDEX [IX_markets_IsLive]
    ON [catalog].[markets]([IsLive] ASC);


GO

