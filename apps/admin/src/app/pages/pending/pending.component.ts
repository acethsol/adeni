import { Component, inject, OnInit, signal } from "@angular/core";
import type { PendingBusiness } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
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

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<PendingBusiness[]>([]);
  readonly busyId = signal<string | null>(null);
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
