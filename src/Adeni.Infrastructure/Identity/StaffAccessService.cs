namespace Adeni.Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Abstractions;
using Adeni.Application.Auth;
using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class StaffAccessService(
    AdeniDbContext dbContext,
    INotificationDispatcher notifications,
    IConfiguration configuration,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    ILogger<StaffAccessService> logger) : IStaffAccessService
{
    private static readonly TimeSpan InviteTtl = TimeSpan.FromDays(7);

    public Task<Result<StaffPortalInviteResponse>> InviteStaffMemberAsync(
        Guid tenantId,
        Guid staffMemberId,
        CreateStaffInviteRequest request,
        string invitedByAuth0Sub,
        CancellationToken cancellationToken = default) =>
        CreateInviteAsync(tenantId, staffMemberId, request, invitedByAuth0Sub, cancellationToken);

    public Task<Result<StaffPortalInviteResponse>> InviteAccessOnlyAsync(
        Guid tenantId,
        CreateStaffInviteRequest request,
        string invitedByAuth0Sub,
        CancellationToken cancellationToken = default) =>
        CreateInviteAsync(tenantId, staffMemberId: null, request, invitedByAuth0Sub, cancellationToken);

    public async Task<IReadOnlyList<StaffPortalInviteResponse>> ListInvitesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var rows = await dbContext.StaffPortalInvites
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return rows.Select(r => ToResponse(r, now)).ToArray();
    }

    public async Task<Result<StaffPortalInviteResponse>> ResendAsync(
        Guid tenantId,
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var invite = await dbContext.StaffPortalInvites
            .FirstOrDefaultAsync(x => x.Id == inviteId && x.TenantId == tenantId, cancellationToken);

        if (invite is null)
        {
            return Result.Failure<StaffPortalInviteResponse>(Error.NotFound("Invite"));
        }

        if (invite.Status is not (StaffPortalInviteStatuses.Pending or StaffPortalInviteStatuses.Expired))
        {
            return Result.Failure<StaffPortalInviteResponse>(ErrorCodes.StaffInviteInvalidError());
        }

        var (rawToken, tokenHash) = GenerateToken();
        invite.TokenHash = tokenHash;
        invite.Status = StaffPortalInviteStatuses.Pending;
        invite.ExpiresAt = DateTimeOffset.UtcNow.Add(InviteTtl);
        invite.AcceptedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        await SendInviteEmailAsync(invite, rawToken, cancellationToken);
        logger.LogInformation(
            "Staff portal invite resent {InviteId} for tenant {TenantId} to {Email}",
            invite.Id,
            tenantId,
            PiiMasker.MaskEmail(invite.Email));

        await WriteAuditAsync(
            invite.InvitedByAuth0Sub ?? "system",
            AuditActions.StaffInviteResent,
            "staff_portal_invite",
            invite.Id.ToString(),
            $"{{\"email\":\"{PiiMasker.MaskEmail(invite.Email)}\"}}",
            cancellationToken);

        return Result.Success(ToResponse(invite, DateTimeOffset.UtcNow));
    }

    public async Task<Result> RevokeAsync(
        Guid tenantId,
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        var invite = await dbContext.StaffPortalInvites
            .FirstOrDefaultAsync(x => x.Id == inviteId && x.TenantId == tenantId, cancellationToken);

        if (invite is null)
        {
            return Result.Failure(Error.NotFound("Invite"));
        }

        if (invite.Status == StaffPortalInviteStatuses.Accepted)
        {
            return Result.Failure(ErrorCodes.StaffInviteInvalidError());
        }

        invite.Status = StaffPortalInviteStatuses.Revoked;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Staff portal invite revoked {InviteId} for tenant {TenantId}",
            invite.Id,
            tenantId);

        await WriteAuditAsync(
            invite.InvitedByAuth0Sub ?? "system",
            AuditActions.StaffInviteRevoked,
            "staff_portal_invite",
            invite.Id.ToString(),
            null,
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result<AcceptStaffInviteResponse>> AcceptAsync(
        string auth0Sub,
        string? authenticatedEmail,
        AcceptStaffInviteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.AuthRequiredError());
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.StaffInviteInvalidError());
        }

        var existingMembership = await dbContext.BusinessUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (existingMembership is not null)
        {
            return Result.Failure<AcceptStaffInviteResponse>(
                Error.Conflict("This login is already linked to a business."));
        }

        var tokenHash = HashToken(request.Token.Trim());
        var invite = await dbContext.StaffPortalInvites
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (invite is null
            || invite.Status is StaffPortalInviteStatuses.Revoked or StaffPortalInviteStatuses.Accepted)
        {
            return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.StaffInviteInvalidError());
        }

        if (invite.ExpiresAt < DateTimeOffset.UtcNow
            || invite.Status == StaffPortalInviteStatuses.Expired)
        {
            invite.Status = StaffPortalInviteStatuses.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.StaffInviteExpiredError());
        }

        if (!string.IsNullOrWhiteSpace(authenticatedEmail)
            && !EmailsMatch(authenticatedEmail, invite.Email))
        {
            return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.StaffInviteEmailMismatchError());
        }

        if (invite.StaffMemberId is { } staffId)
        {
            var staffExists = await dbContext.StaffMembers
                .IgnoreQueryFilters()
                .AnyAsync(s => s.Id == staffId && s.TenantId == invite.TenantId, cancellationToken);
            if (!staffExists)
            {
                return Result.Failure<AcceptStaffInviteResponse>(ErrorCodes.StaffInviteInvalidError());
            }

            var alreadyLinked = await dbContext.BusinessUsers
                .IgnoreQueryFilters()
                .AnyAsync(
                    b => b.TenantId == invite.TenantId && b.StaffMemberId == staffId,
                    cancellationToken);
            if (alreadyLinked)
            {
                return Result.Failure<AcceptStaffInviteResponse>(
                    Error.Conflict("That team member already has portal access."));
            }
        }

        var now = DateTimeOffset.UtcNow;
        var businessUser = new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = invite.TenantId,
            Auth0Sub = auth0Sub,
            Role = PortalPermissionRoles.Normalize(invite.PermissionRole),
            StaffMemberId = invite.StaffMemberId,
            CreatedAt = now,
        };

        invite.Status = StaffPortalInviteStatuses.Accepted;
        invite.AcceptedAt = now;
        dbContext.BusinessUsers.Add(businessUser);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Staff portal invite accepted {InviteId} tenant {TenantId} role {Role}",
            invite.Id,
            invite.TenantId,
            businessUser.Role);

        await WriteAuditAsync(
            auth0Sub,
            AuditActions.StaffInviteAccepted,
            "business_user",
            businessUser.Id.ToString(),
            $"{{\"inviteId\":\"{invite.Id}\",\"role\":\"{businessUser.Role}\"}}",
            cancellationToken);

        return Result.Success(new AcceptStaffInviteResponse(
            businessUser.Id,
            businessUser.Auth0Sub,
            businessUser.TenantId,
            businessUser.Role,
            businessUser.StaffMemberId));
    }

    public async Task<IReadOnlyList<TenantAccessUserResponse>> ListUsersAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var users = await dbContext.BusinessUsers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return users.Select(ToUserResponse).ToArray();
    }

    public async Task<Result<TenantAccessUserResponse>> UpdateRoleAsync(
        Guid tenantId,
        Guid businessUserId,
        UpdateAccessUserRequest request,
        string actorAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.BusinessUsers
            .FirstOrDefaultAsync(x => x.Id == businessUserId && x.TenantId == tenantId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<TenantAccessUserResponse>(Error.NotFound("Business user"));
        }

        var newRole = request.PermissionRole?.Trim().ToLowerInvariant();
        if (!PortalPermissionRoles.IsValid(newRole))
        {
            return Result.Failure<TenantAccessUserResponse>(
                Error.Validation("A valid permission role is required."));
        }

        var currentRole = PortalPermissionRoles.Normalize(user.Role);
        if (currentRole == newRole)
        {
            return Result.Success(ToUserResponse(user));
        }

        if (currentRole == PortalPermissionRoles.Owner && newRole != PortalPermissionRoles.Owner)
        {
            var ownerCount = await CountOwnersAsync(tenantId, cancellationToken);
            if (ownerCount <= 1)
            {
                return Result.Failure<TenantAccessUserResponse>(ErrorCodes.StaffLastOwnerError());
            }
        }

        // Owners are created via registration — do not promote via this path.
        if (newRole == PortalPermissionRoles.Owner && currentRole != PortalPermissionRoles.Owner)
        {
            return Result.Failure<TenantAccessUserResponse>(
                Error.Validation("Owner access is created through business registration, not role edits."));
        }

        var previous = currentRole;
        user.Role = newRole!;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Staff access role changed {BusinessUserId} tenant {TenantId} {From} -> {To} by {Actor}",
            user.Id,
            tenantId,
            previous,
            newRole,
            actorAuth0Sub);

        await WriteAuditAsync(
            actorAuth0Sub,
            AuditActions.StaffAccessRoleChanged,
            "business_user",
            user.Id.ToString(),
            JsonSerializer.Serialize(new { from = previous, to = newRole }),
            cancellationToken);

        return Result.Success(ToUserResponse(user));
    }

    public async Task<Result> RevokeLoginAsync(
        Guid tenantId,
        Guid businessUserId,
        string actorAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.BusinessUsers
            .FirstOrDefaultAsync(x => x.Id == businessUserId && x.TenantId == tenantId, cancellationToken);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Business user"));
        }

        if (PortalPermissionRoles.Normalize(user.Role) == PortalPermissionRoles.Owner)
        {
            var ownerCount = await CountOwnersAsync(tenantId, cancellationToken);
            if (ownerCount <= 1)
            {
                return Result.Failure(ErrorCodes.StaffLastOwnerError());
            }
        }

        var revokedRole = PortalPermissionRoles.Normalize(user.Role);
        dbContext.BusinessUsers.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Staff access revoked {BusinessUserId} tenant {TenantId} by {Actor}",
            businessUserId,
            tenantId,
            actorAuth0Sub);

        await WriteAuditAsync(
            actorAuth0Sub,
            AuditActions.StaffAccessRevoked,
            "business_user",
            businessUserId.ToString(),
            $"{{\"role\":\"{revokedRole}\"}}",
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result<StaffPortalInviteResponse>> CreateInviteAsync(
        Guid tenantId,
        Guid? staffMemberId,
        CreateStaffInviteRequest request,
        string invitedByAuth0Sub,
        CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (email is null)
        {
            return Result.Failure<StaffPortalInviteResponse>(
                Error.Validation("A valid email address is required."));
        }

        string permissionRole;
        if (staffMemberId is { } sid)
        {
            var staff = await dbContext.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sid && s.TenantId == tenantId, cancellationToken);
            if (staff is null)
            {
                return Result.Failure<StaffPortalInviteResponse>(Error.NotFound("Staff member"));
            }

            var linked = await dbContext.BusinessUsers
                .AsNoTracking()
                .AnyAsync(b => b.TenantId == tenantId && b.StaffMemberId == sid, cancellationToken);
            if (linked)
            {
                return Result.Failure<StaffPortalInviteResponse>(
                    Error.Conflict("That team member already has portal access."));
            }

            permissionRole = ResolveInviteRole(request.PermissionRole, staff.RoleKey);
        }
        else
        {
            permissionRole = ResolveInviteRole(request.PermissionRole, floorRoleKey: null);
        }

        if (permissionRole == PortalPermissionRoles.Owner)
        {
            return Result.Failure<StaffPortalInviteResponse>(
                Error.Validation("Owner access is created through business registration, not invites."));
        }

        var pendingSameEmail = await dbContext.StaffPortalInvites
            .AnyAsync(
                x => x.TenantId == tenantId
                    && x.Email == email
                    && x.Status == StaffPortalInviteStatuses.Pending
                    && x.ExpiresAt >= DateTimeOffset.UtcNow,
                cancellationToken);
        if (pendingSameEmail)
        {
            return Result.Failure<StaffPortalInviteResponse>(
                Error.Conflict("A pending invite already exists for that email."));
        }

        var (rawToken, tokenHash) = GenerateToken();
        var now = DateTimeOffset.UtcNow;
        var invite = new StaffPortalInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            PermissionRole = permissionRole,
            StaffMemberId = staffMemberId,
            TokenHash = tokenHash,
            Status = StaffPortalInviteStatuses.Pending,
            ExpiresAt = now.Add(InviteTtl),
            CreatedAt = now,
            InvitedByAuth0Sub = invitedByAuth0Sub,
        };

        dbContext.StaffPortalInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SendInviteEmailAsync(invite, rawToken, cancellationToken);

        logger.LogInformation(
            "Staff portal invite created {InviteId} tenant {TenantId} role {Role} email {Email}",
            invite.Id,
            tenantId,
            permissionRole,
            PiiMasker.MaskEmail(email));

        await WriteAuditAsync(
            invitedByAuth0Sub,
            AuditActions.StaffInviteCreated,
            "staff_portal_invite",
            invite.Id.ToString(),
            $"{{\"email\":\"{PiiMasker.MaskEmail(email)}\",\"role\":\"{permissionRole}\"}}",
            cancellationToken);

        return Result.Success(ToResponse(invite, DateTimeOffset.UtcNow));
    }

    private Task<int> CountOwnersAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.BusinessUsers
            .AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId && x.Role == PortalPermissionRoles.Owner,
                cancellationToken);

    private async Task WriteAuditAsync(
        string actorId,
        string action,
        string entityType,
        string entityId,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                action,
                entityType,
                entityId,
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow,
                metadataJson),
            cancellationToken);
    }

    private async Task SendInviteEmailAsync(
        StaffPortalInvite invite,
        string rawToken,
        CancellationToken cancellationToken)
    {
        var portalBase = configuration["Portal:PublicBaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5173";
        var acceptUrl = $"{portalBase}/accept-invite?token={Uri.EscapeDataString(rawToken)}";
        var tenantName = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == invite.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "your business";

        await notifications.SendAsync(
            new NotificationMessage(
                Channel: "email",
                RecipientKey: PiiMasker.MaskEmail(invite.Email),
                Subject: $"You're invited to {tenantName} on Adeni",
                Body:
                $"You've been invited to the Adeni business portal for {tenantName} "
                + $"as {invite.PermissionRole}. Open this link to accept (expires {invite.ExpiresAt:u}): {acceptUrl}"
                + $" | To: {invite.Email}",
                TenantId: invite.TenantId),
            cancellationToken);
    }

    private static string ResolveInviteRole(string? requested, string? floorRoleKey)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var normalized = requested.Trim().ToLowerInvariant();
            if (PortalPermissionRoles.IsValid(normalized) && normalized != PortalPermissionRoles.Owner)
            {
                return normalized;
            }
        }

        return PortalPermissionRoles.DefaultForFloorRole(floorRoleKey);
    }

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.Contains('@') || trimmed.Length > 320)
        {
            return null;
        }

        return trimmed;
    }

    private static bool EmailsMatch(string a, string b) =>
        string.Equals(NormalizeEmail(a), NormalizeEmail(b), StringComparison.Ordinal);

    private static (string Raw, string Hash) GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, HashToken(raw));
    }

    internal static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static StaffPortalInviteResponse ToResponse(StaffPortalInvite invite, DateTimeOffset now)
    {
        var status = invite.Status == StaffPortalInviteStatuses.Pending && invite.ExpiresAt < now
            ? StaffPortalInviteStatuses.Expired
            : invite.Status;

        return new StaffPortalInviteResponse(
            invite.Id,
            PiiMasker.MaskEmail(invite.Email),
            invite.PermissionRole,
            invite.StaffMemberId,
            status,
            invite.ExpiresAt);
    }

    private static TenantAccessUserResponse ToUserResponse(BusinessUser user) =>
        new(
            user.Id,
            PortalPermissionRoles.Normalize(user.Role),
            user.StaffMemberId,
            user.CreatedAt);
}
