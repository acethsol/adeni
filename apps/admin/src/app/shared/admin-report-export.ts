import type { ReportExportFormat } from "@adeni/shared";
import type { AdeniApiClient } from "@adeni/api-client";

export type AdminListExportOptions = {
  reportKey: string;
  format: ReportExportFormat;
  q?: string;
  kind?: string;
  catalogVersionCode?: string;
  from?: Date;
  to?: Date;
};

export async function downloadAdminListExport(
  client: AdeniApiClient,
  options: AdminListExportOptions,
): Promise<Blob> {
  const to = options.to ?? new Date();
  const from = options.from ?? new Date(to.getTime());
  if (!options.from) {
    from.setFullYear(from.getFullYear() - 1);
  }

  const format = options.format === "pdf" ? "csv" : options.format;

  return client.downloadAdminReportExport({
    reportKey: options.reportKey,
    from: from.toISOString(),
    to: to.toISOString(),
    format,
    q: options.q,
    kind: options.kind,
    catalogVersionCode: options.catalogVersionCode,
  });
}

export function adminListExportFileName(reportKey: string, format: ReportExportFormat): string {
  const stamp = new Date().toISOString().slice(0, 10);
  const ext = format === "xlsx" ? "xlsx" : "csv";
  return `${reportKey}-${stamp}.${ext}`;
}

export function triggerBrowserDownload(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}
