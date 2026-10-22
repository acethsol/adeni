import { t } from "./i18n";

export const REPORT_EXPORT_ERROR_CODES = {
  multiLocationReporting: "entitlements.multi_location_reporting",
  exports: "entitlements.exports",
  excelExport: "entitlements.excel_export",
} as const;

export function tenantReportExportErrorMessage(
  locale: string,
  options: { code?: string | null; message?: string | null; fallback: string },
): string {
  const code = options.code?.trim();
  if (code === REPORT_EXPORT_ERROR_CODES.multiLocationReporting) {
    const key = "errors.entitlements.multi_location_reporting";
    const translated = t(locale, key);
    if (translated !== key) {
      return translated;
    }
  }
  if (options.message?.trim()) {
    return options.message.trim();
  }
  return options.fallback;
}
