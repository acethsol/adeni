namespace Adeni.Infrastructure.Tenancy;

using System.Net;
using Adeni.Application.Tenancy;
using Adeni.Domain.Tenancy;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public sealed class BookingLinkService(AdeniDbContext db, IBookingLinkAccessQuery access, IBookingPreselectionQuery selection,
    IConfiguration configuration, TimeProvider clock) : IBookingLinkService
{
    public async Task<Result<BookingLinkView>> PublishAsync(Guid tenant, string actor, BookingLinkRequest request, CancellationToken ct)
    {
        if (!await access.CanPublishAsync(tenant, actor, ct)) return Result.Failure<BookingLinkView>(Error.Forbidden("Public page access is required."));
        var location = await db.BusinessLocations.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == request.LocationId && x.IsActive, ct);
        if (location == null || !await BookableAsync(tenant, ct)) return Result.Failure<BookingLinkView>(Error.NotFound("Public booking page"));
        if (!await selection.IsValidAsync(tenant, location.Id, request.ServiceId, request.PractitionerId, ct)) return Result.Failure<BookingLinkView>(Error.Validation("Invalid booking preselection."));
        var origin = configuration["App:PublicWebBaseUrl"] ?? "http://localhost:5190";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "") return Result.Failure<BookingLinkView>(new("calendar.unavailable"));
        var link = await db.Set<PublicBookingLink>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.LocationId == location.Id, ct);
        if (link == null)
        {
            // Collision after an old profile slug is reused must never reassign an existing permalink.
            var slug = location.Slug;
            if (await db.Set<PublicBookingLink>().AnyAsync(x => x.Slug == slug, ct)) slug = slug[..Math.Min(slug.Length, 45)] + "-" + Guid.NewGuid().ToString("N")[..12];
            link = new PublicBookingLink { Id = Guid.NewGuid(), TenantId = tenant, LocationId = location.Id, Slug = slug, CreatedAt = clock.GetUtcNow() };
            db.Add(link);
            try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { return Result.Failure<BookingLinkView>(Error.Conflict("Booking link publication changed; retry.")); }
        }
        var url = origin.TrimEnd('/') + "/book/" + Uri.EscapeDataString(link.Slug) + Parameters(request.ServiceId, request.PractitionerId);
        return Result.Success(new BookingLinkView(url, "<a href=\"" + WebUtility.HtmlEncode(url) + "\">Book with Adeni</a>"));
    }
    public async Task<Result<BookingLinkDestination>> ResolveAsync(string slug, Guid? service, Guid? practitioner, CancellationToken ct)
    {
        if (!SafeSlug(slug)) return Result.Failure<BookingLinkDestination>(Error.NotFound("Booking link"));
        var link = await db.Set<PublicBookingLink>().AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (link == null) return Result.Failure<BookingLinkDestination>(Error.NotFound("Booking link"));
        var location = await db.BusinessLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == link.LocationId && x.TenantId == link.TenantId && x.IsActive, ct);
        return await DestinationAsync(location, service, practitioner, ct);
    }
    public async Task<Result<BookingLinkDestination>> ContextAsync(string profileSlug, Guid? service, Guid? practitioner, CancellationToken ct)
    {
        if (!SafeSlug(profileSlug)) return Result.Failure<BookingLinkDestination>(Error.NotFound("Business"));
        return await DestinationAsync(await db.BusinessLocations.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == profileSlug && x.IsActive, ct), service, practitioner, ct);
    }
    private async Task<Result<BookingLinkDestination>> DestinationAsync(BusinessLocation? location, Guid? service, Guid? practitioner, CancellationToken ct)
    {
        if (location == null || !await BookableAsync(location.TenantId, ct)) return Result.Failure<BookingLinkDestination>(Error.NotFound("Business"));
        if (!await selection.IsValidAsync(location.TenantId, location.Id, service, practitioner, ct)) return Result.Failure<BookingLinkDestination>(Error.Validation("Invalid booking preselection."));
        return Result.Success(new BookingLinkDestination("/businesses/" + Uri.EscapeDataString(location.Slug) + Parameters(service, practitioner) + "#book", service, practitioner));
    }
    private async Task<bool> BookableAsync(Guid tenant, CancellationToken ct) => await db.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenant && x.Status == TenantStatus.Verified, ct)
        && !await db.BusinessProfiles.AsNoTracking().AnyAsync(x => x.TenantId == tenant && !x.PublicPageShowBook, ct);
    private static bool SafeSlug(string slug) => slug.Length is > 0 and <= 64 && slug.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    private static string Parameters(Guid? service, Guid? practitioner)
    {
        var parts = new List<string>();
        if (service != null) parts.Add("service=" + service.Value.ToString("D"));
        if (practitioner != null) parts.Add("practitioner=" + practitioner.Value.ToString("D"));
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }
}
