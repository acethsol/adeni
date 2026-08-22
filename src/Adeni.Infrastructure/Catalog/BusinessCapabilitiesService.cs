namespace Adeni.Infrastructure.Catalog;

using Adeni.Application.Catalog;
using Adeni.Domain.Tenancy;

public sealed class BusinessCapabilitiesService(CategoryWorkflowCatalog catalog) : IBusinessCapabilitiesService
{
    public BusinessType GetDefaultBusinessType(string categorySlug) =>
        catalog.GetDefaultBusinessType(categorySlug);

    public IReadOnlyList<string> GetCapabilities(BusinessType businessType, string categorySlug) =>
        catalog.GetCapabilities(businessType, categorySlug);

    public string GetDiscoveryCta(BusinessType businessType) =>
        catalog.GetDiscoveryCta(businessType);

    public bool HasCapability(BusinessType businessType, string categorySlug, string capability) =>
        GetCapabilities(businessType, categorySlug)
            .Any(item => item.Equals(capability, StringComparison.OrdinalIgnoreCase));
}
