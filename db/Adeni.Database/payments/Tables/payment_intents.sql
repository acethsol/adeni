CREATE TABLE [payments].[payment_intents] (
    [Id]                UNIQUEIDENTIFIER   NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER   NOT NULL,
    [BookingId]         UNIQUEIDENTIFIER   NULL,
    [Type]              INT                NOT NULL,
    [Amount]            DECIMAL (12, 2)    NOT NULL,
    [PlatformFeeAmount] DECIMAL (12, 2)    NOT NULL,
    [Currency]          NVARCHAR (3)       NOT NULL,
    [Status]            INT                NOT NULL,
    [ProviderReference] NVARCHAR (128)     NOT NULL,
    [Description]       NVARCHAR (500)     NULL,
    [CustomerEmail]     NVARCHAR (320)     NULL,
    [CallbackUrl]       NVARCHAR (2048)    NULL,
    [IdempotencyKey]    NVARCHAR (128)     NULL,
    [CreatedAt]         DATETIMEOFFSET (7) NOT NULL,
    [UpdatedAt]         DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_payment_intents] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_payment_intents_ProviderReference]
    ON [payments].[payment_intents]([ProviderReference] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_payment_intents_TenantId_Status_CreatedAt]
    ON [payments].[payment_intents]([TenantId] ASC, [Status] ASC, [CreatedAt] ASC);


GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_payment_intents_IdempotencyKey]
    ON [payments].[payment_intents]([IdempotencyKey] ASC) WHERE ([IdempotencyKey] IS NOT NULL);


GO

