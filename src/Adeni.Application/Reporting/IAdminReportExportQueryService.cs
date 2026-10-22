namespace Adeni.Application.Reporting;

using Adeni.Domain.Common;

public sealed record AdminBusinessDirectoryExportRow(
    Guid TenantId,
    string Name,
    string Slug,
    string Status,
    string SubscriptionSku,
    string BillingStatus,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset CreatedAt);

public sealed record AdminPendingVerificationExportRow(
    Guid TenantId,
    string Name,
    string Slug,
    string MarketId,
    DateTimeOffset SubmittedAt,
    int DocumentCount);

public sealed record AdminTransactionExportRow(
    string Kind,
    Guid Id,
    Guid TenantId,
    string TenantName,
    string? ProviderReference,
    string? EventType,
    string? SubscriptionSku,
    long? AmountMinor,
    string Currency,
    string Status,
    DateTimeOffset OccurredAt);

public sealed record AdminPricingCatalogEntryExportRow(
    string CatalogVersionCode,
    string CountryCode,
    string SubscriptionSku,
    string CurrencyCode,
    decimal? MonthlyAmountMajor,
    decimal PlatformFeePercent,
    bool IsSelfServeEnabled);

public interface IAdminReportExportQueryService
{
    Task<Result<IReadOnlyList<AdminBusinessDirectoryExportRow>>> GetBusinessDirectoryAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AdminPendingVerificationExportRow>>> GetPendingVerificationsAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AdminTransactionExportRow>>> GetTransactionsAsync(
        string? search,
        string? kind,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AdminPricingCatalogEntryExportRow>>> GetPricingCatalogEntriesAsync(
        string catalogVersionCode,
        CancellationToken cancellationToken = default);
}
