namespace Adeni.Application.Markets;

public interface IMarketCatalogLoader
{
    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task InvalidateAndReloadAsync(CancellationToken cancellationToken = default);
}
