namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Reporting;
using Adeni.Application.Storage;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ReportPdfBrandingLoader(
    AdeniDbContext dbContext,
    IFileStorage fileStorage) : IReportPdfBrandingLoader
{
    public Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        LoadForTenantAsync(tenantId, "Business", cancellationToken);

    public async Task<ReportPdfBranding> LoadForTenantAsync(
        Guid tenantId,
        string businessNameFallback,
        CancellationToken cancellationToken = default)
    {
        var tenantRow = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new { x.Name })
            .FirstOrDefaultAsync(cancellationToken);

        var logoKey = await dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.LogoImageKey)
            .FirstOrDefaultAsync(cancellationToken);

        var businessName = string.IsNullOrWhiteSpace(tenantRow?.Name)
            ? businessNameFallback
            : tenantRow.Name.Trim();

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(logoKey))
        {
            try
            {
                logoBytes = await fileStorage.ReadBytesAsync(logoKey.Trim(), cancellationToken);
            }
            catch
            {
                logoBytes = null;
            }
        }

        return new ReportPdfBranding(businessName, logoBytes);
    }
}
