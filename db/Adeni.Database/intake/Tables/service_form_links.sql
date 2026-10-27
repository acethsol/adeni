CREATE TABLE [intake].[service_form_links] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceOfferingId] UNIQUEIDENTIFIER NOT NULL,
    [TemplateId] UNIQUEIDENTIFIER NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PK_service_form_links] PRIMARY KEY ([TenantId], [ServiceOfferingId]),
    CONSTRAINT [FK_service_form_links_0] FOREIGN KEY ([TenantId], [TemplateId]) REFERENCES [intake].[form_templates] ([TenantId], [Id]),
    CONSTRAINT [FK_service_form_links_1] FOREIGN KEY ([TenantId], [ServiceOfferingId]) REFERENCES [booking].[service_offerings] ([TenantId], [Id])
);
