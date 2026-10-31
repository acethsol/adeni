CREATE TABLE [tenancy].[location_security_policies] (
    [TenantId]                                UNIQUEIDENTIFIER   NOT NULL,
    [RequirePresenceForSensitiveCustomerData] BIT                NOT NULL CONSTRAINT [DF_location_security_policies_require_presence] DEFAULT (0),
    [PresenceValidityMinutes]                 INT                NOT NULL CONSTRAINT [DF_location_security_policies_validity_minutes] DEFAULT (90),
    [AllowManagerPresenceOverride]            BIT                NOT NULL CONSTRAINT [DF_location_security_policies_manager_override] DEFAULT (1),
    [UpdatedAt]                               DATETIMEOFFSET (7) NOT NULL,
    [UpdatedByBusinessUserId]                 UNIQUEIDENTIFIER   NULL,
    CONSTRAINT [PK_location_security_policies] PRIMARY KEY CLUSTERED ([TenantId] ASC),
    CONSTRAINT [FK_location_security_policies_tenants] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO
