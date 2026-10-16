import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  formatAmountMinor,
  formatTenantStatus,
  type AdminBusinessDirectoryStats,
  type AdminBusinessSummary,
  type AdminMarket,
  type AdminSubscriptionMetrics,
  type PendingBusiness,
  formatSubscriptionSku,
  resolveSubscriptionSku,
  type SubscriptionTier,
} from "@adeni/shared";
import { AdeniFeedbackService } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

type LoadState = {
  pendingPreview: PendingBusiness[] | null;
  pendingTotal: number | null;
  businessStats: AdminBusinessDirectoryStats | null;
  recentBusinesses: AdminBusinessSummary[] | null;
  marketsPreview: AdminMarket[] | null;
  liveMarketCount: number | null;
  subscriptionMetrics: AdminSubscriptionMetrics | null;
};

const TIER_LABELS: Record<SubscriptionTier, string> = {
  free: "Starter",
  pro: "Professional",
  business: "Business",
};

@Component({
  selector: "app-admin-dashboard",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./dashboard.component.html",
  styleUrl: "./dashboard.component.scss",
})
export class AdminDashboardComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly data = signal<LoadState | null>(null);

  readonly pending = computed(() => this.data()?.pendingPreview ?? null);
  readonly pendingTotal = computed(() => this.data()?.pendingTotal ?? null);
  readonly businessStats = computed(() => this.data()?.businessStats ?? null);
  readonly markets = computed(() => this.data()?.marketsPreview ?? null);
  readonly subscriptionMetrics = computed(() => this.data()?.subscriptionMetrics ?? null);

  readonly queue = computed(() =>
    [...(this.pending() ?? [])].sort((a, b) => a.createdAt.localeCompare(b.createdAt)),
  );

  readonly waitingCount = computed(() => this.pendingTotal());

  readonly verifiedCount = computed(() => this.businessStats()?.verifiedCount ?? null);

  readonly paidCount = computed(
    () => this.subscriptionMetrics()?.paidTenantCount ?? null,
  );

  readonly mrrLabel = computed(() => {
    const metrics = this.subscriptionMetrics();
    if (!metrics) {
      return null;
    }
    const rows =
      metrics.mrrSource === "synced_charges"
        ? metrics.syncedMrrByCurrency
        : metrics.estimatedMrrByCurrency;
    if (rows.length === 0) {
      return "—";
    }
    return rows
      .map((row) => formatAmountMinor(row.amountMinor, row.currency))
      .join(" · ");
  });

  readonly mrrHint = computed(() => {
    const metrics = this.subscriptionMetrics();
    if (!metrics) {
      return "MRR unavailable";
    }
    return metrics.mrrSource === "synced_charges"
      ? "From synced charges this month"
      : "Estimated from list prices";
  });

  readonly liveCount = computed(() => this.data()?.liveMarketCount ?? null);

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

  readonly tierMix = computed(() => {
    const stats = this.businessStats();
    const byTier = new Map((stats?.tierMix ?? []).map((row) => [row.tier, row.count]));
    return (["free", "pro", "business"] as const).map((tier) => ({
      key: tier,
      label: TIER_LABELS[tier],
      count: byTier.get(tier) ?? 0,
      tone: `tier-${tier}`,
    }));
  });

  readonly recent = computed(() => this.data()?.recentBusinesses ?? []);

  readonly formatTenantStatus = formatTenantStatus;
  readonly tierLabel = (tier: SubscriptionTier) => TIER_LABELS[tier];
  readonly planLabel = (business: AdminBusinessSummary) =>
    formatSubscriptionSku(
      resolveSubscriptionSku(business.subscriptionSku, business.subscriptionTier),
    );
  readonly ageLabel = ageLabel;
  readonly shortDate = shortDate;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const next = await this.api.withAuthorizedClient(async (client) => {
          const [
            pendingPage,
            businessStats,
            recentPage,
            marketsPage,
            marketStats,
            subscriptionMetrics,
          ] = await Promise.all([
            client.listPendingBusinesses({ page: 1, pageSize: 6 }).catch(() => null),
            client.getAdminBusinessDirectoryStats().catch(() => null),
            client.listAdminBusinesses({ page: 1, pageSize: 6 }).catch(() => null),
            client.listAdminMarkets({ page: 1, pageSize: 6 }).catch(() => null),
            client.getAdminMarketCatalogStats().catch(() => null),
            client.getAdminSubscriptionMetrics().catch(() => null),
          ]);
          return {
            pendingPreview: pendingPage?.items ?? null,
            pendingTotal: pendingPage?.totalCount ?? null,
            businessStats,
            recentBusinesses: recentPage?.items ?? null,
            marketsPreview: marketsPage?.items ?? null,
            liveMarketCount: marketStats?.liveCount ?? null,
            subscriptionMetrics,
          };
        });
        this.data.set(next);
      }, "Loading dashboard…", "Fetching queue, businesses, and markets");
    } catch {
      this.data.set({
        pendingPreview: null,
        pendingTotal: null,
        businessStats: null,
        recentBusinesses: null,
        marketsPreview: null,
        liveMarketCount: null,
        subscriptionMetrics: null,
      });
      this.feedback.error("Could not load admin dashboard.");
    } finally {
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
