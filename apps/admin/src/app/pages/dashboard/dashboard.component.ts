import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  formatTenantStatus,
  type AdminBusinessSummary,
  type AdminMarket,
  type PendingBusiness,
  type SubscriptionTier,
} from "@adeni/shared";
import { AdminApiService } from "../../core/services/admin-api.service";

type LoadState = {
  pending: PendingBusiness[] | null;
  businesses: AdminBusinessSummary[] | null;
  markets: AdminMarket[] | null;
};

type MixRow = {
  key: string;
  label: string;
  count: number;
  tone: string;
};

const TIER_LABELS: Record<SubscriptionTier, string> = {
  free: "Free",
  pro: "Pro",
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

  readonly loading = signal(true);
  readonly data = signal<LoadState | null>(null);

  readonly pending = computed(() => this.data()?.pending ?? null);
  readonly businesses = computed(() => this.data()?.businesses ?? null);
  readonly markets = computed(() => this.data()?.markets ?? null);

  readonly queue = computed(() =>
    [...(this.pending() ?? [])].sort((a, b) => a.createdAt.localeCompare(b.createdAt)),
  );

  readonly verifiedCount = computed(
    () => this.businesses()?.filter((item) => item.status === 2).length ?? null,
  );

  readonly paidCount = computed(
    () => this.businesses()?.filter((item) => item.subscriptionTier !== "free").length ?? null,
  );

  readonly liveCount = computed(() => this.markets()?.filter((item) => item.isLive).length ?? null);

  readonly headline = computed(() => {
    const pending = this.pending();
    if (pending === null) {
      return "Verification queue could not be loaded.";
    }
    if (pending.length === 1) {
      return "1 business is waiting for verification.";
    }
    if (pending.length > 1) {
      return `${pending.length} businesses are waiting for verification.`;
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

  readonly statusMix = computed(() =>
    mixRows(
      [0, 1, 2, 3, 4].map((status) => ({
        key: String(status),
        label: formatTenantStatus(status),
        count: this.businesses()?.filter((item) => item.status === status).length ?? 0,
        tone: `tone-${status}`,
      })),
    ),
  );

  readonly tierMix = computed(() =>
    mixRows(
      (["free", "pro", "business"] as const).map((tier) => ({
        key: tier,
        label: TIER_LABELS[tier],
        count: this.businesses()?.filter((item) => item.subscriptionTier === tier).length ?? 0,
        tone: `tier-${tier}`,
      })),
    ),
  );

  readonly recent = computed(() =>
    [...(this.businesses() ?? [])]
      .sort((a, b) => b.createdAt.localeCompare(a.createdAt))
      .slice(0, 6),
  );

  readonly formatTenantStatus = formatTenantStatus;
  readonly tierLabel = (tier: SubscriptionTier) => TIER_LABELS[tier];
  readonly ageLabel = ageLabel;
  readonly shortDate = shortDate;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const next = await this.api.withAuthorizedClient(async (client) => {
        const [pending, businesses, markets] = await Promise.all([
          client.getPendingBusinesses().then(
            (items) => items,
            () => null,
          ),
          client.getAdminBusinesses().then(
            (items) => items,
            () => null,
          ),
          client.getAdminMarkets().then(
            (items) => items,
            () => null,
          ),
        ]);
        return { pending, businesses, markets };
      });
      this.data.set(next);
    } catch {
      this.data.set({ pending: null, businesses: null, markets: null });
    } finally {
      this.loading.set(false);
    }
  }
}

function mixRows(rows: MixRow[]): MixRow[] {
  return rows;
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
