namespace Adeni.Infrastructure.Reporting;

using Adeni.Application.Reporting;

/// <summary>Used when blob storage is not registered (tests). Production replaces with <see cref="Tenancy.ReportPdfBrandingLoader"/>.</summary>
public sealed class FallbackReportPdfBrandingLoader : IReportPdfBrandingLoader
{
    public Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReportPdfBranding("Business", null));

    public Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        string businessNameFallback,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReportPdfBranding(businessNameFallback, null));
}
