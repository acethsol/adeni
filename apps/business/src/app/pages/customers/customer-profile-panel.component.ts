import { CustomerSegmentsPanelComponent } from "./customer-segments-panel.component";
import { Component, effect, inject, input } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatPrice, formatSlotTime, type CustomerActivityItem } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniLocaleService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessSessionService } from "../../core/services/business-session.service";
import { CustomerProfileDataService } from "./customer-profile-data.service";

@Component({
  selector: "app-customer-profile-panel",
  standalone: true,
  imports: [
    RouterLink,
    CustomerSegmentsPanelComponent,
    AdeniCarbonIconComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
  ],
  providers: [CustomerProfileDataService],
  templateUrl: "./customer-profile-panel.component.html",
  styleUrl: "./customer-profile-panel.component.scss",
})
export class CustomerProfilePanelComponent {
  readonly customerId = input.required<string>();
  readonly locationId = input<string>();
  readonly activityVisible = input(false);
  readonly overviewVisible = input(false);
  readonly data = inject(CustomerProfileDataService);
  readonly session = inject(BusinessSessionService);
  readonly tr = injectAdeniTranslate();
  readonly apiErr = injectAdeniApiError();
  private readonly locale = inject(AdeniLocaleService);
  readonly formatSlotTime = (value: string): string => formatSlotTime(value, this.locale.locale());
  readonly formatPrice = (amount: number, currency: string): string => formatPrice(amount, currency, this.locale.locale());

  constructor() {
    effect(onCleanup => {
      const id = this.customerId();
      const location = this.locationId();
      let active = true;
      onCleanup(() => { active = false; this.data.cancel(); });
      void this.session.ensureLoaded().then(() => {
        if (active && this.session.can("business.bookings")) void this.data.load(id, location);
      });
    });
  }

  refresh(): void { void this.data.load(this.customerId(), this.locationId()); }

  bookingQueryParams(item: CustomerActivityItem): { focus: string; tab: string } {
    return { focus: item.sourceId, tab: "past" };
  }

  activityStatusTone(status: string): AdeniPillTone {
    switch (status) {
      case "Pending":
        return "warning";
      case "Confirmed":
      case "CheckedIn":
      case "Waiting":
      case "Called":
      case "InService":
        return "info";
      case "Completed":
      case "Paid":
      case "Done":
        return "success";
      case "Rejected":
      case "Cancelled":
      case "NoShow":
        return "danger";
      default:
        return "neutral";
    }
  }
}
