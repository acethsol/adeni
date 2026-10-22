export const REPORT_KEYS = {
  bookingsRegister: "bookings-register",
  compensation: "compensation",
  salesSummary: "sales-summary",
  staffRoster: "staff-roster",
  retentionMembershipMrr: "retention-membership-mrr",
} as const;

export type ReportKey = (typeof REPORT_KEYS)[keyof typeof REPORT_KEYS];

export type ReportExportFormat = "csv" | "xlsx" | "pdf";
