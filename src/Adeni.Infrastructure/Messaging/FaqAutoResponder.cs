namespace Adeni.Infrastructure.Messaging;

using Adeni.Application.Messaging;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public sealed class FaqAutoResponder(
    AdeniDbContext dbContext,
    IConfiguration configuration) : IFaqAutoResponder
{
    private static readonly string[] PriceKeywords =
        ["price", "cost", "how much", "rate", "charge", "fee", "pricing"];

    private static readonly string[] HoursKeywords =
        ["hour", "open", "close", "when", "time", "available", "availability", "slot"];

    private static readonly string[] LocationKeywords =
        ["where", "location", "address", "find you", "directions", "located"];

    public async Task<string?> TryBuildReplyAsync(
        Guid tenantId,
        string customerMessage,
        CancellationToken cancellationToken = default)
    {
        var normalized = customerMessage.Trim().ToLowerInvariant();
        if (normalized.Length is 0)
        {
            return null;
        }

        if (MatchesAny(normalized, PriceKeywords))
        {
            return await BuildPricingReplyAsync(tenantId, cancellationToken);
        }

        if (MatchesAny(normalized, HoursKeywords))
        {
            return await BuildHoursReplyAsync(tenantId, cancellationToken);
        }

        if (MatchesAny(normalized, LocationKeywords))
        {
            return await BuildLocationReplyAsync(tenantId, cancellationToken);
        }

        return null;
    }

    private async Task<string?> BuildPricingReplyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var services = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return "Thanks for asking! View our services and book online on Adeni — we'll confirm your slot in real time.";
        }

        var pricing = string.Join(
            ", ",
            services.Select(s => $"{s.Name} from {s.Currency} {s.PriceAmount:N0}"));

        return $"Our services: {pricing}. Book online anytime on Adeni.";
    }

    private async Task<string?> BuildHoursReplyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var hours = await dbContext.WeeklyAvailabilities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync(cancellationToken);

        if (hours.Count == 0)
        {
            return "Thanks for reaching out! Check our live availability and book online on Adeni — we confirm slots in real time.";
        }

        var summary = string.Join(
            "; ",
            hours.Select(h =>
                $"{h.DayOfWeek}: {h.OpenTime:HH:mm}–{h.CloseTime:HH:mm}"));

        return $"We're typically open {summary}. Check live slots and book on Adeni anytime.";
    }

    private async Task<string?> BuildLocationReplyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        var location = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .FirstOrDefaultAsync(cancellationToken);

        var businessName = tenant?.Name ?? "our business";
        if (location is null)
        {
            return $"You can find {businessName} on Adeni and book a visit.";
        }

        var publicUrl = $"{ResolvePublicWebBaseUrl()}/businesses/{location.Slug}";
        return $"We're at {location.AddressLine}, {location.Area}. Book your visit on Adeni: {publicUrl}";
    }

    private string ResolvePublicWebBaseUrl()
    {
        var configured = configuration["App:PublicWebBaseUrl"]?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? "https://adeni.io" : configured.TrimEnd('/');
    }

    private static bool MatchesAny(string normalized, IEnumerable<string> keywords) =>
        keywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
}
