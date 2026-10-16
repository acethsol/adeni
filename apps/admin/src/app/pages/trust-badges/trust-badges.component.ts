import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import { type PendingBadgeRequest, VERIFICATION_BADGE_LABELS } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { filterByQuery } from "../../shared/admin-list-filter";

@Component({
  selector: "app-admin-trust-badges",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    DatePipe,
  ],
  templateUrl: "./trust-badges.component.html",
  styleUrl: "./trust-badges.component.scss",
})
export class AdminTrustBadgesComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBadgeRequest[]>([]);
  readonly searchText = signal("");
  readonly visible = computed(() =>
    filterByQuery(this.items(), this.searchText(), (item) => [
      item.businessName,
      item.badgeType,
      item.referenceNumber,
    ]),
  );
  readonly isEmptyQueue = computed(() => !this.loading() && this.items().length === 0);
  readonly isEmptyFilter = computed(
    () => !this.loading() && this.items().length > 0 && this.visible().length === 0,
  );
  readonly busyKey = signal<string | null>(null);

  readonly badgeLabel = (type: PendingBadgeRequest["badgeType"]) => VERIFICATION_BADGE_LABELS[type];

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = params.get("q") ?? "";
      if (q) {
        this.searchText.set(q);
      }
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  rowKey(item: PendingBadgeRequest): string {
    return `${item.tenantId}:${item.badgeType}`;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const pending = await this.api.withAuthorizedClient((c) => c.listAdminPendingBadges());
        this.items.set(pending);
      }, "Loading badge queue…", "Trust badge requests");
    } catch {
      const message = "Could not load pending trust badges.";
      this.error.set(message);
      this.feedback.error(message);
    } finally {
      this.loading.set(false);
    }
  }

  async grant(item: PendingBadgeRequest): Promise<void> {
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.grantAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        "Granting…",
        item.businessName,
      );
      this.items.update((list) => list.filter((x) => this.rowKey(x) !== key));
      this.feedback.success(`${this.badgeLabel(item.badgeType)} granted for ${item.businessName}.`, "Granted");
    } catch {
      this.error.set("Grant failed.");
      this.feedback.error("Grant failed.");
    } finally {
      this.busyKey.set(null);
    }
  }

  async deny(item: PendingBadgeRequest): Promise<void> {
    const key = this.rowKey(item);
    this.busyKey.set(key);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.denyAdminVerificationBadge(item.tenantId, item.badgeType),
          ),
        "Denying…",
        item.businessName,
      );
      this.items.update((list) => list.filter((x) => this.rowKey(x) !== key));
      this.feedback.success(`Request denied for ${item.businessName}.`, "Denied");
    } catch {
      this.error.set("Deny failed.");
      this.feedback.error("Deny failed.");
    } finally {
      this.busyKey.set(null);
    }
  }
}
