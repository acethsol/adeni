namespace Adeni.Application.Reporting;

public sealed record ReportPdfBranding(string BusinessName, byte[]? BusinessLogoBytes);

public interface IReportPdfBrandingLoader
{
    Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        string businessNameFallback,
        CancellationToken cancellationToken = default);
}
