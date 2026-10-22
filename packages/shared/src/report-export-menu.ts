import type { TenantEntitlements } from "./schemas";
import {
  hasBasicExportsEntitlement,
  hasExcelExportEntitlement,
} from "./entitlements";
import type { ReportExportFormat } from "./report-keys";

export type ReportExportMenuItemConfig = {
  format: ReportExportFormat;
  label: string;
  disabled?: boolean;
  hint?: string;
};

export function buildReportExportMenuItems(options: {
  entitlements: TenantEntitlements | null | undefined;
  csv?: boolean;
  xlsx?: boolean;
  pdf?: boolean | { disabled: boolean; hint?: string };
  labels?: Partial<Record<ReportExportFormat, string>>;
}): ReportExportMenuItemConfig[] {
  const entitlements = options.entitlements ?? null;
  const basic = hasBasicExportsEntitlement(entitlements);
  const excel = hasExcelExportEntitlement(entitlements);
  const items: ReportExportMenuItemConfig[] = [];

  if (options.csv !== false) {
    items.push({
      format: "csv",
      label: options.labels?.csv ?? "CSV (.csv)",
      disabled: !basic,
      hint: basic ? undefined : "Upgrade for exports",
    });
  }

  if (options.xlsx) {
    items.push({
      format: "xlsx",
      label: options.labels?.xlsx ?? "Excel (.xlsx)",
      disabled: !basic || !excel,
      hint: excel ? undefined : "Pro plan required for Excel",
    });
  }

  if (options.pdf) {
    const pdfOption =
      typeof options.pdf === "object"
        ? options.pdf
        : { disabled: !options.pdf, hint: options.pdf ? undefined : "PDF not available" };
    items.push({
      format: "pdf",
      label: options.labels?.pdf ?? "PDF (.pdf)",
      disabled: pdfOption.disabled,
      hint: pdfOption.hint,
    });
  }

  return items;
}
