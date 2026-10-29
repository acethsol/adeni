import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPortalEmptyStateComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  injectAdeniTranslate,
} from "@adeni/ui";
import { BusinessDetailFacade, type CommercialAuditEntry } from "./business-detail.facade";

function auditMatchesSearch(row: CommercialAuditEntry, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) {
    return true;
  }
  const haystack = [row.action, row.actorId, row.occurredAt, row.metadataJson ?? ""]
    .join(" ")
    .toLowerCase();
  return haystack.includes(q);
}

@Component({
  selector: "app-admin-business-commercial-audit",
  standalone: true,
  imports: [
    DatePipe,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
  ],
  templateUrl: "./business-commercial-audit.component.html",
  styleUrl: "./business-commercial-audit.component.scss",
})
export class BusinessCommercialAuditComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly facade = inject(BusinessDetailFacade);

  readonly searchQuery = signal("");
  readonly sortColumnId = signal<string | null>("when");
  readonly sortDirection = signal<AdeniDataTableSortDirection>("desc");

  readonly auditColumns = computed<AdeniDataTableColumn<CommercialAuditEntry>[]>(() => [
    {
      id: "when",
      header: this.tr("businessApp.plan.date"),
      cell: (row) => row.occurredAt,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "action",
      header: this.tr("admin.businesses.action"),
      cell: (row) => row.action,
      sortValue: (row) => row.action,
    },
    {
      id: "actor",
      header: this.tr("admin.businesses.actor"),
      cell: (row) => row.actorId,
      sortValue: (row) => row.actorId,
    },
  ]);

  readonly trackAuditRow = (row: CommercialAuditEntry): string => row.id;

  readonly filteredRows = computed(() => {
    const q = this.searchQuery();
    return this.facade.commercialAudit().filter((row) => auditMatchesSearch(row, q));
  });

  readonly isEmptyCatalog = computed(
    () => !this.facade.loading() && this.facade.commercialAudit().length === 0,
  );

  readonly isEmptySearch = computed(
    () =>
      this.facade.commercialAudit().length > 0 &&
      this.filteredRows().length === 0 &&
      this.searchQuery().trim().length > 0,
  );

  formatActionLabel(action: string): string {
    if (action === "tenant.commercial_terms_updated") {
      return this.tr("admin.businesses.commercialTermsUpdated");
    }
    if (action === "tenant.commercial_bound") {
      return this.tr("admin.businesses.commercialTermsBound");
    }
    if (action === "subscription.webhook_applied") {
      return this.tr("admin.businesses.subscriptionWebhookApplied");
    }
    return action.replace(/_/g, " ");
  }
}
