import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  formatTenantStatus,
  type AdminBusinessDirectoryStats,
  type AdminBusinessSummary,
  type AdminMarket,
  type PendingBusiness,
  formatSubscriptionSku,
  resolveSubscriptionSku,
  type SubscriptionSku,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

type LoadState = {
  pendingPreview: PendingBusiness[] | null;
  pendingTotal: number | null;
  businessStats: AdminBusinessDirectoryStats | null;
  recentBusinesses: AdminBusinessSummary[] | null;
  marketsPreview: AdminMarket[] | null;
  liveMarketCount: number | null;
};

const EMPTY_LOAD_STATE: LoadState = {
  pendingPreview: null,
  pendingTotal: null,
  businessStats: null,
  recentBusinesses: null,
  marketsPreview: null,
  liveMarketCount: null,
};

@Component({
  selector: "app-admin-dashboard",
  standalone: true,
  imports: [RouterLink, PortalPageComponent, AdeniStaffButtonComponent, AdeniCarbonIconComponent],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class AdminDashboardComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(false);
  readonly initialLoad = signal(true);
  readonly data = signal<LoadState>(EMPTY_LOAD_STATE);

  readonly pending = computed(() => this.data().pendingPreview);
  readonly pendingTotal = computed(() => this.data().pendingTotal);
  readonly businessStats = computed(() => this.data().businessStats);
  readonly markets = computed(() => this.data().marketsPreview);

  readonly queue = computed(() =>
    [...(this.pending() ?? [])].sort((a, b) => a.createdAt.localeCompare(b.createdAt)),
  );

  readonly waitingCount = computed(() => this.pendingTotal());

  readonly verifiedCount = computed(() => this.businessStats()?.verifiedCount ?? null);

  readonly totalBusinessCount = computed(() => this.businessStats()?.totalCount ?? null);

  readonly liveCount = computed(() => this.data().liveMarketCount);

  readonly pageDescription = computed(
    () => "Verification queue, markets, and tenant directory at a glance.",
  );

  readonly headline = computed(() => {
    const total = this.pendingTotal();
    if (total === null) {
      return "Verification queue could not be loaded.";
    }
    if (total === 1) {
      return "1 business is waiting for verification.";
    }
    if (total > 1) {
      return `${total} businesses are waiting for verification.`;
    }
    const offline = (this.markets() ?? []).filter((item) => !item.isLive);
    if (offline.length === 1) {
      return `${offline[0].name} is offline.`;
    }
    if (offline.length > 1) {
      return `${offline.map((item) => item.name).join(" and ")} are offline.`;
    }
    return "Verification queue is clear.";
  });

  readonly statusMix = computed(() => {
    const stats = this.businessStats();
    if (!stats) {
      return [0, 1, 2, 3, 4].map((status) => ({
        key: String(status),
        label: formatTenantStatus(status),
        count: 0,
        tone: `tone-${status}`,
      }));
    }
    const byStatus = new Map(stats.statusMix.map((row) => [row.status, row.count]));
    return [0, 1, 2, 3, 4].map((status) => ({
      key: String(status),
      label: formatTenantStatus(status),
      count: byStatus.get(status) ?? 0,
      tone: `tone-${status}`,
    }));
  });

  readonly skuMix = computed(() => {
    const stats = this.businessStats();
    const bySku = new Map((stats?.skuMix ?? []).map((row) => [row.sku, row.count]));
    return (["starter", "professional", "business"] as const).map((sku: SubscriptionSku) => ({
      key: sku,
      label: formatSubscriptionSku(sku),
      count: bySku.get(sku) ?? 0,
      tone: `sku-${sku}`,
    }));
  });

  readonly recent = computed(() => this.data().recentBusinesses ?? []);

  readonly formatTenantStatus = formatTenantStatus;
  readonly planLabel = (business: AdminBusinessSummary) =>
    formatSubscriptionSku(resolveSubscriptionSku(business.subscriptionSku));
  readonly ageLabel = ageLabel;
  readonly shortDate = shortDate;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const isRefresh = !this.initialLoad();
    this.loading.set(isRefresh);
    try {
      const core = await this.api.withAuthorizedClient(async (client) => {
        const [pendingPage, businessStats, marketsPage, marketStats] = await Promise.all([
          client.listPendingBusinesses({ page: 1, pageSize: 6 }).catch(() => null),
          client.getAdminBusinessDirectoryStats().catch(() => null),
          client.listAdminMarkets({ page: 1, pageSize: 6 }).catch(() => null),
          client.getAdminMarketCatalogStats().catch(() => null),
        ]);
        return {
          pendingPreview: pendingPage?.items ?? null,
          pendingTotal: pendingPage?.totalCount ?? null,
          businessStats,
          marketsPreview: marketsPage?.items ?? null,
          liveMarketCount: marketStats?.liveCount ?? null,
        };
      });

      this.data.update((prev) => ({
        ...prev,
        ...core,
      }));

      void this.api.withAuthorizedClient(async (client) => {
        const recentPage = await client
          .listAdminBusinesses({ page: 1, pageSize: 6 })
          .catch(() => null);
        this.data.update((prev) => ({
          ...prev,
          recentBusinesses: recentPage?.items ?? null,
        }));
      });
    } catch (caught) {
      this.data.set(EMPTY_LOAD_STATE);
      this.feedback.error(this.apiErr(caught, "admin.dashboard.couldNotLoadAdminDashboard"));
    } finally {
      this.initialLoad.set(false);
      this.loading.set(false);
    }
  }
}

function ageLabel(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) {
    return "";
  }
  const minutes = Math.max(0, Math.floor((Date.now() - then) / 60000));
  if (minutes < 1) {
    return "Just now";
  }
  if (minutes < 60) {
    return `${minutes}m ago`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `${hours}h ago`;
  }
  const days = Math.floor(hours / 24);
  return days === 1 ? "1 day ago" : `${days} days ago`;
}

function shortDate(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return "";
  }
  return new Intl.DateTimeFormat("en", { month: "short", day: "numeric", year: "numeric" }).format(date);
}
