import { Component, effect, inject, input } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatPrice, formatSlotTime, type CustomerActivityItem } from "@adeni/shared";
import { AdeniLocaleService, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { CustomerProfileDataService } from "./customer-profile-data.service";

@Component({
  selector: "app-customer-profile-panel",
  standalone: true,
  imports: [RouterLink],
  providers: [CustomerProfileDataService],
  templateUrl: "./customer-profile-panel.component.html",
  styles: `:host { display: block; } section { padding: 1rem; margin-block: 1rem; border: 1px solid var(--border); border-radius: .5rem; }
    dl { display: flex; flex-wrap: wrap; gap: 1.5rem; } dd { margin: .25rem 0; font-weight: 600; }
    li { margin-block: 1rem; } time, small { display: block; } button { cursor: pointer; }`,
})
export class CustomerProfilePanelComponent {
  readonly customerId = input.required<string>();
  readonly locationId = input<string>();
  readonly activityVisible = input(false);
  readonly overviewVisible = input(false);
  readonly data = inject(CustomerProfileDataService);
  readonly session = inject(PortalSessionService);
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
        if (active && this.session.can("portal.bookings")) void this.data.load(id, location);
      });
    });
  }

  refresh(): void { void this.data.load(this.customerId(), this.locationId()); }

  bookingQueryParams(item: CustomerActivityItem): { focus: string; tab: string } {
    return { focus: item.sourceId, tab: "past" };
  }
}
