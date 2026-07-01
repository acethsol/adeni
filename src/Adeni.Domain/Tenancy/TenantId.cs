namespace Adeni.Domain.Tenancy;

using Adeni.Domain.Common;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public static Result<TenantId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<TenantId>(Error.Validation("Tenant id cannot be empty."))
            : Result.Success(new TenantId(value));

    public override string ToString() => Value.ToString();
}

public static class TenantIdExtensions
{
    public static Result<TenantId> ToTenantId(this Guid value) => TenantId.Create(value);
}
