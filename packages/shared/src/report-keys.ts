export const REPORT_KEYS = {
  giftCards: "gift-cards",
  bookingsRegister: "bookings-register",
  compensation: "compensation",
  salesSummary: "sales-summary",
  staffRoster: "staff-roster",
  tenantCustomers: "tenant-customers",
  tenantLocations: "tenant-locations",
  tenantServices: "tenant-services",
  retentionMembershipMrr: "retention-membership-mrr",
  adminBusinessDirectory: "admin-business-directory",
  adminPendingVerifications: "admin-pending-verifications",
  adminTransactions: "admin-transactions",
  adminPricingCatalogEntries: "admin-pricing-catalog-entries",
} as const;

export type ReportKey = (typeof REPORT_KEYS)[keyof typeof REPORT_KEYS];

export type ReportExportFormat = "csv" | "xlsx" | "pdf";
