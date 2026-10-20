import { Component, computed, signal } from "@angular/core";
import {
  formatSubscriptionSku,
  PLAN_ENTITLEMENTS,
  SUBSCRIPTION_SKUS,
  type PlanEntitlementSeed,
  type SubscriptionSku,
} from "@adeni/shared";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
} from "@adeni/ui";

type EntitlementMatrixRow = {
  rowKey: string;
  label: string;
  starter: string;
  professional: string;
  business: string;
  kind: "limit" | "flag";
};

const ENTITLEMENT_ROW_DEFS: { key: keyof PlanEntitlementSeed; label: string; kind: "limit" | "flag" }[] =
  [
    { key: "maxLocations", label: "Business locations", kind: "limit" },
    { key: "maxPractitioners", label: "Team / practitioners", kind: "limit" },
    { key: "unlimitedBookings", label: "Unlimited bookings", kind: "flag" },
    { key: "messaging", label: "Adeni inbox", kind: "flag" },
    { key: "advancedReminders", label: "Automated reminders", kind: "flag" },
    { key: "whatsappIntegration", label: "WhatsApp integration", kind: "flag" },
    { key: "waitlist", label: "Waitlist", kind: "flag" },
    { key: "advancedAnalytics", label: "Advanced analytics", kind: "flag" },
    { key: "customerRetentionAnalytics", label: "Customer retention analytics", kind: "flag" },
    { key: "staffPerformanceAnalytics", label: "Staff performance analytics", kind: "flag" },
    { key: "multiLocationReporting", label: "Multi-location reporting", kind: "flag" },
    { key: "prioritySupport", label: "Priority support", kind: "flag" },
    { key: "publicProfile", label: "Public profile", kind: "flag" },
    { key: "discoverListing", label: "Discover listing", kind: "flag" },
    { key: "onlinePayments", label: "Online payments", kind: "flag" },
    { key: "deposits", label: "Deposits", kind: "flag" },
    { key: "invoicesReceipts", label: "Invoices & receipts", kind: "flag" },
  ];

function formatLimitValue(key: keyof PlanEntitlementSeed, value: number | null): string {
  if (value == null) {
    return key === "maxLocations" ? "Multiple" : "Unlimited";
  }
  return String(value);
}

function formatEntitlementCell(
  sku: SubscriptionSku,
  key: keyof PlanEntitlementSeed,
  kind: "limit" | "flag",
): string {
  const seed = PLAN_ENTITLEMENTS[sku];
  const raw = seed[key];
  if (kind === "limit") {
    return formatLimitValue(key, raw as number | null);
  }
  return raw ? "Yes" : "No";
}

function buildMatrixRows(): EntitlementMatrixRow[] {
  return ENTITLEMENT_ROW_DEFS.map((def) => ({
    rowKey: def.key,
    label: def.label,
    kind: def.kind,
    starter: formatEntitlementCell("starter", def.key, def.kind),
    professional: formatEntitlementCell("professional", def.key, def.kind),
    business: formatEntitlementCell("business", def.key, def.kind),
  }));
}

@Component({
  selector: "app-admin-pricing-entitlements",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniPillComponent,
  ],
  templateUrl: "./pricing-entitlements.component.html",
  styleUrl: "./pricing-entitlements.component.scss",
})
export class AdminPricingEntitlementsComponent {
  readonly searchText = signal("");
  readonly matrixRows = signal<EntitlementMatrixRow[]>(buildMatrixRows());

  readonly filteredRows = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    const items = this.matrixRows();
    if (!query) {
      return items;
    }
    return items.filter((row) => row.label.toLowerCase().includes(query));
  });

  readonly columns: AdeniDataTableColumn<EntitlementMatrixRow>[] = [
    { id: "feature", header: "Capability", cell: (row) => row.label },
    ...SUBSCRIPTION_SKUS.map((sku) => ({
      id: sku,
      header: formatSubscriptionSku(sku),
      cell: (row: EntitlementMatrixRow) => row[sku],
    })),
  ];

  readonly trackRow = (row: EntitlementMatrixRow): string => row.rowKey;

  cellTone(value: string, kind: "limit" | "flag"): "success" | "neutral" | "warning" {
    if (kind === "limit") {
      return "neutral";
    }
    if (value === "Yes") {
      return "success";
    }
    return "neutral";
  }

  onSearch(value: string): void {
    this.searchText.set(value);
  }
}
