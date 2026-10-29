import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatSlotTime, type CustomerMembership, type CustomerMembershipStatus } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { CustomerApiService } from "../../core/services/customer-api.service";

type MembershipTone = "pending" | "active" | "inactive";

/** Signed-in customer's memberships across businesses (read-only). */
@Component({
  selector: "app-my-memberships",
  standalone: true,
  imports: [RouterLink, AdeniCarbonIconComponent],
  templateUrl: "./my-memberships.component.html",
  styleUrl: "./my-memberships.component.scss",
})
export class MyMembershipsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly localeService = inject(AdeniLocaleService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly memberships = signal<CustomerMembership[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await this.api.withAuthorizedClient((c) => c.retention.myMemberships());
      this.memberships.set(result.items);
    } catch (caught) {
      this.memberships.set([]);
      this.error.set(this.apiErr(caught, "discover.memberships.loadError"));
    } finally {
      this.loading.set(false);
    }
  }

  when(iso: string): string {
    return formatSlotTime(iso, this.localeService.locale());
  }

  tone(m: CustomerMembership): MembershipTone {
    if (m.status === "active" || m.status === "past_due" || m.status === "frozen" || m.status === "cancel_pending") {
      return "active";
    }
    if (m.status === "pending") return "pending";
    return "inactive";
  }

  usedPercent(used: number, granted: number): number {
    if (granted <= 0) return 0;
    return Math.min(100, Math.max(0, Math.round((used / granted) * 100)));
  }

  statusLabel(status: CustomerMembershipStatus): string {
    switch (status) {
      case "pending":
        return this.tr("portal.memberships.statusPending");
      case "active":
        return this.tr("portal.memberships.statusActive");
      case "past_due":
        return this.tr("portal.memberships.statusPastDue");
      case "frozen":
        return this.tr("portal.memberships.statusFrozen");
      case "cancel_pending":
        return this.tr("portal.memberships.statusCancelPending");
      case "cancelled":
        return this.tr("portal.memberships.statusCancelled");
      case "expired":
        return this.tr("portal.memberships.statusExpired");
      default:
        return status;
    }
  }
}
