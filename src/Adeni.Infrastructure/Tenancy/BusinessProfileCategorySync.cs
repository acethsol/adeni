namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Catalog;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class BusinessProfileCategorySync
{
    public static async Task ReplaceAsync(
        AdeniDbContext dbContext,
        Guid tenantId,
        string primaryCategorySlug,
        IReadOnlyList<string>? additionalCategorySlugs,
        ICategoryService categoryService,
        CancellationToken cancellationToken)
    {
        var primary = categoryService.NormalizeSlug(primaryCategorySlug);
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primary };

        if (additionalCategorySlugs is not null)
        {
            foreach (var raw in additionalCategorySlugs)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var normalized = categoryService.NormalizeSlug(raw);
                if (!string.Equals(normalized, primary, StringComparison.OrdinalIgnoreCase))
                {
                    slugs.Add(normalized);
                }
            }
        }

        var existing = await dbContext.BusinessProfileCategories
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        dbContext.BusinessProfileCategories.RemoveRange(existing);

        foreach (var slug in slugs)
        {
            dbContext.BusinessProfileCategories.Add(new BusinessProfileCategory
            {
                TenantId = tenantId,
                CategorySlug = slug,
                IsPrimary = string.Equals(slug, primary, StringComparison.OrdinalIgnoreCase),
            });
        }
    }
}
