namespace Adeni.Application.Reporting;

using Adeni.Domain.Common;

public sealed record DocumentPreview(byte[] Bytes, string FileName);
public interface IDocumentPreviewService
{
    Task<Result<DocumentPreview>> GetAsync(Guid tenantId, Guid documentId, string actor, Guid? locationId, CancellationToken ct);
}

public interface IDocumentBookingScopeQuery
{
    Task<Result<Guid>> GetLocationAsync(Guid tenantId, Guid bookingId, CancellationToken ct);
}

/// <summary>Existing Finance rendering, with bounded inputs for private previews/downloads.</summary>
public interface IBillingDocumentPreviewSource
{
    Task<Result<byte[]>> RenderAsync(Guid documentId, CancellationToken ct);
}
