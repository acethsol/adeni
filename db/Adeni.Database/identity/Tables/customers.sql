CREATE TABLE [identity].[customers] (
    [Id]                 UNIQUEIDENTIFIER   NOT NULL,
    [Auth0Sub]           NVARCHAR (128)     NOT NULL,
    [Name]               NVARCHAR (200)     NOT NULL,
    [Email]              NVARCHAR (320)     NULL,
    [Phone]              NVARCHAR (32)      NULL,
    [CreatedAt]          DATETIMEOFFSET (7) NOT NULL,
    [ErasureRequestedAt] DATETIMEOFFSET (7) NULL,
    CONSTRAINT [PK_customers] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_customers_Auth0Sub]
    ON [identity].[customers]([Auth0Sub] ASC);


GO

