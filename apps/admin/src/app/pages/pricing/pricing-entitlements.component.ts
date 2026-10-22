import { Component, computed, signal } from "@angular/core";
import {
  formatSubscriptionSku,
  PLAN_ADMIN_MATRIX_ROW_DEFS,
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
  injectAdeniTranslate,
} from "@adeni/ui";

type EntitlementMatrixRow = {
  rowKey: string;
  label: string;
  starter: string;
  professional: string;
  business: string;
  kind: "limit" | "flag";
  adminNote?: string;
};

type Tr = (key: string) => string;

function formatLimitValue(tr: Tr, key: keyof PlanEntitlementSeed, value: number | null): string {
  if (value == null) {
    return tr(key === "maxLocations" ? "admin.pricing.entitlementMultiple" : "admin.pricing.entitlementUnlimited");
  }
  return String(value);
}

function formatEntitlementCell(
  tr: Tr,
  sku: SubscriptionSku,
  key: keyof PlanEntitlementSeed,
  kind: "limit" | "flag",
): string {
  const seed = PLAN_ENTITLEMENTS[sku];
  const raw = seed[key];
  if (kind === "limit") {
    return formatLimitValue(tr, key, raw as number | null);
  }
  return tr(raw ? "admin.pricing.entitlementYes" : "admin.pricing.entitlementNo");
}

function buildMatrixRows(tr: Tr): EntitlementMatrixRow[] {
  return PLAN_ADMIN_MATRIX_ROW_DEFS.map((def) => ({
    rowKey: def.key,
    label: def.label,
    kind: def.kind,
    adminNote: def.adminNote,
    starter: formatEntitlementCell(tr, "starter", def.key, def.kind),
    professional: formatEntitlementCell(tr, "professional", def.key, def.kind),
    business: formatEntitlementCell(tr, "business", def.key, def.kind),
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
  protected readonly tr = injectAdeniTranslate();
  readonly searchText = signal("");
  readonly matrixRows = computed<EntitlementMatrixRow[]>(() => buildMatrixRows(this.tr));

  readonly filteredRows = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    const items = this.matrixRows();
    if (!query) {
      return items;
    }
    return items.filter((row) => row.label.toLowerCase().includes(query));
  });

  readonly columns = computed<AdeniDataTableColumn<EntitlementMatrixRow>[]>(() => [
    {
      id: "feature",
      header: this.tr("admin.pricing.capability"),
      cell: (row) => row.label,
      sortValue: (row) => row.label.toLowerCase(),
    },
    ...SUBSCRIPTION_SKUS.map((sku) => ({
      id: sku,
      header: formatSubscriptionSku(sku),
      cell: (row: EntitlementMatrixRow) => row[sku],
      sortValue: (row: EntitlementMatrixRow) => row[sku],
    })),
  ]);

  readonly trackRow = (row: EntitlementMatrixRow): string => row.rowKey;

  cellTone(value: string, kind: "limit" | "flag"): "success" | "neutral" | "warning" {
    if (kind === "limit") {
      return "neutral";
    }
    if (value === this.tr("admin.pricing.entitlementYes")) {
      return "success";
    }
    return "neutral";
  }

  onSearch(value: string): void {
    this.searchText.set(value);
  }
}
