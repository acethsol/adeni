namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Common;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

/// <summary>Published content is read-only. This service does not enable customer response collection.</summary>
public sealed class IntakeTemplateService(AdeniDbContext db, IIntakeManagementAccessQuery access) : IIntakeTemplateService
{
    public async Task<Result<IntakeTemplateDetail>> CreateAsync(Guid tenantId, string auth0Sub, string name, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, auth0Sub, cancellationToken)) return Denied();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return Invalid();
        var now = DateTimeOffset.UtcNow;
        var template = new IntakeFormTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = name.Trim(), CreatedAt = now, UpdatedAt = now };
        db.Add(template);
        db.Add(new IntakeFormVersion { Id = Guid.NewGuid(), TenantId = tenantId, TemplateId = template.Id, VersionNumber = 1, Title = name.Trim(), CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await DetailAsync(template, cancellationToken));
    }

    public async Task<Result<IntakeTemplateDetail>> GetAsync(Guid tenantId, string auth0Sub, Guid templateId, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, auth0Sub, cancellationToken)) return Denied();
        var template = await TemplateAsync(tenantId, templateId, cancellationToken);
        return template is null ? Missing() : Result.Success(await DetailAsync(template, cancellationToken));
    }

    public async Task<Result<IntakeTemplateDetail>> SaveDraftAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision, IntakeDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, auth0Sub, cancellationToken)) return Denied();
        if (IntakeValidation.Definition(definition).IsFailure) return Invalid();
        var template = await TemplateAsync(tenantId, templateId, cancellationToken);
        if (template is null) return Missing();
        if (revision != Convert.ToBase64String(template.RowVersion)) return Conflict();
        var draft = await db.Set<IntakeFormVersion>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.TemplateId == templateId && x.PublishedAt == null, cancellationToken);
        if (draft is null) return Conflict();
        var fields = await db.Set<IntakeFormField>().Where(x => x.TenantId == tenantId && x.VersionId == draft.Id).ToListAsync(cancellationToken);
        var choices = await db.Set<IntakeFieldChoice>().Where(x => x.TenantId == tenantId && x.VersionId == draft.Id).ToListAsync(cancellationToken);
        var requestedFields = definition.Fields.Select(x => x.Id).ToHashSet();
        var requestedChoices = definition.Fields.SelectMany(x => x.Choices).Select(x => x.Id).ToHashSet();
        // Do not allow a field/option identity to be moved between versions or other fields.
        var foreignField = await db.Set<IntakeFormField>().AnyAsync(x => requestedFields.Contains(x.Id) && (x.TenantId != tenantId || x.VersionId != draft.Id), cancellationToken);
        var foreignChoice = await db.Set<IntakeFieldChoice>().AnyAsync(x => requestedChoices.Contains(x.Id) && (x.TenantId != tenantId || x.VersionId != draft.Id), cancellationToken);
        if (foreignField || foreignChoice || definition.Fields.Any(f => f.Choices.Any(c => choices.Any(old => old.Id == c.Id && old.FieldId != f.Id)))) return Invalid();
        db.RemoveRange(choices.Where(x => !requestedChoices.Contains(x.Id)));
        db.RemoveRange(fields.Where(x => !requestedFields.Contains(x.Id)));
        for (var order = 0; order < definition.Fields.Count; order++)
        {
            var input = definition.Fields[order];
            var field = fields.SingleOrDefault(x => x.Id == input.Id);
            if (field is null) { field = new() { Id = input.Id, TenantId = tenantId, VersionId = draft.Id }; db.Add(field); }
            field.SortOrder = order; field.FieldType = input.Type; field.Label = input.Label; field.HelpText = input.HelpText; field.IsRequired = input.Required;
            for (var i = 0; i < input.Choices.Count; i++)
            {
                var option = input.Choices[i];
                var row = choices.SingleOrDefault(x => x.Id == option.Id);
                if (row is null) { row = new() { Id = option.Id, TenantId = tenantId, VersionId = draft.Id, FieldId = input.Id }; db.Add(row); }
                row.SortOrder = i; row.Label = option.Label;
            }
        }
        draft.Title = definition.Title; draft.Description = definition.Description; draft.Instructions = definition.Instructions;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        return await SaveAsync(template, cancellationToken);
    }

    public async Task<Result<IntakeTemplateDetail>> PublishAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, auth0Sub, cancellationToken)) return Denied();
        var template = await TemplateAsync(tenantId, templateId, cancellationToken);
        if (template is null) return Missing();
        if (revision != Convert.ToBase64String(template.RowVersion)) return Conflict();
        var draft = await db.Set<IntakeFormVersion>().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.TemplateId == templateId && x.PublishedAt == null, cancellationToken);
        if (draft is null) return Conflict();
        if (IntakeValidation.Definition(await DefinitionAsync(draft, cancellationToken)).IsFailure) return Invalid();
        draft.PublishedAt = DateTimeOffset.UtcNow;
        template.CurrentPublishedVersionId = draft.Id; template.UpdatedAt = draft.PublishedAt.Value;
        return await SaveAsync(template, cancellationToken);
    }

    public async Task<Result<IntakeTemplateDetail>> CreateDraftAsync(Guid tenantId, string auth0Sub, Guid templateId, string revision, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, auth0Sub, cancellationToken)) return Denied();
        var template = await TemplateAsync(tenantId, templateId, cancellationToken);
        if (template is null) return Missing();
        if (revision != Convert.ToBase64String(template.RowVersion) || template.CurrentPublishedVersionId is null) return Conflict();
        if (await db.Set<IntakeFormVersion>().AnyAsync(x => x.TenantId == tenantId && x.TemplateId == templateId && x.PublishedAt == null, cancellationToken)) return Conflict();
        var published = await db.Set<IntakeFormVersion>().SingleAsync(x => x.TenantId == tenantId && x.Id == template.CurrentPublishedVersionId, cancellationToken);
        var content = await DefinitionAsync(published, cancellationToken);
        var draft = new IntakeFormVersion { Id = Guid.NewGuid(), TenantId = tenantId, TemplateId = templateId, VersionNumber = published.VersionNumber + 1,
            Title = content.Title, Description = content.Description, Instructions = content.Instructions, CreatedAt = DateTimeOffset.UtcNow };
        db.Add(draft);
        for (var i = 0; i < content.Fields.Count; i++)
        {
            var field = content.Fields[i]; var id = Guid.NewGuid();
            db.Add(new IntakeFormField { Id = id, TenantId = tenantId, VersionId = draft.Id, SortOrder = i, FieldType = field.Type, Label = field.Label, HelpText = field.HelpText, IsRequired = field.Required });
            for (var j = 0; j < field.Choices.Count; j++) db.Add(new IntakeFieldChoice { Id = Guid.NewGuid(), TenantId = tenantId, VersionId = draft.Id, FieldId = id, SortOrder = j, Label = field.Choices[j].Label });
        }
        template.UpdatedAt = draft.CreatedAt;
        return await SaveAsync(template, cancellationToken);
    }

    private async Task<Result<IntakeTemplateDetail>> SaveAsync(IntakeFormTemplate template, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { db.ChangeTracker.Clear(); return Conflict(); }
        return Result.Success(await DetailAsync(template, ct));
    }
    private Task<IntakeFormTemplate?> TemplateAsync(Guid tenant, Guid id, CancellationToken ct) =>
        db.Set<IntakeFormTemplate>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
    private async Task<IntakeTemplateDetail> DetailAsync(IntakeFormTemplate template, CancellationToken ct)
    {
        var versions = await db.Set<IntakeFormVersion>().AsNoTracking().Where(x => x.TenantId == template.TenantId && x.TemplateId == template.Id).ToArrayAsync(ct);
        var draft = versions.SingleOrDefault(x => x.PublishedAt == null);
        var published = versions.SingleOrDefault(x => x.Id == template.CurrentPublishedVersionId);
        return new(template.Id, template.IsActive, Convert.ToBase64String(template.RowVersion), published?.Id, draft?.Id,
            draft is null ? null : await DefinitionAsync(draft, ct), published is null ? null : await DefinitionAsync(published, ct));
    }
    private async Task<IntakeDefinition> DefinitionAsync(IntakeFormVersion version, CancellationToken ct)
    {
        var fields = await db.Set<IntakeFormField>().AsNoTracking().Where(x => x.TenantId == version.TenantId && x.VersionId == version.Id).OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        var choices = await db.Set<IntakeFieldChoice>().AsNoTracking().Where(x => x.TenantId == version.TenantId && x.VersionId == version.Id).OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        return new(version.Title, version.Description, version.Instructions, fields.Select(x => new IntakeFieldDefinition(x.Id, x.FieldType, x.Label, x.HelpText, x.IsRequired,
            choices.Where(c => c.FieldId == x.Id).Select(c => new IntakeChoiceDefinition(c.Id, c.Label)).ToArray())).ToArray());
    }
    private static Result<IntakeTemplateDetail> Denied() => Result.Failure<IntakeTemplateDetail>(ErrorCodes.PermissionDeniedError());
    private static Result<IntakeTemplateDetail> Invalid() => Result.Failure<IntakeTemplateDetail>(new Error("intake.validation"));
    private static Result<IntakeTemplateDetail> Conflict() => Result.Failure<IntakeTemplateDetail>(new Error("intake.conflict"));
    private static Result<IntakeTemplateDetail> Missing() => Result.Failure<IntakeTemplateDetail>(Error.NotFound("Intake form"));
}
