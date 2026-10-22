namespace Adeni.Application.Reporting;

public sealed record ReportExportPayload(
    string ContentType,
    string FileName,
    byte[] Body);
