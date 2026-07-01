namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class AuthSyncService(AdeniDbContext dbContext) : IAuthSyncService
{
    public async Task<Result<UserProfileResponse>> SyncAsync(
        SyncAuthUserRequest request,
        string? authenticatedAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Auth0Sub))
        {
            return Result.Failure<UserProfileResponse>(Error.Validation("Auth0 subject is required."));
        }

        if (authenticatedAuth0Sub is not null
            && !string.Equals(authenticatedAuth0Sub, request.Auth0Sub, StringComparison.Ordinal))
        {
            return Result.Failure<UserProfileResponse>(Error.Forbidden("Token subject does not match request."));
        }

        var role = NormalizeRole(request.RoleHint);

        return role switch
        {
            AdeniRoles.Business => await SyncBusinessUserAsync(request, cancellationToken),
            _ => await SyncCustomerAsync(request, cancellationToken)
        };
    }

    private async Task<Result<UserProfileResponse>> SyncCustomerAsync(
        SyncAuthUserRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Auth0Sub == request.Auth0Sub, cancellationToken);

        if (existing is null)
        {
            existing = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = request.Auth0Sub,
                Name = request.Name ?? string.Empty,
                Email = request.Email,
                Phone = request.Phone,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Customers.Add(existing);
        }
        else
        {
            existing.Name = request.Name ?? existing.Name;
            existing.Email = request.Email ?? existing.Email;
            existing.Phone = request.Phone ?? existing.Phone;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserProfileResponse(
            existing.Id,
            existing.Auth0Sub,
            AdeniRoles.Customer,
            existing.Name,
            existing.Email,
            null));
    }

    private async Task<Result<UserProfileResponse>> SyncBusinessUserAsync(
        SyncAuthUserRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.BusinessUsers
            .Include(b => b.Tenant)
            .FirstOrDefaultAsync(b => b.Auth0Sub == request.Auth0Sub, cancellationToken);

        if (existing is null)
        {
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = request.Name ?? "New business",
                Status = TenantStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow
            };

            existing = new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Auth0Sub = request.Auth0Sub,
                Role = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
                Tenant = tenant
            };

            dbContext.Tenants.Add(tenant);
            dbContext.BusinessUsers.Add(existing);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserProfileResponse(
            existing.Id,
            existing.Auth0Sub,
            AdeniRoles.Business,
            existing.Tenant?.Name,
            request.Email,
            existing.TenantId));
    }

    private static string NormalizeRole(string? roleHint) =>
        roleHint?.Trim().ToLowerInvariant() switch
        {
            AdeniRoles.Business => AdeniRoles.Business,
            AdeniRoles.Admin => AdeniRoles.Admin,
            _ => AdeniRoles.Customer
        };
}
