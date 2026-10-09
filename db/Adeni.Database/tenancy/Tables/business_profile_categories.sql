CREATE TABLE [tenancy].[business_profile_categories] (
    [TenantId]     UNIQUEIDENTIFIER NOT NULL,
    [CategorySlug] NVARCHAR (64)    NOT NULL,
    [IsPrimary]    BIT              NOT NULL,
    CONSTRAINT [PK_business_profile_categories] PRIMARY KEY CLUSTERED ([TenantId] ASC, [CategorySlug] ASC),
    CONSTRAINT [FK_business_profile_categories_business_profiles_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[business_profiles] ([TenantId]) ON DELETE CASCADE
);


GO

