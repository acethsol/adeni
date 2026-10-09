CREATE TABLE [booking].[staff_service_links] (
    [StaffMemberId]     UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId]          UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_staff_service_links] PRIMARY KEY CLUSTERED ([StaffMemberId] ASC, [ServiceOfferingId] ASC),
    CONSTRAINT [FK_staff_service_links_service_offerings_ServiceOfferingId] FOREIGN KEY ([ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_staff_service_links_staff_members_StaffMemberId] FOREIGN KEY ([StaffMemberId]) REFERENCES [booking].[staff_members] ([Id]) ON DELETE CASCADE
);


GO

CREATE NONCLUSTERED INDEX [IX_staff_service_links_TenantId_ServiceOfferingId]
    ON [booking].[staff_service_links]([TenantId] ASC, [ServiceOfferingId] ASC);


GO

CREATE NONCLUSTERED INDEX [IX_staff_service_links_ServiceOfferingId]
    ON [booking].[staff_service_links]([ServiceOfferingId] ASC);


GO

