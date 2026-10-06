import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import type { PendingBusiness } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

@Component({
  selector: "app-admin-pending",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./pending.component.html",
  styleUrl: "./pending.component.scss",
})
export class AdminPendingComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBusiness[]>([]);
  readonly query = signal("");
  readonly visible = computed(() =>
    this.items().filter((item) => {
      const needle = this.query().trim().toLowerCase();
      if (!needle) {
        return true;
      }
      return `${item.name} ${item.slug} ${item.marketId} ${item.status}`.toLowerCase().includes(needle);
    }),
  );
  readonly busyId = signal<string | null>(null);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.query.set(params.get("q") ?? "");
    });
  }
  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const pending = await this.api.withAuthorizedClient((c) => c.getPendingBusinesses());
      this.items.set(pending);
    } catch {
      this.error.set("Could not load pending businesses. Use Auth0 admin or dev admin sub.");
    } finally {
      this.loading.set(false);
    }
  }

  async approve(item: PendingBusiness): Promise<void> {
    this.busyId.set(item.id);
    try {
      await this.api.withAuthorizedClient((c) => c.approvePendingBusiness(item.id));
      this.items.update((list) => list.filter((x) => x.id !== item.id));
    } catch {
      this.error.set("Approve failed.");
    } finally {
      this.busyId.set(null);
    }
  }

  async reject(item: PendingBusiness): Promise<void> {
    const reason = prompt("Rejection reason (min 10 chars) for " + item.name + "?");
    if (!reason || reason.trim().length < 10) return;
    this.busyId.set(item.id);
    try {
      await this.api.withAuthorizedClient((c) => c.rejectPendingBusiness(item.id, reason.trim()));
      this.items.update((list) => list.filter((x) => x.id !== item.id));
    } catch {
      this.error.set("Reject failed.");
    } finally {
      this.busyId.set(null);
    }
  }
}
