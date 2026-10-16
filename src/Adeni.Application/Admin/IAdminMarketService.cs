namespace Adeni.Application.Admin;

using Adeni.Domain.Common;

public sealed record AdminMarketResponse(
    string Id,
    string Name,
    string CountryCode,
    string Currency,
    string TimeZoneId,
    double DefaultLat,
    double DefaultLng,
    IReadOnlyList<string> Languages,
    bool IsLive,
    string? LaunchNote,
    DateTimeOffset UpdatedAt,
    int BusinessCount);

public sealed record CreateMarketRequest(
    string Id,
    string Name,
    string CountryCode,
    string Currency,
    string TimeZoneId,
    double DefaultLat,
    double DefaultLng,
    IReadOnlyList<string> Languages,
    bool IsLive,
    string? LaunchNote);

public sealed record UpdateMarketRequest(
    string Name,
    string CountryCode,
    string Currency,
    string TimeZoneId,
    double DefaultLat,
    double DefaultLng,
    IReadOnlyList<string> Languages,
    string? LaunchNote);

public sealed record AdminMarketCatalogStats(int TotalCount, int LiveCount);

public interface IAdminMarketService
{
    Task<AdminMarketCatalogStats> GetCatalogStatsAsync(CancellationToken cancellationToken = default);

    Task<Result<AdminPagedResult<AdminMarketResponse>>> ListAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Result<AdminMarketResponse>> CreateAsync(
        CreateMarketRequest request,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<Result<AdminMarketResponse>> UpdateAsync(
        string id,
        UpdateMarketRequest request,
        string adminId,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> SetLiveAsync(
        string id,
        bool isLive,
        string adminId,
        CancellationToken cancellationToken = default);
}
