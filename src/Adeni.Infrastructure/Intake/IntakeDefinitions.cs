namespace Adeni.Infrastructure.Intake;

using Adeni.Application.Intake;
using Adeni.Domain.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class IntakeDefinitions
{
    public static async Task<IntakeDefinition> ReadAsync(AdeniDbContext db, Guid tenantId, Guid versionId, CancellationToken ct)
    {
        var version = await db.Set<IntakeFormVersion>().AsNoTracking().SingleAsync(x => x.TenantId == tenantId && x.Id == versionId, ct);
        var sections = await db.Set<IntakeFormSection>().AsNoTracking().Where(x => x.TenantId == tenantId && x.VersionId == versionId).OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        var fields = await db.Set<IntakeFormField>().AsNoTracking().Where(x => x.TenantId == tenantId && x.VersionId == versionId).OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        var choices = await db.Set<IntakeFieldChoice>().AsNoTracking().Where(x => x.TenantId == tenantId && x.VersionId == versionId).OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        var sectionDtos = sections.Select(x => new IntakeSectionDefinition(x.Id, x.Title, x.Description, x.SortOrder)).ToArray();
        return new(version.Title, version.Description, version.Instructions, fields.Select(x => new IntakeFieldDefinition(x.Id, x.FieldType, x.Label, x.HelpText, x.IsRequired,
            choices.Where(c => c.FieldId == x.Id).Select(c => new IntakeChoiceDefinition(c.Id, c.Label)).ToArray(), x.SectionId)).ToArray(),
            sectionDtos.Length == 0 ? null : sectionDtos);
    }
}
