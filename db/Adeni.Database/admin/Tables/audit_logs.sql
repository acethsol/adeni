CREATE TABLE [admin].[audit_logs] (
    [Id]            UNIQUEIDENTIFIER   NOT NULL,
    [ActorId]       NVARCHAR (128)     NOT NULL,
    [Action]        NVARCHAR (128)     NOT NULL,
    [EntityType]    NVARCHAR (64)      NOT NULL,
    [EntityId]      NVARCHAR (64)      NOT NULL,
    [CorrelationId] NVARCHAR (64)      NOT NULL,
    [OccurredAt]    DATETIMEOFFSET (7) NOT NULL,
    [MetadataJson]  NVARCHAR (MAX)     NULL,
    CONSTRAINT [PK_audit_logs] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

