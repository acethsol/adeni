namespace Adeni.Application.Tenancy;

using Adeni.Domain.Common;

public sealed record BookingLinkRequest(Guid LocationId, Guid? ServiceId = null, Guid? PractitionerId = null);
public sealed record BookingLinkView(string Url, string Html);
public sealed record BookingLinkDestination(string Path, Guid? ServiceId, Guid? PractitionerId);
public interface IBookingLinkAccessQuery
{
    Task<bool> CanPublishAsync(Guid tenant, string actor, CancellationToken ct);
}
public interface IBookingPreselectionQuery
{
    Task<bool> IsValidAsync(Guid tenant, Guid location, Guid? service, Guid? practitioner, CancellationToken ct);
}
public interface IBookingLinkService
{
    Task<Result<BookingLinkView>> PublishAsync(Guid tenant, string actor, BookingLinkRequest request, CancellationToken ct);
    Task<Result<BookingLinkDestination>> ResolveAsync(string slug, Guid? service, Guid? practitioner, CancellationToken ct);
    Task<Result<BookingLinkDestination>> ContextAsync(string profileSlug, Guid? service, Guid? practitioner, CancellationToken ct);
}
