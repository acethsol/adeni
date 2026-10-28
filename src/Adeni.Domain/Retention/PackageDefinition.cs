namespace Adeni.Domain.Retention;

using System.Text.Json;
using Adeni.Domain.Tenancy;

/// <summary>Sellable package product (session / service credits only — never currency).</summary>
public sealed class PackageDefinition : ITenantEntity
{
    public const int DefaultExpiryMonthsFallback = 12;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Display list price for till / online sale. Not a stored value.</summary>
    public decimal ListPrice { get; set; }

    public string Currency { get; set; } = "NGN";

    public bool IsActive { get; set; } = true;

    /// <summary>Null → tenant default (<see cref="DefaultExpiryMonthsFallback"/>).</summary>
    public int? DefaultExpiryMonths { get; set; }

    public PackageRedemptionScope RedemptionScope { get; set; } = PackageRedemptionScope.AllLocations;

    public PackageSellScope SellScope { get; set; } = PackageSellScope.AllLocations;

    public string? RedemptionLocationIdsJson { get; set; }

    public string? SellLocationIdsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? DeactivatedAt { get; set; }

    public ICollection<PackageBenefit> Benefits { get; set; } = new List<PackageBenefit>();

    public int EffectiveExpiryMonths => DefaultExpiryMonths ?? DefaultExpiryMonthsFallback;

    public override string ToString() => $"PackageDefinition {Id}";
}

/// <summary>One benefit line: N sessions of a catalog service per purchase.</summary>
public sealed class PackageBenefit : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PackageDefinitionId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int QuantityGranted { get; set; }

    public int SortOrder { get; set; }
}

public static class LocationIdList
{
    public static string? Serialize(IReadOnlyCollection<Guid>? ids) =>
        ids is { Count: > 0 } ? JsonSerializer.Serialize(ids.Distinct().Order().ToArray()) : null;

    public static IReadOnlyList<Guid> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>();
        }
        catch (JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}