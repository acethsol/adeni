namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Intake;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

[AttributeUsage(AttributeTargets.Class)]
public sealed class IntakePrivacyAttribute : ActionFilterAttribute
{
    public IntakePrivacyAttribute() => Order = -3000;
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        if (!context.ModelState.IsValid) context.Result = new BadRequestObjectResult(new { code = "intake.validation" });
    }
}

public abstract class IntakeControllerBase(IOptions<Auth0Options> auth) : ControllerBase
{
    protected string? Actor => User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub")
        : !auth.Value.Enabled ? Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault() : null;
    protected Guid? Tenant => Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var id) ? id : null;
    protected IActionResult Map<T>(Result<T> result) => ApiResults.FromResult(result, value => Ok(value), HttpContext);
}

[ApiController, IntakePrivacy, Route("api/v1/tenant/intake"), RequiresPortalPermission(PortalPermissions.Services)]
public sealed class IntakeTemplatesController(IIntakeTemplateService templates, IIntakeServiceBinding bindings, IOptions<Auth0Options> auth) : IntakeControllerBase(auth)
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record CreateBody(string Name);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record DraftBody(string Revision, IntakeDefinition Definition);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record RevisionBody(string Revision);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record MetadataBody(string Name, bool Active, string Revision);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record BindingBody(Guid? TemplateId, string Revision);
    [HttpGet("templates")]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.ListAsync(tenant, actor, page, ct));
    [HttpPost("templates")]
    public async Task<IActionResult> Create(CreateBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.CreateAsync(tenant, actor, body.Name, ct));
    [HttpGet("templates/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.GetAsync(tenant, actor, id, ct));
    [HttpPut("templates/{id:guid}/draft"), RequestSizeLimit(65536)]
    public async Task<IActionResult> Save(Guid id, DraftBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.SaveDraftAsync(tenant, actor, id, body.Revision, body.Definition, ct));
    [HttpPost("templates/{id:guid}/draft")]
    public async Task<IActionResult> Clone(Guid id, RevisionBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.CreateDraftAsync(tenant, actor, id, body.Revision, ct));
    [HttpPost("templates/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, RevisionBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.PublishAsync(tenant, actor, id, body.Revision, ct));
    [HttpPatch("templates/{id:guid}")]
    public async Task<IActionResult> Metadata(Guid id, MetadataBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await templates.UpdateAsync(tenant, actor, id, body.Name, body.Active, body.Revision, ct));
    [HttpGet("services/{id:guid}")]
    public async Task<IActionResult> Binding(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await bindings.GetAsync(tenant, actor, id, ct));
    [HttpPut("services/{id:guid}")]
    public async Task<IActionResult> Bind(Guid id, BindingBody body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await bindings.SetAsync(tenant, actor, id, body.TemplateId, body.Revision, ct));
}

[ApiController, IntakePrivacy, Route("api/v1/tenant/intake/bookings"), RequiresPortalPermission(PortalPermissions.Bookings, PortalPermissions.BookingsSelf)]
public sealed class IntakeReviewController(IIntakeSubmissionService intake, IOptions<Auth0Options> auth) : IntakeControllerBase(auth)
{
    [HttpGet("{bookingId:guid}")]
    public async Task<IActionResult> Status(Guid bookingId, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await intake.StaffStatusesAsync(tenant, actor, bookingId, ct));
    [HttpPost("statuses"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Batch([FromBody] Guid[] bookingIds, CancellationToken ct)
    {
        if (Actor is not { } actor || Tenant is not { } tenant) return Unauthorized();
        if (bookingIds.Length > 50 || bookingIds.Distinct().Count() != bookingIds.Length) return BadRequest(new { code = "intake.validation" });
        var rows = new Dictionary<Guid, IReadOnlyList<IntakeAssignmentStatus>>();
        foreach (var id in bookingIds)
        {
            var result = await intake.StaffStatusesAsync(tenant, actor, id, ct);
            if (result.IsFailure) return ApiErrorResponseMapper.ToActionResult(result.Error, HttpContext);
            rows[id] = result.Value!;
        }
        return Ok(rows);
    }
    [HttpGet("{bookingId:guid}/forms/{assignmentId:guid}/submission")]
    public async Task<IActionResult> Review(Guid bookingId, Guid assignmentId, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await intake.ReviewAsync(tenant, actor, bookingId, assignmentId, ct));
}

[ApiController, IntakePrivacy, Route("api/v1/bookings/{bookingId:guid}/intake")]
public sealed class CustomerIntakeController(IIntakeSubmissionService intake, IOptions<Auth0Options> auth) : IntakeControllerBase(auth)
{
    [HttpGet]
    public async Task<IActionResult> Status(Guid bookingId, CancellationToken ct) => Actor is not { } actor ? Unauthorized() : Map(await intake.CustomerStatusesAsync(actor, bookingId, ct));
    [HttpGet("{assignmentId:guid}")]
    public async Task<IActionResult> Get(Guid bookingId, Guid assignmentId, CancellationToken ct) => Actor is not { } actor ? Unauthorized() : Map(await intake.CustomerReadAsync(actor, bookingId, assignmentId, ct));
    [HttpPost("{assignmentId:guid}/submission"), RequestSizeLimit(65536)]
    public async Task<IActionResult> Submit(Guid bookingId, Guid assignmentId, SubmitIntakeRequest body, CancellationToken ct) => Actor is not { } actor ? Unauthorized() : Map(await intake.SubmitAsync(actor, bookingId, assignmentId, Request.Headers["Idempotency-Key"].ToString(), body, ct));
}

[ApiController, IntakePrivacy, Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy), Route("api/v1/admin")]
public sealed class IntakePrivacyController(IIntakeRetentionService retention, IIntakeSubmissionService intake) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)] public sealed record HoldBody(string Category);
    [HttpGet("customers/{id:guid}/intake-export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) => Ok(await intake.PrivacyExportAsync(id, User.FindFirstValue("sub")!, ct));
    [HttpPost("intake/tenants/{tenantId:guid}/submissions/{id:guid}/hold")]
    public async Task<IActionResult> Hold(Guid tenantId, Guid id, HoldBody body, CancellationToken ct)
    {
        var result = await retention.PlaceHoldAsync(tenantId, id, User.FindFirstValue("sub")!, body.Category, ct);
        return result.IsSuccess ? NoContent() : ApiErrorResponseMapper.ToActionResult(result.Error, HttpContext);
    }
    [HttpPost("intake/tenants/{tenantId:guid}/submissions/{id:guid}/hold/release")]
    public async Task<IActionResult> Release(Guid tenantId, Guid id, CancellationToken ct)
    {
        var result = await retention.ReleaseHoldAsync(tenantId, id, User.FindFirstValue("sub")!, ct);
        return result.IsSuccess ? NoContent() : ApiErrorResponseMapper.ToActionResult(result.Error, HttpContext);
    }
}
