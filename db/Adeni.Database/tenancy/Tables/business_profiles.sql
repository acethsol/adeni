CREATE TABLE [tenancy].[business_profiles] (
    [TenantId]                UNIQUEIDENTIFIER   NOT NULL,
    [Description]             NVARCHAR (2000)    NOT NULL,
    [CategorySlug]            NVARCHAR (64)      NOT NULL,
    [Phone]                   NVARCHAR (32)      NOT NULL,
    [CoverImageKey]           NVARCHAR (512)     NULL,
    [GalleryImageKeysJson]    NVARCHAR (4000)    NULL,
    [LogoImageKey]            NVARCHAR (512)     NULL,
    [PublicPageTemplateId]    NVARCHAR (32)      DEFAULT (N'studio') NOT NULL,
    [PublicPageAccentColor]   NVARCHAR (7)       NULL,
    [PublicPageShowAbout]     BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [PublicPageShowServices]  BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [PublicPageShowReviews]   BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [PublicPageShowVisit]     BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [PublicPageShowBook]      BIT                DEFAULT (CONVERT([bit],(1))) NOT NULL,
    [PublicPageShowPolicies]  BIT                DEFAULT (CONVERT([bit],(0))) NOT NULL,
    [BusinessType]            INT                NOT NULL,
    [AutoConfirmBookings]     BIT                NOT NULL,
    [DepositPercent]          INT                NOT NULL,
    [PolicyBookingText]       NVARCHAR (MAX)     NULL,
    [PolicyPaymentText]       NVARCHAR (MAX)     NULL,
    [PolicyCancellationText]  NVARCHAR (MAX)     NULL,
    [PolicyTermsText]         NVARCHAR (MAX)     NULL,
    [RequirePolicyAcceptance] BIT                NOT NULL,
    [FaqAutoResponderEnabled] BIT                NOT NULL,
    [UpdatedAt]               DATETIMEOFFSET (7) NOT NULL,
    CONSTRAINT [PK_business_profiles] PRIMARY KEY CLUSTERED ([TenantId] ASC),
    CONSTRAINT [FK_business_profiles_tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [tenancy].[tenants] ([Id]) ON DELETE CASCADE
);


GO

