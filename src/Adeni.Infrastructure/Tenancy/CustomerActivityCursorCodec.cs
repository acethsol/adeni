namespace Adeni.Infrastructure.Tenancy;

using System.Security.Cryptography;
using System.Text.Json;
using Adeni.Application.Tenancy;
using Microsoft.AspNetCore.DataProtection;

public sealed class CustomerActivityCursorCodec(IDataProtectionProvider provider) : ICustomerActivityCursorCodec
{
    private readonly IDataProtector protector = provider.CreateProtector("Adeni.CustomerActivity.v1");
    public string Encode(CustomerActivityCursor cursor) => protector.Protect(JsonSerializer.Serialize(cursor));
    public CustomerActivityCursor? Decode(string cursor)
    {
        try { return JsonSerializer.Deserialize<CustomerActivityCursor>(protector.Unprotect(cursor)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException) { return null; }
    }
}
