namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

/// <summary>Published content is read-only. This service does not enable customer response collection.</summary>
public sealed class IntakeTemplateService(AdeniDbContext db, IIntakeManagementAccessQuery access, IAuditLogWriter? audit = null, ICorrelationContext? correlation = null) : IIntakeTemplateService
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
        await AuditAsync(auth0Sub, "intake.template_created", template.Id, cancellationToken);
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
        var sections = await db.Set<IntakeFormSection>().Where(x => x.TenantId == tenantId && x.VersionId == draft.Id).ToListAsync(cancellationToken);
        var fields = await db.Set<IntakeFormField>().Where(x => x.TenantId == tenantId && x.VersionId == draft.Id).ToListAsync(cancellationToken);
        var choices = await db.Set<IntakeFieldChoice>().Where(x => x.TenantId == tenantId && x.VersionId == draft.Id).ToListAsync(cancellationToken);
        var requestedSections = (definition.Sections ?? []).Select(x => x.Id).ToHashSet();
        var requestedFields = definition.Fields.Select(x => x.Id).ToHashSet();
        var requestedChoices = definition.Fields.SelectMany(x => x.Choices).Select(x => x.Id).ToHashSet();
        // Do not allow a field/option identity to be moved between versions or other fields.
        var foreignSection = await db.Set<IntakeFormSection>().AnyAsync(x => requestedSections.Contains(x.Id) && (x.TenantId != tenantId || x.VersionId != draft.Id), cancellationToken);
        var foreignField = await db.Set<IntakeFormField>().AnyAsync(x => requestedFields.Contains(x.Id) && (x.TenantId != tenantId || x.VersionId != draft.Id), cancellationToken);
        var foreignChoice = await db.Set<IntakeFieldChoice>().AnyAsync(x => requestedChoices.Contains(x.Id) && (x.TenantId != tenantId || x.VersionId != draft.Id), cancellationToken);
        if (foreignSection || foreignField || foreignChoice || definition.Fields.Any(f => f.Choices.Any(c => choices.Any(old => old.Id == c.Id && old.FieldId != f.Id)))) return Invalid();
        db.RemoveRange(choices.Where(x => !requestedChoices.Contains(x.Id)));
        var sectionInputs = definition.Sections ?? [];
        for (var order = 0; order < sectionInputs.Count; order++)
        {
            var input = sectionInputs[order];
            var row = sections.SingleOrDefault(x => x.Id == input.Id);
            if (row is null) { row = new() { Id = input.Id, TenantId = tenantId, VersionId = draft.Id }; db.Add(row); }
            row.SortOrder = order; row.Title = input.Title; row.Description = input.Description;
        }
        for (var order = 0; order < definition.Fields.Count; order++)
        {
            var input = definition.Fields[order];
            var field = fields.SingleOrDefault(x => x.Id == input.Id);
            if (field is null) { field = new() { Id = input.Id, TenantId = tenantId, VersionId = draft.Id }; db.Add(field); }
            field.SortOrder = order; field.FieldType = input.Type; field.Label = input.Label; field.HelpText = input.HelpText; field.IsRequired = input.Required; field.SectionId = input.SectionId;
            for (var i = 0; i < input.Choices.Count; i++)
            {
                var option = input.Choices[i];
                var row = choices.SingleOrDefault(x => x.Id == option.Id);
                if (row is null) { row = new() { Id = option.Id, TenantId = tenantId, VersionId = draft.Id, FieldId = input.Id }; db.Add(row); }
                row.SortOrder = i; row.Label = option.Label;
            }
        }
        db.RemoveRange(fields.Where(x => !requestedFields.Contains(x.Id)));
        db.RemoveRange(sections.Where(x => !requestedSections.Contains(x.Id)));
        draft.Title = definition.Title; draft.Description = definition.Description; draft.Instructions = definition.Instructions;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        return await SaveAsync(template, auth0Sub, cancellationToken);
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
        return await SaveAsync(template, auth0Sub, cancellationToken);
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
        var sectionMap = new Dictionary<Guid, Guid>();
        foreach (var section in content.Sections ?? [])
        {
            var id = Guid.NewGuid();
            sectionMap[section.Id] = id;
            db.Add(new IntakeFormSection { Id = id, TenantId = tenantId, VersionId = draft.Id, SortOrder = section.SortOrder, Title = section.Title, Description = section.Description });
        }
        for (var i = 0; i < content.Fields.Count; i++)
        {
            var field = content.Fields[i]; var id = Guid.NewGuid();
            Guid? sectionId = field.SectionId is { } sid && sectionMap.TryGetValue(sid, out var mapped) ? mapped : null;
            db.Add(new IntakeFormField { Id = id, TenantId = tenantId, VersionId = draft.Id, SortOrder = i, FieldType = field.Type, Label = field.Label, HelpText = field.HelpText, IsRequired = field.Required, SectionId = sectionId });
            for (var j = 0; j < field.Choices.Count; j++) db.Add(new IntakeFieldChoice { Id = Guid.NewGuid(), TenantId = tenantId, VersionId = draft.Id, FieldId = id, SortOrder = j, Label = field.Choices[j].Label });
        }
        template.UpdatedAt = draft.CreatedAt;
        return await SaveAsync(template, auth0Sub, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<IntakeTemplateSummary>>> ListAsync(Guid tenantId, string actor, int page = 1, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, actor, cancellationToken)) return Result.Failure<IReadOnlyList<IntakeTemplateSummary>>(ErrorCodes.PermissionDeniedError());
        if (page is < 1 or > 100000) return Result.Failure<IReadOnlyList<IntakeTemplateSummary>>(new Error("intake.validation"));
        var rows = await db.Set<IntakeFormTemplate>().AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync(cancellationToken);
        return Result.Success<IReadOnlyList<IntakeTemplateSummary>>(rows.Select(x => new IntakeTemplateSummary(x.Id, x.Name, x.IsActive, x.CurrentPublishedVersionId, Convert.ToBase64String(x.RowVersion))).ToArray());
    }
    public async Task<Result<IntakeTemplateDetail>> UpdateAsync(Guid tenantId, string actor, Guid templateId, string name, bool active, string revision, CancellationToken cancellationToken = default)
    {
        if (!await access.CanManageAsync(tenantId, actor, cancellationToken)) return Denied();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) return Invalid();
        var template = await TemplateAsync(tenantId, templateId, cancellationToken);
        if (template is null) return Missing();
        if (revision != Convert.ToBase64String(template.RowVersion)) return Conflict();
        if (!active && await db.Set<ServiceIntakeForm>().AnyAsync(x => x.TenantId == tenantId && x.TemplateId == templateId, cancellationToken)) return Conflict();
        template.Name = name.Trim(); template.IsActive = active; template.UpdatedAt = DateTimeOffset.UtcNow;
        return await SaveAsync(template, actor, cancellationToken);
    }
    private async Task AuditAsync(string actor, string action, Guid id, CancellationToken ct)
    {
        if (audit is not null && correlation is not null) await audit.WriteAsync(new AuditEntry(Guid.NewGuid(), actor, action, "intake", id.ToString(), correlation.CorrelationId, DateTimeOffset.UtcNow), ct);
    }

    private async Task<Result<IntakeTemplateDetail>> SaveAsync(IntakeFormTemplate template, string actor, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        { db.ChangeTracker.Clear(); return Conflict(); }
        await AuditAsync(actor, "intake.template_changed", template.Id, ct);
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
    private Task<IntakeDefinition> DefinitionAsync(IntakeFormVersion version, CancellationToken ct) =>
        IntakeDefinitions.ReadAsync(db, version.TenantId, version.Id, ct);
    private static Result<IntakeTemplateDetail> Denied() => Result.Failure<IntakeTemplateDetail>(ErrorCodes.PermissionDeniedError());
    private static Result<IntakeTemplateDetail> Invalid() => Result.Failure<IntakeTemplateDetail>(new Error("intake.validation"));
    private static Result<IntakeTemplateDetail> Conflict() => Result.Failure<IntakeTemplateDetail>(new Error("intake.conflict"));
    private static Result<IntakeTemplateDetail> Missing() => Result.Failure<IntakeTemplateDetail>(Error.NotFound("Intake form"));
}
