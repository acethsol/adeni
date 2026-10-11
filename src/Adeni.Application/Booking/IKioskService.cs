namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record KioskStatusResponse(bool HasPin, string? Slug, bool QueueEnabled);

public sealed record SetKioskPinRequest(string? Pin);

public sealed record VerifyKioskPinRequest(string Pin);

public sealed record VerifyKioskPinResponse(bool Ok);

public interface IKioskService
{
    Task<Result<KioskStatusResponse>> GetStatusAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<KioskStatusResponse>> SetPinAsync(
        Guid tenantId,
        SetKioskPinRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VerifyKioskPinResponse>> VerifyPinAsync(
        Guid tenantId,
        VerifyKioskPinRequest request,
        CancellationToken cancellationToken = default);
}
